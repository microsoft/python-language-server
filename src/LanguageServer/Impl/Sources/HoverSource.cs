﻿// Copyright(c) Microsoft Corporation
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

using System.Collections.Generic;
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
                        var contents = HandleFromImport(fi, location, scope, analysis);
                        if (contents != null) {
                            return new Hover {
                                contents = contents,
                                range = range
                            };
                        }

                        break;
                    }
                case ImportStatement imp: {
                        var contents = HandleImport(imp, location, scope, analysis);
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

        private MarkupContent HandleImport(ImportStatement imp, SourceLocation location, ScopeStatement scope, IDocumentAnalysis analysis) {
            // 'import A.B, B.C, D.E as F, G, H'
            var eval = analysis.ExpressionEvaluator;
            var position = location.ToIndex(analysis.Ast);
            var dottedNameIndex = imp.Names?.IndexOf(n => n.StartIndex <= position && position < n.EndIndex);
            if (dottedNameIndex >= 0) {
                var dottedName = imp.Names[dottedNameIndex.Value];
                var module = GetModule(dottedName.MakeString(), dottedName.Names, position, analysis);
                module = module ?? GetModuleFromDottedName(dottedName.Names, position, eval);
                return module != null ? _docSource.GetHover(module.Name, module) : null;
            }
            // Are we over 'D'?
            var nameIndex = imp.AsNames?.ExcludeDefault().IndexOf(n => n.StartIndex <= position && position < n.EndIndex);
            if (nameIndex >= 0) {
                using (eval.OpenScope(analysis.Document, scope)) {
                    var variableName = imp.AsNames[nameIndex.Value].Name;
                    var m = eval.LookupNameInScopes(variableName, out _);
                    if (m != null) {
                        return _docSource.GetHover(variableName, m);
                    }
                }
            }
            return null;
        }

        private MarkupContent HandleFromImport(FromImportStatement fi, SourceLocation location, ScopeStatement scope, IDocumentAnalysis analysis) {
            var eval = analysis.ExpressionEvaluator;
            var position = location.ToIndex(analysis.Ast);
            // 'from A.B import C as D'
            if (fi.Root.StartIndex <= position && position < fi.Root.EndIndex) {
                // We are over A.B
                var module = GetModule(fi.Root.MakeString(), fi.Root.Names, position, analysis);
                module = module ?? GetModuleFromDottedName(fi.Root.Names, position, eval);
                return module != null ? _docSource.GetHover(module.Name, module) : null;
            }
            // Are we over 'C'?
            var nameIndex = fi.Names?.ExcludeDefault().IndexOf(n => n.StartIndex <= position && position < n.EndIndex);
            if (nameIndex >= 0) {
                var module = eval.Interpreter.ModuleResolution.GetImportedModule(fi.Root.MakeString());
                module = module ?? GetModuleFromDottedName(fi.Root.Names, -1, eval);
                if (module != null) {
                    var memberName = fi.Names[nameIndex.Value].Name;
                    var m = module.GetMember(memberName);
                    return m != null ? _docSource.GetHover(memberName, m) : null;
                }
            }
            // Are we over 'D'?
            nameIndex = fi.AsNames?.ExcludeDefault().IndexOf(n => n.StartIndex <= position && position < n.EndIndex);
            if (nameIndex >= 0) {
                using (eval.OpenScope(analysis.Document, scope)) {
                    var variableName = fi.AsNames[nameIndex.Value].Name;
                    var m = eval.LookupNameInScopes(variableName, out _);
                    return m != null ? _docSource.GetHover(variableName, m) : null;
                }
            }
            return null;
        }

        private static IPythonModule GetModule(string moduleName, IList<NameExpression> names, int position, IDocumentAnalysis analysis) {
            IPythonModule module = null;
            var eval = analysis.ExpressionEvaluator;
            var nameIndex = names.IndexOf(n => n.StartIndex <= position && position < n.EndIndex);
            if (nameIndex == 0) {
                module = eval.Interpreter.ModuleResolution.GetImportedModule(names[nameIndex].Name);
            }
            return module ?? eval.Interpreter.ModuleResolution.GetImportedModule(moduleName);
        }

        private static IPythonModule GetModuleFromDottedName(IList<NameExpression> names, int position, IExpressionEvaluator eval) {
            IPythonModule module = null;
            var index = position >= 0 ? names.IndexOf(n => n.StartIndex <= position && position <= n.EndIndex) : names.Count - 1;
            if (index >= 0) {
                module = eval.Interpreter.ModuleResolution.GetImportedModule(names[0].Name);
                for (var i = 1; module != null && i <= index; i++) {
                    module = module.GetMember(names[i].Name) as IPythonModule;
                }
            }
            return module;
        }
    }
}
