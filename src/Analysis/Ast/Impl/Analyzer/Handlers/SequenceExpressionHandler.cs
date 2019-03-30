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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class SequenceExpressionHandler : StatementHandler {
        public SequenceExpressionHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleAssignment(IEnumerable<Expression> lhs, Expression rhs, IMember value) {
            if (rhs is TupleExpression tex) {
                Assign(lhs, tex, Eval);
            } else {
                Assign(lhs, value, Eval);
            }
        }

        internal static void Assign(IEnumerable<Expression> lhs, TupleExpression rhs, ExpressionEval eval) {
            var names = NamesFromSequenceExpression(lhs).ToArray();
            var values = ValuesFromSequenceExpression(rhs.Items, eval).ToArray();
            for (var i = 0; i < names.Length; i++) {
                IMember value = null;
                if (values.Length > 0) {
                    value = i < values.Length ? values[i] : values[values.Length - 1];
                }

                if (!string.IsNullOrEmpty(names[i]?.Name)) {
                    eval.DeclareVariable(names[i].Name, value ?? eval.UnknownType, VariableSource.Declaration, eval.Module, names[i]);
                }
            }
        }

        internal static void Assign(IEnumerable<Expression> lhs, IMember value, ExpressionEval eval) {
            // Tuple = 'tuple value' (such as from callable). Transfer values.
            IMember[] values;
            if (value is IPythonCollection seq) {
                values = seq.Contents.ToArray();
            } else {
                values = new[] { value };
            }

            var typeEnum = new ValueEnumerator(values, eval.UnknownType);
            Assign(lhs, typeEnum, eval);
        }

        private static void Assign(IEnumerable<Expression> items, ValueEnumerator valueEnum, ExpressionEval eval) {
            foreach (var item in items) {
                switch (item) {
                    case StarredExpression stx when stx.Expression is NameExpression nex && !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next, VariableSource.Declaration, eval.Module, nex);
                        break;
                    case NameExpression nex when !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next, VariableSource.Declaration, eval.Module, nex);
                        break;
                    case TupleExpression te:
                        Assign(te.Items, valueEnum, eval);
                        break;
                }
            }
        }

        private static IEnumerable<NameExpression> NamesFromSequenceExpression(IEnumerable<Expression> items) {
            var names = new List<NameExpression>();
            foreach (var item in items) {
                var expr = item.RemoveParenthesis();
                switch (expr) {
                    case SequenceExpression seq:
                        names.AddRange(NamesFromSequenceExpression(seq.Items));
                        break;
                    case NameExpression nex:
                        names.Add(nex);
                        break;
                }
            }
            return names;
        }

        private static IEnumerable<IMember> ValuesFromSequenceExpression(IEnumerable<Expression> items, ExpressionEval eval) {
            var members = new List<IMember>();
            foreach (var item in items) {
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

        private class ValueEnumerator {
            private readonly IMember[] _values;
            private readonly IMember _filler;
            private int _index;

            public ValueEnumerator(IMember[] values, IMember filler) {
                _values = values;
                _filler = filler;
            }

            public IMember Next {
                get {
                    IMember t;
                    if (_values.Length > 0) {
                        t = _index < _values.Length ? _values[_index] : _values[_values.Length - 1];
                    } else {
                        t = _filler;
                    }

                    _index++;
                    return t;
                }
            }
        }
    }
}
