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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class TupleExpressionHandler : StatementHandler {
        public TupleExpressionHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleTupleAssignment(TupleExpression lhs, Expression rhs, IMember value) {
            if (rhs is TupleExpression tex) {
                AssignTuple(lhs, tex, Eval);
            } else {
                AssignTuple(lhs, value, Eval);
            }
        }

        internal static void AssignTuple(TupleExpression lhs, TupleExpression rhs, ExpressionEval eval) {
            var returnedExpressions = rhs.Items.ToArray();
            var names = lhs.Items.OfType<NameExpression>().Select(x => x.Name).ToArray();
            for (var i = 0; i < names.Length; i++) {
                Expression e = null;
                if (returnedExpressions.Length > 0) {
                    e = i < returnedExpressions.Length ? returnedExpressions[i] : returnedExpressions[returnedExpressions.Length - 1];
                }

                if (e != null && !string.IsNullOrEmpty(names[i])) {
                    var v = eval.GetValueFromExpression(e);
                    eval.DeclareVariable(names[i], v ?? eval.UnknownType, VariableSource.Declaration, e);
                }
            }
        }

        internal static void AssignTuple(TupleExpression lhs, IMember value, ExpressionEval eval) {
            // Tuple = 'tuple value' (such as from callable). Transfer values.
            IPythonType[] types;
            if (value is IPythonCollection seq) {
                types = seq.Contents.Select(c => c.GetPythonType()).ToArray();
            } else {
                types = new[] {eval.UnknownType};
            }

            var typeEnum = new TypeEnumerator(types, eval.UnknownType);
            AssignTuple(lhs, typeEnum, eval);
        }

        private static void AssignTuple(TupleExpression tex, TypeEnumerator typeEnum, ExpressionEval eval) {
            foreach (var item in tex.Items) {
                switch (item) {
                    case NameExpression nex when !string.IsNullOrEmpty(nex.Name):
                        var instance = typeEnum.Next.CreateInstance(null, LocationInfo.Empty, ArgumentSet.Empty);
                        eval.DeclareVariable(nex.Name, instance, VariableSource.Declaration, nex);
                        break;
                    case TupleExpression te:
                        AssignTuple(te, typeEnum, eval);
                        break;
                }
            }
        }

        private class TypeEnumerator {
            private readonly IPythonType[] _types;
            private readonly IPythonType _filler;
            private int _index;

            public TypeEnumerator(IPythonType[] types, IPythonType filler) {
                _types = types;
                _filler = filler;
            }

            public IPythonType Next {
                get {
                    IPythonType t;
                    if (_types.Length > 0) {
                        t = _index < _types.Length ? _types[_index] : _types[_types.Length - 1];
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
