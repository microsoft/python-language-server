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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
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
        public async Task<Reference> FindDefinitionAsync(IDocumentAnalysis analysis, SourceLocation location, CancellationToken cancellationToken = default) {
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
                var value = await eval.GetValueFromExpressionAsync(expr, cancellationToken);
                return await FromMemberAsync(value, expr, eval, cancellationToken);
            }
        }

        private async Task<Reference> FromMemberAsync(IMember value, Expression expr, IExpressionEvaluator eval, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            Node node = null;
            IPythonModule module = null;

            switch (value) {
                case IPythonClassType cls:
                    node = cls.ClassDefinition;
                    module = cls.DeclaringModule;
                    break;
                case IPythonFunctionType fn:
                    node = fn.FunctionDefinition;
                    module = fn.DeclaringModule;
                    break;
                case IPythonPropertyType prop:
                    node = prop.FunctionDefinition;
                    module = prop.DeclaringModule;
                    break;
                case IPythonModule mod: {
                        var member = eval.LookupNameInScopes(mod.Name, out var scope);
                        if (member != null && scope != null) {
                            var v = scope.Variables[mod.Name];
                            if (v != null) {
                                return new Reference { range = v.Location.Span, uri = v.Location.DocumentUri };
                            }
                        }
                        break;
                    }
                case IPythonInstance instance when instance.Type is IPythonFunctionType ft: {
                        node = ft.FunctionDefinition;
                        module = ft.DeclaringModule;
                        break;
                    }
                case IPythonInstance _ when expr is NameExpression nex: {
                        var member = eval.LookupNameInScopes(nex.Name, out var scope);
                        if (member != null && scope != null) {
                            var v = scope.Variables[nex.Name];
                            if (v != null) {
                                return new Reference { range = v.Location.Span, uri = v.Location.DocumentUri };
                            }
                        }
                        break;
                    }
                case IPythonInstance _ when expr is MemberExpression mex: {
                        var target = await eval.GetValueFromExpressionAsync(mex.Target, cancellationToken);
                        var type = target?.GetPythonType();
                        var member = type?.GetMember(mex.Name);
                        if (member is IPythonInstance v) {
                            return new Reference { range = v.Location.Span, uri = v.Location.DocumentUri };
                        }
                        return await FromMemberAsync(member, null, eval, cancellationToken);
                    }
            }

            if (node != null && CanNavigateToModule(module) && module is IDocument doc) {
                return new Reference {
                    range = node.GetSpan(doc.GetAnyAst()), uri = doc.Uri
                };
            }

            return null;
        }

        private static bool CanNavigateToModule(IPythonModule m) {
#if DEBUG
            // Allow navigation anywhere in debug.
            return m.ModuleType != ModuleType.Specialized && m.ModuleType != ModuleType.Unresolved; 
#else
            return m.ModuleType == ModuleType.User || m.ModuleType == ModuleType.Package || m.ModuleType == ModuleType.Library;
#endif
        }
    }
}
