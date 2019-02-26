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

using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class DefinitionSource {
        public Reference FindDefinition(IDocumentAnalysis analysis, SourceLocation location) {
            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Hover, out var exprNode, out var statement, out var exprScope);

            if (exprNode is ConstantExpression) {
                return null; // No hover for literals.
            }
            if (!(exprNode is Expression expr)) {
                return null;
            }

            var eval = analysis.ExpressionEvaluator;
            using (eval.OpenScope(analysis.Document, exprScope)) {

                // First try variables, except in imports
                if (expr is NameExpression nex && !string.IsNullOrEmpty(nex.Name) &&
                    !(statement is ImportStatement) && !(statement is FromImportStatement)) {
                    var m = eval.LookupNameInScopes(nex.Name, out var scope);
                    if (m != null && scope.Variables[nex.Name] is IVariable v) {
                        var type = v.Value.GetPythonType();
                        var module = type as IPythonModule ?? type?.DeclaringModule;
                        if (CanNavigateToModule(module, analysis)) {
                            return new Reference { range = v.Location.Span, uri = v.Location.DocumentUri };
                        }
                    }
                }

                var value = eval.GetValueFromExpression(expr);
                return FromMember(value, expr, statement, analysis);
            }
        }

        private Reference FromMember(IMember value, Expression expr, Node statement, IDocumentAnalysis analysis) {
            Node node = null;
            IPythonModule module = null;
            LocationInfo location = null;
            var eval = analysis.ExpressionEvaluator;

            switch (value) {
                case IPythonClassType cls:
                    node = cls.ClassDefinition;
                    module = cls.DeclaringModule;
                    location = cls.Location;
                    break;
                case IPythonFunctionType fn:
                    node = fn.FunctionDefinition;
                    module = fn.DeclaringModule;
                    location = fn.Location;
                    break;
                case IPythonPropertyType prop:
                    node = prop.FunctionDefinition;
                    module = prop.DeclaringModule;
                    location = prop.Location;
                    break;
                case IPythonModule mod:
                    return HandleModule(mod, analysis, statement);
                case IPythonInstance instance when instance.Type is IPythonFunctionType ft:
                    node = ft.FunctionDefinition;
                    module = ft.DeclaringModule;
                    location = ft.Location;
                    break;
                case IPythonInstance instance when instance.Type is IPythonFunctionType ft:
                    node = ft.FunctionDefinition;
                    module = ft.DeclaringModule;
                    break;
                case IPythonInstance _ when expr is NameExpression nex:
                    var m1 = eval.LookupNameInScopes(nex.Name, out var scope);
                    if (m1 != null && scope != null) {
                        var v = scope.Variables[nex.Name];
                        if (v != null) {
                            return new Reference { range = v.Location.Span, uri = v.Location.DocumentUri };
                        }
                    }
                    break;
                case IPythonInstance _ when expr is MemberExpression mex: {
                        var target = eval.GetValueFromExpression(mex.Target);
                        var type = target?.GetPythonType();
                        var m2 = type?.GetMember(mex.Name);
                        if (m2 is IPythonInstance v) {
                            return new Reference { range = v.Location.Span, uri = v.Location.DocumentUri };
                        }
                        return FromMember(m2, null, statement, analysis);
                    }
            }

            module = module?.ModuleType == ModuleType.Stub ? module.PrimaryModule : module;
            if (node != null && module is IDocument doc && CanNavigateToModule(module, analysis)) {
                return new Reference {
                    range = location?.Span ?? node.GetSpan(doc.GetAnyAst()), uri = doc.Uri
                };
            }

            return null;
        }

        private static Reference HandleModule(IPythonModule module, IDocumentAnalysis analysis, Node statement) {
            // If we are in import statement, open the module source if available.
            if (statement is ImportStatement || statement is FromImportStatement) {
                if (module.Uri != null && CanNavigateToModule(module, analysis)) {
                    return new Reference { range = default, uri = module.Uri };
                }
            }
            return null;
        }

        private static bool CanNavigateToModule(IPythonModule m, IDocumentAnalysis analysis) {
            if (m == null) {
                return false;
            }
            var canNavigate = m.ModuleType == ModuleType.User || m.ModuleType == ModuleType.Package || m.ModuleType == ModuleType.Library;
#if DEBUG
            // Allow navigation anywhere in debug.
            canNavigate |= m.ModuleType == ModuleType.Stub || m.ModuleType == ModuleType.Compiled;
#endif
            return canNavigate;
        }
    }
}
