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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class SignatureSource {
        private readonly IDocumentationSource _docSource;

        public SignatureSource(IDocumentationSource docSource) {
            _docSource = docSource;
        }

        public SignatureHelp GetSignature(IDocumentAnalysis analysis, SourceLocation location) {
            if (analysis is EmptyAnalysis) {
                return null;
            }

            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Hover, out var node, out var statement, out var scope);

            IMember value = null;
            IPythonType selfType = null;
            var call = node as CallExpression;
            if (call != null) {
                using (analysis.ExpressionEvaluator.OpenScope(analysis.Document, scope)) {
                    if (call.Target is MemberExpression mex) {
                        var v = analysis.ExpressionEvaluator.GetValueFromExpression(mex.Target);
                        selfType = v?.GetPythonType();
                    }
                    value = analysis.ExpressionEvaluator.GetValueFromExpression(call.Target);
                }
            }

            var ft = value?.GetPythonType<IPythonFunctionType>();
            if (ft == null) {
                return null;
            }

            var skip = ft.IsStatic || ft.IsUnbound ? 0 : 1;

            var signatures = new SignatureInformation[ft.Overloads.Count];
            for (var i = 0; i < ft.Overloads.Count; i++) {
                var o = ft.Overloads[i];

                var parameters = o.Parameters.Skip(skip).Select(p => new ParameterInformation {
                    label = p.Name,
                    documentation = _docSource.FormatParameterDocumentation(p)
                }).ToArray();

                signatures[i] = new SignatureInformation {
                    label = _docSource.GetSignatureString(ft, selfType, i),
                    documentation = _docSource.FormatDocumentation(ft.Documentation),
                    parameters = parameters
                };
            }

            var index = location.ToIndex(analysis.Ast);
            if (call.GetArgumentAtIndex(analysis.Ast, index, out var activeParameter) && activeParameter < 0) {
                // Returned 'true' and activeParameter == -1 means that we are after 
                // the trailing comma, so assume partially typed expression such as 'pow(x, y, |)
                activeParameter = call.Args.Count;
            }

            var activeSignature = -1;
            if (activeParameter >= 0) {
                // TODO: Better selection of active signature by argument set
                activeSignature = signatures
                    .Select((s, i) => Tuple.Create(s, i))
                    .OrderBy(t => t.Item1.parameters.Length)
                    .FirstOrDefault(t => t.Item1.parameters.Length > activeParameter)
                    ?.Item2 ?? -1;
            }

            activeSignature = activeSignature >= 0
                ? activeSignature
                : (signatures.Length > 0 ? 0 : -1);

            return new SignatureHelp {
                signatures = signatures.ToArray(),
                activeSignature = activeSignature,
                activeParameter = activeParameter
            };
        }
    }
}
