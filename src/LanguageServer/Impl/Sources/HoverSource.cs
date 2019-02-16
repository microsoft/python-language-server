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
using Microsoft.Python.Analysis.Values;
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
                end = expr.GetEnd(analysis.Ast)
            };

            var eval = analysis.ExpressionEvaluator;
            switch (statement) {
                case FromImportStatement fi when node is NameExpression nex: {
                        var contents = HandleFromImport(fi, nex, scope, analysis);
                        if (contents != null) {
                            return new Hover {
                                contents = contents,
                                range = range
                            };
                        }
                        break;
                    }
                case ImportStatement imp: {
                        var contents = HandleImport(imp, location, analysis);
                        if (contents != null) {
                            return new Hover {
                                contents = contents,
                                range = range
                            };
                        }
                        break;
                    }
            }

            IMember value;
            IPythonType type;
            using (eval.OpenScope(analysis.Document, scope)) {
                value = await analysis.ExpressionEvaluator.GetValueFromExpressionAsync(expr, cancellationToken);
                type = value?.GetPythonType();
                if (type == null) {
                    return null;
                }
            }

            IPythonType self = null;
            string name = null;
            // If expression is A.B, trim applicable span to 'B'.
            if (expr is MemberExpression mex) {
                name = mex.Name;
                range = new Range {
                    start = mex.Target.GetEnd(analysis.Ast),
                    end = range.end
                };

                // In case of a member expression get the target since if we end up with method
                // of a generic class, the function will need specific type to determine its return
                // value correctly. I.e. in x.func() we need to determine type of x (self for func).
                var v = await analysis.ExpressionEvaluator.GetValueFromExpressionAsync(mex.Target, cancellationToken);
                self = v?.GetPythonType();
            }

            // Figure out name, if any
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
                contents = _docSource.GetHover(name, value, self),
                range = range
            };
        }

        private MarkupContent HandleImport(ImportStatement imp, SourceLocation location, IDocumentAnalysis analysis) {
            // In 'import A as B' 'A' is not declared as a variable
            // so we have to fetch the type manually.
            var index = location.ToIndex(analysis.Ast);
            var dottedName = imp.Names.FirstOrDefault(n => n.StartIndex <= index && index < n.EndIndex);
            if (dottedName != null) {
                var mod = analysis.Document.Interpreter.ModuleResolution.GetImportedModule(dottedName.MakeString());
                if (mod != null) {
                    return _docSource.GetHover(null, mod);
                }
            }
            return null;
        }

        private MarkupContent HandleFromImport(FromImportStatement fi, NameExpression nex, ScopeStatement scope, IDocumentAnalysis analysis) {
            var eval = analysis.ExpressionEvaluator;
            var index = fi.Names.IndexOf(nex);
            if (index >= 0) {
                // Name expression is B in 'from A import B as C'
                // In 'from A import B as C' B is not declared as a variable
                // so we have to fetch the type manually.
                using (eval.OpenScope(analysis.Document, scope)) {
                    var variable = eval.LookupNameInScopes(fi.Root.MakeString(), out _);

                    var module = variable == null 
                        ? GetModuleFromDottedName(fi, null, eval) 
                        : variable.GetPythonType<IPythonModule>();

                    if (module != null) {
                        var name = fi.Names[index].Name;
                        if (!string.IsNullOrEmpty(name)) {
                            var m = module.GetMember(name);
                            return m != null ? _docSource.GetHover(name, m) : null;
                        }
                    }
                }
            } else {
                // Name expression is the ModuleName such as B in 'from A.B.C ...'
                var module = GetModuleFromDottedName(fi, nex, eval);
                if (module != null) {
                    return _docSource.GetHover(module.Name, module);
                }
            }

            return null;
        }

        private IPythonModule GetModuleFromDottedName(FromImportStatement fi, NameExpression nex, IExpressionEvaluator eval) {
            IPythonModule module = null;
            var index = nex != null ? fi.Root.Names.IndexOf(n => n == nex) : fi.Root.Names.Count - 1;
            if (index >= 0) {
                module = eval.Interpreter.ModuleResolution.GetImportedModule(fi.Root.Names[0].Name);
                for (var i = 1; module != null && i <= index; i++) {
                    module = module.GetMember(fi.Root.Names[i].Name) as IPythonModule;
                }
            }
            return module;
        }
    }
}
