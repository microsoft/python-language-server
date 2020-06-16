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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class AssignmentHandler : StatementHandler {
        public AssignmentHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleAssignment(AssignmentStatement node, LookupOptions lookupOptions = LookupOptions.Normal) {
            if (node.Right is ErrorExpression) {
                return;
            }

            // Filter out parenthesis expression in assignment because it makes no difference.
            var lhs = node.Left.Select(s => s.RemoveParenthesis());

            // Note that this is handling assignments of the same value to multiple variables,
            // i.e. with "x = y = z = value", x/y/z are the items in lhs. If an expression looks
            // like "x, y, z = value", then "x, y, z" is a *single* lhs value and its unpacking
            // will be handled by AssignToExpr.
            var value = ExtractRhs(node.Right, lhs.FirstOrDefault(), lookupOptions);
            if (value != null) {
                foreach (var expr in lhs) {
                    AssignToExpr(expr, value);
                }
            }
        }

        public void HandleNamedExpression(NamedExpression node) {
            if (node.Value is ErrorExpression) {
                return;
            }

            var lhs = node.Target.RemoveParenthesis();

            // This is fine, as named expression targets are not allowed to be anything but simple names.
            var value = ExtractRhs(node.Value, lhs);
            if (value != null) {
                AssignToExpr(lhs, value);
            }
        }

        private IMember ExtractRhs(Expression rhs, Expression typed, LookupOptions lookupOptions = LookupOptions.Normal) {
            var value = Eval.GetValueFromExpression(rhs, lookupOptions) ?? Eval.UnknownType;

            // Check PEP hint first
            var valueType = Eval.GetTypeFromPepHint(rhs);
            if (valueType != null) {
                HandleTypedVariable(valueType, value, typed);
                return null;
            }

            if (value.IsUnknown()) {
                Log?.Log(TraceEventType.Verbose, $"Undefined value: {rhs.ToCodeString(Ast).Trim()}");
            }
            if (value?.GetPythonType().TypeId == BuiltinTypeId.Ellipsis) {
                value = Eval.UnknownType;
            }

            return value;
        }

        private void AssignToExpr(Expression expr, IMember value) {
            switch (expr) {
                case SequenceExpression seq:
                    // Tuple = Tuple. Transfer values.
                    var seqHandler = new SequenceExpressionHandler(Walker);
                    seqHandler.HandleAssignment(seq, value);
                    break;
                case ExpressionWithAnnotation annExpr:
                    HandleAnnotatedExpression(annExpr, value);
                    break;
                case NameExpression nameExpr:
                    AssignVariable(nameExpr, value);
                    break;
                case MemberExpression memberExpr:
                    TryHandleClassVariable(memberExpr, value);
                    break;
            }
        }

        public void HandleAnnotatedExpression(ExpressionWithAnnotation expr, IMember value, LookupOptions lookupOptions = LookupOptions.Normal) {
            if (expr?.Annotation == null) {
                return;
            }

            var variableType = Eval.GetTypeFromAnnotation(expr.Annotation, lookupOptions);
            // If value is null, then this is a pure declaration like 
            //   x: List[str]
            // without a value. If value is provided, then this is
            //   x: List[str] = [...]
            HandleTypedVariable(variableType, value, expr.Expression);
        }

        private void TryHandleClassVariable(MemberExpression mex, IMember value) {
            if (mex.Target is NameExpression nex && nex.Name == "self") {
                var m = Eval.LookupNameInScopes(nex.Name, out _, LookupOptions.Local);
                var cls = m.GetPythonType<IPythonClassType>();
                if (cls != null) {
                    using (Eval.OpenScope(Eval.Module, cls.ClassDefinition, out _)) {
                        Eval.DeclareVariable(mex.Name, value, VariableSource.Declaration, Eval.GetLocationOfName(mex));
                    }
                }
            }
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
            var args = ArgumentSet.Empty(expr, Eval);
            instance = instance ?? variableType?.CreateInstance(args) ?? Eval.UnknownType.CreateInstance(ArgumentSet.WithoutContext);

            if (expr is NameExpression ne) {
                Eval.DeclareVariable(ne.Name, instance, VariableSource.Declaration, ne);
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
