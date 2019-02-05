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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class HoverSource {
        private readonly IDocumentationSource _docSource;

        public HoverSource(IDocumentationSource docSource) {
            _docSource = docSource;
        }

        public async Task<Hover> GetHoverAsync(IDocumentAnalysis analysis, SourceLocation location, CancellationToken cancellationToken = default) {
            if (analysis is EmptyAnalysis) {
                return new Hover { contents = Resources.AnalysisIsInProgressHover };
            }

            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Hover, out var node, out var statement, out var scope);

            if (node is ConstantExpression || !(node is Expression expr)) {
                return null; // No hover for literals.
            }

            var range = new Range {
                start = expr.GetStart(analysis.Ast),
                end = expr.GetEnd(analysis.Ast),
            };

            var eval = analysis.ExpressionEvaluator;
            switch (statement) {
                case FromImportStatement fi when node is NameExpression nex: {
                    // In 'from A import B as C' B is not declared as a variable
                    // so we have to fetch the type manually.
                    var index = fi.Names.IndexOf(nex);
                    if (index >= 0) {
                        using (eval.OpenScope(scope)) {
                            var variable = eval.LookupNameInScopes(fi.Root.MakeString(), out _);
                            if (variable.GetPythonType() is IPythonModule mod) {
                                var v = mod.GetMember(nex.Name)?.GetPythonType();
                                return new Hover {
                                    contents = _docSource.GetHover(mod.Name, v),
                                    range = range
                                };
                            }
                        }
                    }

                    break;
                }
                case ImportStatement imp: {
                    // In 'import A as B' 'A' is not declared as a variable
                    // so we have to fetch the type manually.
                    var index = location.ToIndex(analysis.Ast);
                    var dottedName = imp.Names.FirstOrDefault(n => n.StartIndex <= index && index < n.EndIndex);
                    if (dottedName != null) {
                        var mod = analysis.Document.Interpreter.ModuleResolution.GetImportedModule(dottedName.MakeString());
                        if (mod != null) {
                            return new Hover {
                                contents = _docSource.GetHover(null, mod),
                                range = range
                            };
                        }
                    }
                    break;
                }
            }

            IMember value;
            IPythonType type;
            using (eval.OpenScope(scope)) {
                value = await analysis.ExpressionEvaluator.GetValueFromExpressionAsync(expr, cancellationToken);
                type = value?.GetPythonType();
                if (type == null) {
                    return null;
                }
            }

            // Figure out name, if any
            var name = (expr as MemberExpression)?.Name;
            name = name ?? (node as NameExpression)?.Name;

            // Special case hovering over self or cls
            if ((name.EqualsOrdinal("self") || name.EqualsOrdinal("cls")) && type is IPythonClassType) {
                return new Hover {
                    contents = _docSource.GetHover(null, type),
                    range = range
                };
            }

            name = name == null && statement is ClassDefinition cd ? cd.Name : name;
            name = name == null && statement is FunctionDefinition fd ? fd.Name : name;

            return new Hover {
                contents = _docSource.GetHover(name, value),
                range = range
            };
        }
    }
}
