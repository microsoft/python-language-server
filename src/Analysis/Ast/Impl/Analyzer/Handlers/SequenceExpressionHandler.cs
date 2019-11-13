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

using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class SequenceExpressionHandler : StatementHandler {
        public SequenceExpressionHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleAssignment(SequenceExpression seq, IMember value) => Assign(seq, value, Eval);

        internal static void Assign(SequenceExpression seq, IMember value, ExpressionEval eval) {
            var typeEnum = new ValueEnumerator(value, eval.UnknownType, eval.Module);
            // Fetch actual tuple from the list for assignment item by item
            // rather than assigning tuples from the list to each item.
            // x, y, z = [('abc', 42, True), ('abc', 23, False)]
            // yields x = 'abc', y = 42 rather than x = ('abc', 42, True), ...
            if (seq is TupleExpression && typeEnum.Peek is IPythonCollection coll) {
                typeEnum = new ValueEnumerator(coll, eval.UnknownType, eval.Module);
            }
            Assign(seq, typeEnum, eval);
        }

        private static void Assign(SequenceExpression seq, ValueEnumerator valueEnum, ExpressionEval eval) {
            foreach (var item in seq.Items) {
                switch (item) {
                    case StarredExpression stx when stx.Expression is NameExpression nex && !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next(), VariableSource.Declaration, nex);
                        break;
                    case ParenthesisExpression pex when pex.Expression is NameExpression nex && !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next(), VariableSource.Declaration, nex);
                        break;
                    case NameExpression nex when !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next(), VariableSource.Declaration, nex);
                        break;
                    // Nested sequence expression in sequence, Tuple[Tuple[int, str], int], List[Tuple[int], str]
                    // TODO: Because of bug with how collection types are constructed, they don't make nested collection types
                    // into instances, meaning we have to create it here
                    case SequenceExpression se when valueEnum.Peek is IPythonCollection || valueEnum.Peek is IPythonCollectionType:
                        var collection = valueEnum.Next();
                        var pc = collection as IPythonCollection;
                        var pct = collection as IPythonCollectionType;
                        Assign(se, pc ?? pct.CreateInstance(ArgumentSet.Empty(se, eval)), eval);
                        break;
                    case SequenceExpression se:
                        Assign(se, valueEnum, eval);
                        break;
                }
            }
        }
    }
}
