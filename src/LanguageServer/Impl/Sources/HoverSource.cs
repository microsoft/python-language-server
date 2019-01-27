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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
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

            if (node is ConstantExpression) {
                return null; // No hover for literals.
            }

            if (node is Expression expr) {
                using (analysis.ExpressionEvaluator.OpenScope(scope)) {
                    var value = await analysis.ExpressionEvaluator.GetValueFromExpressionAsync(expr, cancellationToken);
                    var type = value?.GetPythonType();
                    if (type != null) {
                        var range = new Range {
                            start = expr.GetStart(analysis.Ast),
                            end = expr.GetEnd(analysis.Ast),
                        };

                        // Figure out name, if any
                        var name = (expr as MemberExpression)?.Name;
                        name = name ?? (node as NameExpression)?.Name;
                        name = statement is ClassDefinition || statement is FunctionDefinition ? null : name;

                        return new Hover {
                            contents = _docSource.GetHover(name, value),
                            range = range
                        };
                    }
                }
            }
            return null;
        }
    }
}
