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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        private IMember GetValueFromUnaryOp(UnaryExpression expr) {
            switch (expr.Op) {
                case PythonOperator.Not:
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                    // Assume all of these return True/False
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);

                case PythonOperator.Invert:
                    return GetValueFromUnaryOp(expr, "__invert__");
                case PythonOperator.Negate:
                    return GetValueFromUnaryOp(expr, "__neg__");
                case PythonOperator.Pos:
                    return GetValueFromUnaryOp(expr, "__pos__");
            }
            return UnknownType;
        }

        private IMember GetValueFromUnaryOp(UnaryExpression expr, string op) {
            var target = GetValueFromExpression(expr.Expression);
            if (target is IPythonInstance instance) {
                var fn = instance.GetPythonType()?.GetMember<IPythonFunctionType>(op);
                // Process functions declared in code modules. Scraped/compiled/stub modules do not actually perform any operations.
                if (fn?.DeclaringModule != null && (fn.DeclaringModule.ModuleType == ModuleType.User || fn.DeclaringModule.ModuleType == ModuleType.Library)) {
                    var result = fn.Call(instance, op, ArgumentSet.Empty);
                    if (!result.IsUnknown()) {
                        return result;
                    }
                }
                return instance is IPythonConstant c && instance.TryGetConstant<int>(out var value)
                    ? new PythonConstant(-value, c.Type)
                    : instance;
            }
            return UnknownType;
        }

        private IMember GetValueFromBinaryOp(Expression expr) {
            if (expr is AndExpression a) {
                GetValueFromExpression(a.Left);
                GetValueFromExpression(a.Right);
                return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            }

            if (expr is OrExpression orexp) {
                // Consider 'self.__params = types.MappingProxyType(params or {})'
                var leftSide = GetValueFromExpression(orexp.Left);
                if (!leftSide.IsUnknown()) {
                    return leftSide;
                }
                var rightSide = GetValueFromExpression(orexp.Right);
                return rightSide.IsUnknown() ? Interpreter.GetBuiltinType(BuiltinTypeId.Bool) : rightSide;
            }

            if (!(expr is BinaryExpression binop) || binop.Left == null) {
                return null;
            }

            var left = GetValueFromExpression(binop.Left) ?? UnknownType;
            var right = GetValueFromExpression(binop.Right) ?? UnknownType;

            var (funcName, swappedFuncName) = OpMethodName(binop.Operator);

            var leftType = left.GetPythonType();

            if (funcName != null && left is IPythonInstance lpi) {
                var ret = leftType?.Call(lpi, funcName, new ArgumentSet(new[] { right }));
                if (ret != null) {
                    return ret;
                }
            }

            var rightType = right.GetPythonType();

            if (swappedFuncName != null && right is IPythonInstance rpi) {
                var ret = rightType?.Call(rpi, swappedFuncName, new ArgumentSet(new[] { left }));
                if (ret != null) {
                    return ret;
                }
            }

            // TODO: Specific parsing
            // TODO: warn about incompatible types like 'str' + 1
            switch (binop.Operator) {
                case PythonOperator.Equal:
                case PythonOperator.GreaterThan:
                case PythonOperator.GreaterThanOrEqual:
                case PythonOperator.In:
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                case PythonOperator.LessThan:
                case PythonOperator.LessThanOrEqual:
                case PythonOperator.Not:
                case PythonOperator.NotEqual:
                case PythonOperator.NotIn:
                    // Assume all of these return True/False
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);

                case PythonOperator.Divide:
                case PythonOperator.TrueDivide:
                    if (Interpreter.LanguageVersion.Is3x()) {
                        return Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    }

                    break;
            }

            if (rightType?.TypeId == BuiltinTypeId.Float) {
                return right;
            }


            if (leftType?.TypeId == BuiltinTypeId.Float) {
                return left;
            }

            if (rightType?.TypeId == BuiltinTypeId.Long) {
                return right;
            }

            if (leftType?.TypeId == BuiltinTypeId.Long) {
                return left;
            }

            if (binop.Operator == PythonOperator.Add
                && leftType?.TypeId == BuiltinTypeId.List && rightType?.TypeId == BuiltinTypeId.List
                && left is IPythonCollection lc && right is IPythonCollection rc) {

                return PythonCollectionType.CreateConcatenatedList(Module.Interpreter, lc, rc);
            }

            return left.IsUnknown() ? right : left;
        }

        private static (string name, string swappedName) OpMethodName(PythonOperator op) {
            switch (op) {
                // Unary operators
                // Not cannot be overridden, there is no method for it.
                case PythonOperator.Pos: return ("__pos__", null);
                case PythonOperator.Invert: return ("__invert__", null);
                case PythonOperator.Negate: return ("__neg__", null);

                // Numeric operators, can be swapped
                case PythonOperator.Add: return ("__add__", "__radd__");
                case PythonOperator.Subtract: return ("__sub__", "__rsub__");
                case PythonOperator.Multiply: return ("__mul__", "__rmul__");
                case PythonOperator.MatMultiply: return ("__matmul__", "__rmatmul__");
                case PythonOperator.Divide: return ("__div__", "__rdiv__"); // The parser has already chosen the correct operator here; no need to check versions.
                case PythonOperator.TrueDivide: return ("__truediv__", "__rtruediv__");
                case PythonOperator.Mod: return ("__mod__", "__rmod__");
                case PythonOperator.BitwiseAnd: return ("__and__", "__rand__");
                case PythonOperator.BitwiseOr: return ("__or__", "__ror__");
                case PythonOperator.Xor: return ("__xor__", "__rxor__");
                case PythonOperator.LeftShift: return ("__lshift__", "__rlshift__");
                case PythonOperator.RightShift: return ("__rshift__", "__rrshift__");
                case PythonOperator.Power: return ("__pow__", "__ppow__");
                case PythonOperator.FloorDivide: return ("__floordiv__", "__rfloordiv__");

                // Comparison operators
                case PythonOperator.LessThan: return ("__lt__", null);
                case PythonOperator.LessThanOrEqual: return ("__le__", null);
                case PythonOperator.GreaterThan: return ("__gt__", null);
                case PythonOperator.GreaterThanOrEqual: return ("__ge__", null);
                case PythonOperator.Equal: return ("__eq__", null);
                case PythonOperator.NotEqual: return ("__ne__", null);
            }

            return (null, null);
        }
    }
}
