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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class LoopHandler : StatementHandler {
        public LoopHandler(AnalysisWalker walker) : base(walker) { }

        public async Task HandleForAsync(ForStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            var iterable = await Eval.GetValueFromExpressionAsync(node.List, cancellationToken);
            var iterator = (iterable as IPythonIterable)?.GetIterator();
            if (iterator == null) {
                // TODO: report that expression does not evaluate to iterable.
            }
            var value = iterator?.Next ?? Eval.UnknownType;

            switch (node.Left) {
                case NameExpression nex:
                    // for x in y:
                    Eval.DeclareVariable(nex.Name, value, VariableSource.Declaration, Eval.GetLoc(node.Left));
                    break;
                case TupleExpression tex:
                    // x = [('abc', 42, True), ('abc', 23, False)]
                    // for some_str, some_int, some_bool in x:
                    var names = tex.Items.OfType<NameExpression>().Select(x => x.Name).ToArray();
                    if (value is IPythonIterable valueIterable) {
                        var valueIterator = valueIterable.GetIterator();
                        foreach (var n in names) {
                            Eval.DeclareVariable(n, valueIterator?.Next ?? Eval.UnknownType, VariableSource.Declaration, Eval.GetLoc(node.Left));
                        }
                    } else {
                        // TODO: report that expression yields value that does not evaluate to iterable.
                    }
                    break;
            }

            if (node.Body != null) {
                await node.Body.WalkAsync(Walker, cancellationToken);
            }
        }

        public async Task HandleWhileAsync(WhileStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Body != null) {
                await node.Body.WalkAsync(Walker, cancellationToken);
            }
            if (node.ElseStatement != null) {
                await node.ElseStatement.WalkAsync(Walker, cancellationToken);
            }
        }
    }
}
