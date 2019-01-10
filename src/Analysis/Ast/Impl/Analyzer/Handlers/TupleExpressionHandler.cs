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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class TupleExpressionHandler: StatementHandler {
        public TupleExpressionHandler(AnalysisWalker walker): base(walker) { }

        public async Task HandleTupleAssignmentAsync(TupleExpression lhs, Expression rhs, IMember value, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            if (rhs is TupleExpression tex) {
                var returnedExpressions = tex.Items.ToArray();
                var names = lhs.Items.OfType<NameExpression>().Select(x => x.Name).ToArray();
                for (var i = 0; i < Math.Min(names.Length, returnedExpressions.Length); i++) {
                    if (returnedExpressions[i] != null && !string.IsNullOrEmpty(names[i])) {
                        var v = await Eval.GetValueFromExpressionAsync(returnedExpressions[i], cancellationToken);
                        if (v != null) {
                            Eval.DeclareVariable(names[i], v, returnedExpressions[i]);
                        }
                    }
                }
                return;
            }

            // Tuple = 'tuple value' (such as from callable). Transfer values.
            if (value is IPythonCollection seq) {
                var types = seq.Contents.Select(c => c.GetPythonType()).ToArray();
                var expressions = lhs.Items.OfType<NameExpression>().ToArray();
                var names = expressions.Select(x => x.Name).ToArray();
                for (var i = 0; i < Math.Min(names.Length, types.Length); i++) {
                    if (names[i] != null && types[i] != null) {
                        Eval.DeclareVariable(names[i], types[i], expressions[i]);
                    }
                }
            }
        }
    }
}
