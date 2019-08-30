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

using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class LoopHandler : StatementHandler {
        public LoopHandler(AnalysisWalker walker) : base(walker) { }

        public bool HandleFor(ForStatement node) {
            var iterable = Eval.GetValueFromExpression(node.List);
            var iterator = (iterable as IPythonIterable)?.GetIterator();
            var value = iterator?.Next ?? Eval.UnknownType;
            switch (node.Left) {
                case NameExpression nex:
                    // for x in y:
                    if (!string.IsNullOrEmpty(nex.Name)) {
                        Eval.DeclareVariable(nex.Name, value, VariableSource.Declaration, nex);
                    }
                    break;
                case SequenceExpression seq:
                    // x = [('abc', 42, True), ('abc', 23, False)]
                    // for some_str, (some_int, some_bool) in x:
                    var h = new SequenceExpressionHandler(Walker);
                    h.HandleAssignment(seq, node.List, value);
                    break;
            }

            node.Body?.Walk(Walker);
            node.Else?.Walk(Walker);
            return false;
        }

        public void HandleWhile(WhileStatement node) {
            node.Body?.Walk(Walker);
            node.ElseStatement?.Walk(Walker);
        }
    }
}
