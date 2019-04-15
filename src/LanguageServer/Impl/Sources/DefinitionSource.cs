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

using System;
using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class DefinitionSource {
        private readonly IServiceContainer _services;

        public DefinitionSource(IServiceContainer services) {
            _services = services;
        }

        public Reference FindDefinition(IDocumentAnalysis analysis, SourceLocation location, out ILocatedMember member) {
            member = null;
            if (analysis?.Ast == null) {
                return null;
            }

            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Hover, out var exprNode, out var statement, out var exprScope);

            if (exprNode is ConstantExpression || !(exprNode is Expression expr)) {
                return null; // No hover for literals.
            }

            var eval = analysis.ExpressionEvaluator;
            using (eval.OpenScope(analysis.Document, exprScope)) {
                if (expr is MemberExpression mex) {
                    return FromMemberExpression(mex, analysis, out member);
                }

                // Try variables
                var name = (expr as NameExpression)?.Name;
                IMember value = null;
                if (!string.IsNullOrEmpty(name)) {
                    var reference = TryFromVariable(name, analysis, location, statement, out member);
                    if (reference != null) {
                        return reference;
                    }

                    if (statement is ImportStatement || statement is FromImportStatement) {
                        reference = TryFromImport(statement, name, analysis, out value);
                        if (reference != null) {
                            member = value as ILocatedMember;
                            return reference;
                        }
                    }
                }

                value = value ?? eval.GetValueFromExpression(expr);
                if (value.IsUnknown()) {
                    return null;
                }
                member = value as ILocatedMember;
                return FromMember(value);
            }
        }

        private Reference TryFromVariable(string name, IDocumentAnalysis analysis, SourceLocation location, Node statement, out ILocatedMember member) {
            member = null;

            var m = analysis.ExpressionEvaluator.LookupNameInScopes(name, out var scope);
            if (m != null && scope.Variables[name] is IVariable v) {
                member = v;
                var definition = v.Definition;
                if (statement is ImportStatement || statement is FromImportStatement) {
                    // If we are on the variable definition in this module,
                    // then goto definition should go to the parent, if any.
                    var indexSpan = v.Definition.Span.ToIndexSpan(analysis.Ast);
                    var index = location.ToIndex(analysis.Ast);
                    if (indexSpan.Start <= index && index < indexSpan.End) {
                        if (v.Parent == null) {
                            return null;
                        }
                        definition = v.Parent.Definition;
                    }
                }

                if (CanNavigateToModule(definition.DocumentUri)) {
                    return new Reference { range = definition.Span, uri = definition.DocumentUri };
                }
            }
            return null;
        }

        private Reference FromMemberExpression(MemberExpression mex, IDocumentAnalysis analysis, out ILocatedMember member) {
            member = null;

            var eval = analysis.ExpressionEvaluator;
            var target = eval.GetValueFromExpression(mex.Target);
            var type = target?.GetPythonType();

            switch (type) {
                case IPythonModule m when m.Analysis.GlobalScope != null:
                    // Module GetMember returns module variable value while we
                    // want the variable itself since we want to know its location.
                    var v1 = m.Analysis.GlobalScope.Variables[mex.Name];
                    if (v1 != null) {
                        member = v1;
                        return FromMember(v1);
                    }
                    break;

                case IPythonClassType cls:
                    // Data members may be instances that are not tracking locations.
                    // In this case we'll try look up the respective variable instead.
                    using (eval.OpenScope(analysis.Document, cls.ClassDefinition)) {
                        eval.LookupNameInScopes(mex.Name, out _, out var v2, LookupOptions.Local);
                        if (v2 != null) {
                            member = v2;
                            return FromMember(v2);
                        }
                    }
                    break;

                default:
                    if (type?.GetMember(mex.Name) is ILocatedMember lm) {
                        member = lm;
                        return FromMember(lm);
                    }

                    break;
            }
            return null;
        }

        private static Reference TryFromImport(Node statement, string name, IDocumentAnalysis analysis, out IMember value) {
            value = null;
            string moduleName = null;
            switch (statement) {
                // In 'import A as B' A is not declared as a variable, so try locating B.
                case ImportStatement imp when imp.Names.Any(x => x?.MakeString() == name):
                case FromImportStatement fimp when fimp.Root.Names.Any(x => x?.Name == name):
                    moduleName = name;
                    break;
            }

            if (moduleName != null) {
                var module = analysis.Document.Interpreter.ModuleResolution.GetImportedModule(moduleName);
                if (module != null) {
                    value = module;
                    return CanNavigateToModule(module) ? new Reference { range = default, uri = module.Uri } : null;
                }
            }

            // Perhaps it is a member such as A in 'from X import A as B'
            switch (statement) {
                case ImportStatement imp: {
                        // Import A as B
                        var index = imp.Names.IndexOf(x => x?.MakeString() == name);
                        if (index >= 0 && index < imp.AsNames.Count) {
                            value = analysis.ExpressionEvaluator.GetValueFromExpression(imp.AsNames[index]);
                            return null;
                        }
                        break;
                    }
                case FromImportStatement fimp: {
                        // From X import A as B
                        var index = fimp.Names.IndexOf(x => x?.Name == name);
                        if (index >= 0 && index < fimp.AsNames.Count) {
                            value = analysis.ExpressionEvaluator.GetValueFromExpression(fimp.AsNames[index]);
                            return null;
                        }
                        break;
                    }
            }
            return null;
        }


        private Reference FromMember(IMember m) {
            var definition = (m as ILocatedMember)?.Definition;
            var moduleUri = definition?.DocumentUri;
            // Make sure module we are looking for is not a stub
            if (m is IPythonType t) {
                moduleUri = t.DeclaringModule.ModuleType == ModuleType.Stub
                    ? t.DeclaringModule.PrimaryModule.Uri
                    : t.DeclaringModule.Uri;
            }
            if (definition != null && CanNavigateToModule(moduleUri)) {
                return new Reference { range = definition.Span, uri = moduleUri };
            }
            return null;
        }

        private bool CanNavigateToModule(Uri uri) {
            if (uri == null) {
                return false;
            }
            var rdt = _services.GetService<IRunningDocumentTable>();
            var doc = rdt.GetDocument(uri);
            return CanNavigateToModule(doc);
        }

        private static bool CanNavigateToModule(IPythonModule m) {
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
