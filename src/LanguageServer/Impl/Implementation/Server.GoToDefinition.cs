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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public async Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Goto Definition in {uri} at {@params.position}");

            var analysis = GetAnalysis(uri, cancellationToken);
            var reference = await FindVariableAsync(analysis, @params.position, cancellationToken);
            return reference != null ? new[] { reference } : Array.Empty<Reference>();
        }

        private async Task<Reference> FindVariableAsync(IDocumentAnalysis analysis, SourceLocation location, CancellationToken cancellationToken) {
            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Hover, out var exprNode, out var statement, out var exprScope);

            if (exprNode is ConstantExpression) {
                return null; // No hover for literals.
            }
            if (!(exprNode is Expression expr)) {
                return null;
            }


            var eval = analysis.ExpressionEvaluator;
            IMember value;
            using (eval.OpenScope(exprScope)) {
                value = await eval.GetValueFromExpressionAsync(expr, cancellationToken);
            }

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
            }

            if (node != null && module is IDocument doc) {
                return new Reference { range = node.GetSpan(doc.GetAnyAst()), uri = doc.Uri };
            }

            return null;
        }
    }
}
