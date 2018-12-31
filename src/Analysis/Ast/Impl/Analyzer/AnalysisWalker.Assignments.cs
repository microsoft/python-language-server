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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal partial class AnalysisWalker {
        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await Lookup.GetValueFromExpressionAsync(node.Right, cancellationToken);

            if (value.IsUnknown()) {
                Log?.Log(TraceEventType.Verbose, $"Undefined value: {node.Right.ToCodeString(Ast).Trim()}");
            }

            if (value?.GetPythonType().TypeId == BuiltinTypeId.Ellipsis) {
                value = Lookup.UnknownType;
            }

            if (node.Left.FirstOrDefault() is TupleExpression lhs) {
                // Tuple = Tuple. Transfer values.
                var texHandler = new TupleExpressionHandler(Lookup);
                await texHandler.HandleTupleAssignmentAsync(lhs, node.Right, value, cancellationToken);
            } else {
                foreach (var expr in node.Left.OfType<ExpressionWithAnnotation>()) {
                    // x: List[str] = [...]
                    AssignAnnotatedVariable(expr, value);
                    if (!value.IsUnknown() && expr.Expression is NameExpression ne) {
                        Lookup.DeclareVariable(ne.Name, value, GetLoc(ne));
                    }
                }

                foreach (var ne in node.Left.OfType<NameExpression>()) {
                    Lookup.DeclareVariable(ne.Name, value, GetLoc(ne));
                }
            }

            return await base.WalkAsync(node, cancellationToken);
        }

        public override Task<bool> WalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default) {
            AssignAnnotatedVariable(node.Expression as ExpressionWithAnnotation, null);
            return Task.FromResult(false);
        }

        private void AssignAnnotatedVariable(ExpressionWithAnnotation expr, IMember value) {
            if (expr?.Annotation != null && expr.Expression is NameExpression ne) {
                var variableType = Lookup.GetTypeFromAnnotation(expr.Annotation);
                // If value is null, then this is a pure declaration like 
                //   x: List[str]
                // without a value. If value is provided, then this is
                //   x: List[str] = [...]

                // Check value type for compatibility
                IMember instance;
                if (value != null) {
                    var valueType = value.GetPythonType();
                    if (!valueType.IsUnknown() && !variableType.IsUnknown() && !valueType.Equals(variableType)) {
                        // TODO: warn incompatible value type.
                        return;
                    }
                    instance = value;
                } else {
                    instance = variableType?.CreateInstance(Module, GetLoc(expr.Expression)) ?? Lookup.UnknownType;
                }
                Lookup.DeclareVariable(ne.Name, instance, expr.Expression);
            }
        }
    }
}
