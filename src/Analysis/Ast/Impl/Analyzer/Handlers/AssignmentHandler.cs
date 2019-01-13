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

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class AssignmentHandler : StatementHandler {
        public AssignmentHandler(AnalysisWalker walker) : base(walker) { }

        public async Task HandleAssignmentAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await Eval.GetValueFromExpressionAsync(node.Right, cancellationToken);

            if (value.IsUnknown()) {
                Log?.Log(TraceEventType.Verbose, $"Undefined value: {node.Right.ToCodeString(Ast).Trim()}");
            }

            if (value?.GetPythonType().TypeId == BuiltinTypeId.Ellipsis) {
                value = Eval.UnknownType;
            }

            if (node.Left.FirstOrDefault() is TupleExpression lhs) {
                // Tuple = Tuple. Transfer values.
                var texHandler = new TupleExpressionHandler(Walker);
                await texHandler.HandleTupleAssignmentAsync(lhs, node.Right, value, cancellationToken);
                return;
            }
            foreach (var expr in node.Left.OfType<ExpressionWithAnnotation>()) {
                // x: List[str] = [...]
                await HandleAnnotatedExpressionAsync(expr, value, cancellationToken);
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

                Eval.DeclareVariable(ne.Name, value, Eval.GetLoc(ne));
            }
        }

        public async Task HandleAnnotatedExpressionAsync(ExpressionWithAnnotation expr, IMember value, CancellationToken cancellationToken = default) {
            if (expr?.Annotation == null) {
                return;
            }

            var variableType = await Eval.GetTypeFromAnnotationAsync(expr.Annotation, cancellationToken);
            // If value is null, then this is a pure declaration like 
            //   x: List[str]
            // without a value. If value is provided, then this is
            //   x: List[str] = [...]

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
            instance = instance ?? variableType?.CreateInstance(variableType.Name, Eval.GetLoc(expr.Expression), Array.Empty<IMember>()) ?? Eval.UnknownType;

            if (expr.Expression is NameExpression ne) {
                Eval.DeclareVariable(ne.Name, instance, expr.Expression);
                return;
            }

            if (expr.Expression is MemberExpression m) {
                // self.x : int = 42
                var self = Eval.LookupNameInScopes("self", out var scope);
                var argType = self?.GetPythonType<IFunctionArgumentType>();
                if (argType?.ParameterType is PythonClassType cls && scope != null) {
                    var selfCandidateType = (await Eval.GetValueFromExpressionAsync(m.Target, cancellationToken))?.GetPythonType();
                    if (selfCandidateType is IFunctionArgumentType fa && cls.Equals(fa.ParameterType)) {
                        cls.AddMember(m.Name, instance, true);
                    }
                }
            }
        }
    }
}
