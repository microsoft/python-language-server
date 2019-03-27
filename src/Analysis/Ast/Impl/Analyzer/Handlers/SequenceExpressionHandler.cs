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
            var returnedExpressions = rhs.Items.ToArray();
            var names = lhs.OfType<NameExpression>().Select(x => x.Name).ToArray();
            for (var i = 0; i < names.Length; i++) {
                Expression e = null;
                if (returnedExpressions.Length > 0) {
                    e = i < returnedExpressions.Length ? returnedExpressions[i] : returnedExpressions[returnedExpressions.Length - 1];
                }

                if (e != null && !string.IsNullOrEmpty(names[i])) {
                    var v = eval.GetValueFromExpression(e);
                    eval.DeclareVariable(names[i], v ?? eval.UnknownType, VariableSource.Declaration, eval.Module, e);
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
                    case NameExpression nex when !string.IsNullOrEmpty(nex.Name):
                        eval.DeclareVariable(nex.Name, valueEnum.Next, VariableSource.Declaration, eval.Module, nex);
                        break;
                    case TupleExpression te:
                        Assign(te.Items, valueEnum, eval);
                        break;
                }
            }
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
