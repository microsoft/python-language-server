// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Documentation;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        private static readonly Hover EmptyHover = new Hover {
            contents = new MarkupContent { kind = MarkupKind.PlainText, value = string.Empty }
        };

        private DocumentationBuilder _displayTextBuilder;

        public override async Task<Hover> Hover(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            ProjectFiles.GetEntry(@params.textDocument, @params._version, out var entry, out var tree);

            TraceMessage($"Hover in {uri} at {@params.position}");

            var analysis = entry != null ? await entry.GetAnalysisAsync(50, cancellationToken) : null;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return EmptyHover;
            }

            tree = GetParseTree(entry, uri, cancellationToken, out var version) ?? tree;

            Expression expr;
            SourceSpan? exprSpan;

            var finder = new ExpressionFinder(tree, GetExpressionOptions.Hover);
            expr = finder.GetExpression(@params.position) as Expression;
            exprSpan = expr?.GetSpan(tree);

            if (expr == null) {
                TraceMessage($"No hover info found in {uri} at {@params.position}");
                return EmptyHover;
            }

            TraceMessage($"Getting hover for {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");

            var hover = await GetSelfHoverAsync(expr, analysis, tree, @params.position, cancellationToken);
            if (hover != null && hover != EmptyHover) {
                return hover;
            }

            // First try values from expression. This works for the import statement most of the time.
            var values = analysis.GetValues(expr, @params.position, null).ToList();
            if (values.Count == 0) {
                values = GetImportHover(entry, analysis, tree, @params.position, out hover).ToList();
                if(hover != null) {
                    return hover;
                }
            }

            if (values.Count > 0) {
                string originalExpr;
                if (expr is ConstantExpression || expr is ErrorExpression) {
                    originalExpr = null;
                } else {
                    originalExpr = @params._expr?.Trim();
                    if (string.IsNullOrEmpty(originalExpr)) {
                        originalExpr = expr.ToCodeString(tree, CodeFormattingOptions.Traditional);
                    }
                }

                var names = values.Select(GetFullTypeName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
                var res = new Hover {
                    contents = GetMarkupContent(
                        _displayTextBuilder.GetDocumentation(values, originalExpr),
                        _clientCaps.textDocument?.hover?.contentFormat),
                    range = exprSpan,
                    _version = version?.Version,
                    _typeNames = names
                };
                return res;
            }

            return EmptyHover;
        }

        private async Task<Hover> GetSelfHoverAsync(Expression expr, IModuleAnalysis analysis, PythonAst tree, Position position, CancellationToken cancellationToken) {
            if(!(expr is NameExpression name) || name.Name != "self") {
                return null;
            }
            var classDef = analysis.GetVariables(expr, position).FirstOrDefault(v => v.Type == VariableType.Definition);
            if(classDef == null) {
                return null;
            }

            var instanceInfo = classDef.Variable.Types.OfType<IInstanceInfo>().FirstOrDefault();
            if (instanceInfo == null) {
                return null;
            }

            var cd = instanceInfo.ClassInfo.ClassDefinition;
            var classParams = new TextDocumentPositionParams {
                position = cd.NameExpression.GetStart(tree),
                textDocument =  new TextDocumentIdentifier { uri = classDef.Location.DocumentUri }
            };
            return await Hover(classParams, cancellationToken);
        }

        private IEnumerable<AnalysisValue> GetImportHover(IPythonProjectEntry entry, IModuleAnalysis analysis, PythonAst tree, Position position, out Hover hover) {
            hover = null;

            var index = tree.LocationToIndex(position);
            var w = new ImportedModuleNameWalker(entry, index, tree);
            tree.Walk(w);

            if (w.ImportedType != null) {
                return analysis.GetValues(w.ImportedType.Name, position);
            }

            var sb = new StringBuilder();
            var span = SourceSpan.Invalid;
            foreach (var n in w.ImportedModules) {
                if (Analyzer.Modules.TryGetImportedModule(n.Name, out var modRef) && modRef.AnalysisModule != null) {
                    if (sb.Length > 0) {
                        sb.AppendLine();
                        sb.AppendLine();
                    }
                    sb.Append(_displayTextBuilder.GetModuleDocumentation(modRef));
                    span = span.IsValid ? span.Union(n.SourceSpan) : n.SourceSpan;
                }
            }
            if (sb.Length > 0) {
                hover = new Hover {
                    contents = sb.ToString(),
                    range = span
                };
            }
            return Enumerable.Empty<AnalysisValue>();
        }

        private static string GetFullTypeName(AnalysisValue value) {
            if (value is IHasQualifiedName qualName) {
                return qualName.FullyQualifiedName;
            }

            if (value is InstanceInfo ii) {
                return GetFullTypeName(ii.ClassInfo);
            }

            if (value is BuiltinInstanceInfo bii) {
                return GetFullTypeName(bii.ClassInfo);
            }

            return value?.Name;
        }
    }
}
