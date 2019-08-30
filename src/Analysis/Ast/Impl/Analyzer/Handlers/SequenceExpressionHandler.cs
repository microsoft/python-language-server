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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class SequenceExpressionHandler : StatementHandler {
        public SequenceExpressionHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleAssignment(SequenceExpression seq, Expression rhs, IMember value) {
            if (rhs is TupleExpression tex) {
                Assign(seq, tex, Eval);
            } else {
                Assign(seq, value, Eval);
            }
        }

        internal static void Assign(SequenceExpression lhs, TupleExpression rhs, ExpressionEval eval) {
            var names = NamesFromSequenceExpression(lhs).ToArray();
            var values = ValuesFromSequenceExpression(rhs, eval).ToArray();
            for (var i = 0; i < names.Length; i++) {
                IMember value = null;
                if (values.Length > 0) {
                    value = i < values.Length ? values[i] : values[values.Length - 1];
                }

                if (!string.IsNullOrEmpty(names[i]?.Name)) {
                    eval.DeclareVariable(names[i].Name, value ?? eval.UnknownType, VariableSource.Declaration, names[i]);
                }
            }
        }

        internal static void Assign(SequenceExpression seq, IMember value, ExpressionEval eval) {
            var typeEnum = new ValueEnumerator(value, eval.UnknownType);
            Assign(seq, typeEnum, eval);
        }

        private static void Assign(SequenceExpression seq, ValueEnumerator valueEnum, ExpressionEval eval) {
            foreach (var item in seq.Items) {
                switch (item) {
                    case StarredExpression stx when stx.Expression is NameExpression nex && !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next, VariableSource.Declaration, nex);
                        break;
                    case ParenthesisExpression pex when pex.Expression is NameExpression nex && !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next, VariableSource.Declaration, nex);
                        break;
                    case NameExpression nex when !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next, VariableSource.Declaration, nex);
                        break;
                    case SequenceExpression se:
                        Assign(se, valueEnum, eval);
                        break;
                }
            }
        }

        private static IEnumerable<NameExpression> NamesFromSequenceExpression(SequenceExpression rootSeq) {
            var names = new List<NameExpression>();
            foreach (var item in rootSeq.Items) {
                var expr = item.RemoveParenthesis();
                switch (expr) {
                    case SequenceExpression seq:
                        names.AddRange(NamesFromSequenceExpression(seq));
                        break;
                    case NameExpression nex:
                        names.Add(nex);
                        break;
                }
            }
            return names;
        }

        private static IEnumerable<IMember> ValuesFromSequenceExpression(SequenceExpression seq, ExpressionEval eval) {
            var members = new List<IMember>();
            foreach (var item in seq.Items) {
                var value = eval.GetValueFromExpression(item);
                switch (value) {
                    case IPythonCollection coll:
                        members.AddRange(coll.Contents);
                        break;
                    default:
                        members.Add(value);
                        break;
                }
            }
            return members;
        }
    }
}
