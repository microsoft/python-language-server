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
        private readonly bool _labelOffsetSupport;

        public SignatureSource(IDocumentationSource docSource, bool labelOffsetSupport = true) {
            _docSource = docSource;
            // TODO: deprecate eventually.
            _labelOffsetSupport = labelOffsetSupport; // LSP 3.14.0+
        }

        public SignatureHelp GetSignature(IDocumentAnalysis analysis, SourceLocation location) {
            if (analysis is EmptyAnalysis) {
                return null;
            }

            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Hover, out var node, out var statement, out var scope);

            IMember value = null;
            IPythonType selfType = null;
            string name = null;
            var call = node as CallExpression;
            if (call != null) {
                using (analysis.ExpressionEvaluator.OpenScope(analysis.Document, scope)) {
                    switch (call.Target) {
                        case MemberExpression mex:
                            var v = analysis.ExpressionEvaluator.GetValueFromExpression(mex.Target);
                            selfType = v?.GetPythonType();
                            name = mex.Name;
                            break;
                        case NameExpression ne:
                            name = ne.Name;
                            break;
                    }

                    value = analysis.ExpressionEvaluator.GetValueFromExpression(call.Target);
                }
            }

            var ft = value.TryGetFunctionType();
            if (ft == null) {
                return null;
            }

            var signatures = new SignatureInformation[ft.Overloads.Count];
            for (var i = 0; i < ft.Overloads.Count; i++) {
                var o = ft.Overloads[i];

                var signatureLabel = _docSource.GetSignatureString(ft, selfType, out var parameterSpans, i, name);

                var parameterInfo = new ParameterInformation[parameterSpans.Length];
                for (var j = 0; j < parameterSpans.Length; j++) {
                    var (ps, p) = parameterSpans[j];

                    parameterInfo[j] = new ParameterInformation {
                        label = _labelOffsetSupport ? new[] { ps.Start, ps.End } : (object)p.Name,
                        documentation = _docSource.FormatParameterDocumentation(p)
                    };
                }

                signatures[i] = new SignatureInformation {
                    label = signatureLabel,
                    documentation = _docSource.FormatDocumentation(ft.Documentation),
                    parameters = parameterInfo
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
