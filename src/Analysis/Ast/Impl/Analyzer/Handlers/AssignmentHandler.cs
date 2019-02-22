﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class AssignmentHandler : StatementHandler {
        public AssignmentHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleAssignment(AssignmentStatement node) {
            if (node.Right is ErrorExpression) {
                return;
            }

            var value = Eval.GetValueFromExpression(node.Right) ?? Eval.UnknownType;
            // Check PEP hint first
            var valueType = Eval.GetTypeFromPepHint(node.Right);
            if (valueType != null) {
                HandleTypedVariable(valueType, value, node.Left.FirstOrDefault());
                return;
            }

            if (value.IsUnknown()) {
                Log?.Log(TraceEventType.Verbose, $"Undefined value: {node.Right.ToCodeString(Ast).Trim()}");
            }
            if (value?.GetPythonType().TypeId == BuiltinTypeId.Ellipsis) {
                value = Eval.UnknownType;
            }

            if (node.Left.FirstOrDefault() is TupleExpression lhs) {
                // Tuple = Tuple. Transfer values.
                var texHandler = new TupleExpressionHandler(Walker);
                texHandler.HandleTupleAssignment(lhs, node.Right, value);
                return;
            }

            // Process annotations, if any.
            foreach (var expr in node.Left.OfType<ExpressionWithAnnotation>()) {
                // x: List[str] = [...]
                HandleAnnotatedExpression(expr, value);
            }

            foreach (var ne in node.Left.OfType<NameExpression>()) {
                if (Eval.CurrentScope.NonLocals[ne.Name] != null) {
                    Eval.LookupNameInScopes(ne.Name, out var scope, LookupOptions.Nonlocal);
                    if (scope != null) {
                        scope.Variables[ne.Name].Value = value;
                    } else {
                        // TODO: report variable is not declared in outer scopes.
                    }
                    continue;
                }

                if (Eval.CurrentScope.Globals[ne.Name] != null) {
                    Eval.LookupNameInScopes(ne.Name, out var scope, LookupOptions.Global);
                    if (scope != null) {
                        scope.Variables[ne.Name].Value = value;
                    } else {
                        // TODO: report variable is not declared in global scope.
                    }
                    continue;
                }

                var source = value.IsGeneric() ? VariableSource.Generic : VariableSource.Declaration;
                Eval.DeclareVariable(ne.Name, value ?? Module.Interpreter.UnknownType, source, Eval.GetLoc(ne));
            }
        }

        public void HandleAnnotatedExpression(ExpressionWithAnnotation expr, IMember value) {
            if (expr?.Annotation == null) {
                return;
            }

            var variableType = Eval.GetTypeFromAnnotation(expr.Annotation);
            // If value is null, then this is a pure declaration like 
            //   x: List[str]
            // without a value. If value is provided, then this is
            //   x: List[str] = [...]
            HandleTypedVariable(variableType, value, expr.Expression);
        }

        private void HandleTypedVariable(IPythonType variableType, IMember value, Expression expr) {
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
            instance = instance ?? variableType?.CreateInstance(variableType.Name, Eval.GetLoc(expr), ArgumentSet.Empty) ?? Eval.UnknownType;

            if (expr is NameExpression ne) {
                Eval.DeclareVariable(ne.Name, instance, VariableSource.Declaration, expr);
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
    }
}
