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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class TupleExpressionHandler : StatementHandler {
        public TupleExpressionHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleTupleAssignment(TupleExpression lhs, Expression rhs, IMember value) {
            string[] names;

            if (rhs is TupleExpression tex) {
                var returnedExpressions = tex.Items.ToArray();
                names = lhs.Items.OfType<NameExpression>().Select(x => x.Name).ToArray();
                for (var i = 0; i < names.Length; i++) {
                    Expression e = null;
                    if (returnedExpressions.Length > 0) {
                        e = i < returnedExpressions.Length ? returnedExpressions[i] : returnedExpressions[returnedExpressions.Length - 1];
                    }

                    if (e != null && !string.IsNullOrEmpty(names[i])) {
                        var v = Eval.GetValueFromExpression(e);
                        Eval.DeclareVariable(names[i], v ?? Eval.UnknownType, VariableSource.Declaration, e);
                    }
                }

                return;
            }

            // Tuple = 'tuple value' (such as from callable). Transfer values.
            var expressions = lhs.Items.OfType<NameExpression>().ToArray();
            names = expressions.Select(x => x.Name).ToArray();
            if (value is IPythonCollection seq) {
                var types = seq.Contents.Select(c => c.GetPythonType()).ToArray();
                for (var i = 0; i < names.Length; i++) {
                    IPythonType t = null;
                    if (types.Length > 0) {
                        t = i < types.Length ? types[i] : types[types.Length - 1];
                    }

                    if (names[i] != null && t != null) {
                        var instance = t.CreateInstance(null, Eval.GetLoc(expressions[i]), ArgumentSet.Empty);
                        Eval.DeclareVariable(names[i], instance, VariableSource.Declaration, expressions[i]);
                    }
                }
            } else {
                foreach (var n in names.ExcludeDefault()) {
                    Eval.DeclareVariable(n, value, VariableSource.Declaration, rhs);
                }
            }
        }
    }
}
