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

using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class AssignmentHandler : StatementHandler {
        public AssignmentHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleAssignment(AssignmentStatement node, AssignmentAction assignmentAction = null) {
            if (node.Right is ErrorExpression) {
                return;
            }

            // Get value of the right side of the assignment.
            var value = Eval.GetValueFromExpression(node.Right) ?? Eval.UnknownType;
            // Check PEP hint first
            var valueType = Eval.GetTypeFromPepHint(node.Right);
            if (valueType != null) {
                HandleTypedVariable(valueType, value, node.Left.FirstOrDefault(), assignmentAction);
                return;
            }

            if (value.IsUnknown()) {
                Log?.Log(TraceEventType.Verbose, $"Undefined value: {node.Right.ToCodeString(Ast).Trim()}");
            }

            if (value?.GetPythonType().TypeId == BuiltinTypeId.Ellipsis) {
                value = Eval.UnknownType;
            }

            if (node.Left.FirstOrDefault() is SequenceExpression seq) {
                // Tuple = Tuple. Transfer values.
                var seqHandler = new SequenceExpressionHandler(Walker);
                seqHandler.HandleAssignment(seq.Items, node.Right, value);
                return;
            }

            if (TryHandleClassVariable(node, value)) {
                return;
            }

            // Process annotations, if any.
            foreach (var expr in node.Left.OfType<ExpressionWithAnnotation>()) {
                // x: List[str] = [...]
                HandleAnnotatedExpression(expr, value, assignmentAction);
            }

            foreach (var ne in node.Left.OfType<NameExpression>()) {
                if (Eval.CurrentScope.NonLocals[ne.Name] != null) {
                    Eval.LookupNameInScopes(ne.Name, out var scope, LookupOptions.Nonlocal);
                    scope?.Variables[ne.Name].Assign(value, Eval.GetLocationOfName(ne));
                    continue;
                }

                if (Eval.CurrentScope.Globals[ne.Name] != null) {
                    Eval.LookupNameInScopes(ne.Name, out var scope, LookupOptions.Global);
                    scope?.Variables[ne.Name].Assign(value, Eval.GetLocationOfName(ne));
                    continue;
                }

                var source = value.IsGeneric() ? VariableSource.Generic : VariableSource.Declaration;
                Eval.AssignVariable(ne, value ?? Module.Interpreter.UnknownType, source, assignmentAction);
            }
        }

        public void HandleAnnotatedExpression(ExpressionWithAnnotation expr, IMember value, AssignmentAction assignmentAction = null) {
            if (expr?.Annotation == null) {
                return;
            }

            var variableType = Eval.GetTypeFromAnnotation(expr.Annotation);
            // If value is null, then this is a pure declaration like 
            //   x: List[str]
            // without a value. If value is provided, then this is
            //   x: List[str] = [...]
            HandleTypedVariable(variableType, value, expr.Expression, assignmentAction);
        }

        private bool TryHandleClassVariable(AssignmentStatement node, IMember value) {
            var mex = node.Left.OfType<MemberExpression>().FirstOrDefault();
            if (!string.IsNullOrEmpty(mex?.Name) && mex.Target is NameExpression nex && nex.Name.EqualsOrdinal("self")) {
                var m = Eval.LookupNameInScopes(nex.Name, out _, LookupOptions.Local);
                m.GetPythonType<PythonClassType>()?.AddMember(mex.Name, value, overwrite: true);
                return true;
            }
            return false;
        }

        private void HandleTypedVariable(IPythonType variableType, IMember value, Expression expr, AssignmentAction assignmentAction) {
            var instance = CreateInstance(variableType, value, expr);

            if (expr is NameExpression ne) {
                Eval.AssignVariable(ne, instance, VariableSource.Declaration, assignmentAction);
                return;
            }

            if (expr is MemberExpression m) {
                // self.x : int = 42
                var self = Eval.LookupNameInScopes("self", out var scope);
                var argType = self?.GetPythonType();
                if (argType is PythonClassType cls && scope != null) {
                    cls.AddMember(m.Name, instance, true);
                }
            }
        }

        private IMember CreateInstance(IPythonType variableType, IMember value, Expression expr) {
            // Check value type for compatibility
            IMember instance = null;
            if (value != null) {
                var valueType = value.GetPythonType();
                if (!variableType.IsUnknown() && !valueType.Equals(variableType)) {
                    // TODO: warn incompatible value type.
                    // TODO: verify values. Value may be list() while variable type is List[str].
                    // Leave it as variable type.
                } else {
                    instance = value;
                }
            }
            return instance ?? variableType?.CreateInstance(variableType.Name, ArgumentSet.Empty(expr, Eval)) ?? Eval.UnknownType;
        }
    }
}
