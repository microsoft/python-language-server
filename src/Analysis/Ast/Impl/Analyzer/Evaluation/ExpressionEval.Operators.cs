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

            var op = binop.Operator;

            var left = GetValueFromExpression(binop.Left) ?? UnknownType;
            var right = GetValueFromExpression(binop.Right) ?? UnknownType;

            if (left.IsUnknown() && right.IsUnknown()) {
                // Fast path for when nothing below will give any results.
                if (op.IsComparison()) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                }

                return UnknownType;
            }

            var leftType = left.GetPythonType();
            var rightType = right.GetPythonType();

            var leftTypeId = leftType.TypeId;
            var rightTypeId = rightType.TypeId;

            if (op == PythonOperator.Add
                && leftTypeId == rightTypeId
                && left is IPythonCollection lc && right is IPythonCollection rc) {

                switch (leftTypeId) {
                    case BuiltinTypeId.List:
                        return PythonCollectionType.CreateConcatenatedList(Module.Interpreter, lc, rc);
                    case BuiltinTypeId.Tuple:
                        return PythonCollectionType.CreateConcatenatedTuple(Module.Interpreter, lc, rc);
                }
            }

            // Mod-style string formatting; don't bother looking at the right side.
            if (op == PythonOperator.Mod && (leftTypeId == BuiltinTypeId.Str || leftTypeId == BuiltinTypeId.Unicode)) {
                return Interpreter.GetBuiltinType(leftTypeId);
            }

            var leftIsSupported = IsSupportedBinopBuiltin(leftTypeId);
            var rightIsSupported = IsSupportedBinopBuiltin(rightTypeId);

            if (leftIsSupported && rightIsSupported) {
                if (TryGetValueFromBuiltinBinaryOp(op, leftTypeId, rightTypeId, Interpreter.LanguageVersion.Is3x(), out var member)) {
                    return member;
                }
            }

            if (leftIsSupported) {
                IMember ret;

                if (op.IsComparison()) {
                    // If the op is a comparison, and the thing on the left is the builtin,
                    // flip the operation and call it instead.
                    ret = CallOperator(op.InvertComparison(), right, rightType, left, leftType, tryRight: false);
                } else {
                    ret = CallOperator(op, left, leftType, right, rightType, tryLeft: false);
                }

                if (!ret.IsUnknown()) {
                    return ret;
                }

                return op.IsComparison() ? Interpreter.GetBuiltinType(BuiltinTypeId.Bool) : left;
            }

            if (rightIsSupported) {
                // Try calling the function on the left side, otherwise just return right.
                var ret = CallOperator(op, left, leftType, right, rightType, tryRight: false);

                if (!ret.IsUnknown()) {
                    return ret;
                }

                return op.IsComparison() ? Interpreter.GetBuiltinType(BuiltinTypeId.Bool) : right;
            }

            var callRet = CallOperator(op, left, leftType, right, rightType);
            if (!callRet.IsUnknown()) {
                return callRet;
            }

            if (op.IsComparison()) {
                callRet = CallOperator(op.InvertComparison(), right, rightType, left, leftType);

                if (!callRet.IsUnknown()) {
                    return callRet;
                }
            }

            // TODO: Specific parsing
            // TODO: warn about incompatible types like 'str' + 1
            switch (op) {
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

            return left.IsUnknown() ? right : left;
        }

        private IMember CallOperator(PythonOperator op, IMember left, IPythonType leftType, IMember right, IPythonType rightType, bool tryLeft = true, bool tryRight = true) {
            var (funcName, swappedFuncName) = OpMethodName(op);

            if (tryLeft && funcName != null && left is IPythonInstance lpi) {
                var ret = leftType.Call(lpi, funcName, new ArgumentSet(new[] { right }));
                if (!ret.IsUnknown()) {
                    return ret;
                }
            }

            if (tryRight && swappedFuncName != null && right is IPythonInstance rpi) {
                var ret = rightType.Call(rpi, swappedFuncName, new ArgumentSet(new[] { left }));
                if (!ret.IsUnknown()) {
                    return ret;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to get the result of a binary operation on builtin types. This does not actually do the operation, but maybe should.
        /// </summary>
        /// <param name="op">The operation being done.</param>
        /// <param name="left">The left side's type ID.</param>
        /// <param name="right">The right side's type ID.</param>
        /// <param name="is3x">True if the Python version is 3.x.</param>
        /// <param name="member">The resulting member.</param>
        /// <returns>True, if member is correct and no further checks should be done.</returns>
        private bool TryGetValueFromBuiltinBinaryOp(PythonOperator op, BuiltinTypeId left, BuiltinTypeId right, bool is3x, out IMember member) {
            if (op.IsComparison()) {
                // All builtins compare to bool.
                member = Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                return true;
            }

            member = UnknownType;

            switch (op) {
                case PythonOperator.MatMultiply:
                    // No builtins implement this operator.
                    return true;

                case PythonOperator.BitwiseAnd:
                case PythonOperator.BitwiseOr:
                case PythonOperator.Xor:
                    switch (left) {
                        case BuiltinTypeId.Bool when right == BuiltinTypeId.Bool:
                            member = Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                            return true;

                        case BuiltinTypeId.Bool when right == BuiltinTypeId.Int:
                        case BuiltinTypeId.Int when right == BuiltinTypeId.Bool:
                        case BuiltinTypeId.Int when right == BuiltinTypeId.Int:
                            member = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
                            return true;

                        case BuiltinTypeId.Long when right == BuiltinTypeId.Long:
                        case BuiltinTypeId.Long when right == BuiltinTypeId.Int:
                        case BuiltinTypeId.Long when right == BuiltinTypeId.Bool:
                        case BuiltinTypeId.Bool when right == BuiltinTypeId.Long:
                        case BuiltinTypeId.Int when right == BuiltinTypeId.Long:
                            member = Interpreter.GetBuiltinType(BuiltinTypeId.Long);
                            return true;
                    }

                    // All other combinations of these bitwise operaton on builtin types fail.
                    return true;
            }

            // At this point, @ & | ^ are all handled and do not need to be considered.

            if (CoalesceComplex(left, right)) {
                switch (op) {
                    case PythonOperator.Add:
                    case PythonOperator.Multiply:
                    case PythonOperator.Power:
                    case PythonOperator.Subtract:
                    case PythonOperator.Divide:
                    case PythonOperator.TrueDivide:
                    case PythonOperator.FloorDivide when !is3x:
                        member = Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
                        return true;

                    case PythonOperator.FloorDivide when is3x:
                        // Complex numbers cannot be floordiv'd in Python 3.
                        return true;
                }
            }

            if (IsStringLike(left)) {
                member = HandleStringLike(op, left, right);
                return true;
            }

            if (IsStringLike(right)) {
                member = HandleStringLike(op, right, left);
                return true;
            }

            // All string-like cases have been handled.

            // If a complex value made it to here, then it wasn't coalesced or used in a string format; bail.
            if (left == BuiltinTypeId.Complex || right == BuiltinTypeId.Complex) {
                return true;
            }

            switch (op) {
                case PythonOperator.TrueDivide:
                    member = Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    return true;

                case PythonOperator.LeftShift:
                case PythonOperator.RightShift:
                    if (IsIntegerLike(left) && IsIntegerLike(right)) {
                        if (left == BuiltinTypeId.Long || right == BuiltinTypeId.Long) {
                            member = Interpreter.GetBuiltinType(BuiltinTypeId.Long);
                        } else {
                            member = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
                        }
                        return true;
                    }

                    // If they aren't integer-like, then they can't be shifted.
                    return true;

                case PythonOperator.Add:
                case PythonOperator.Divide:
                case PythonOperator.FloorDivide:
                case PythonOperator.Mod:
                case PythonOperator.Power:
                case PythonOperator.Subtract:
                    if (IsIntegerLike(left) && IsIntegerLike(right)) {
                        if (left == BuiltinTypeId.Long || right == BuiltinTypeId.Long) {
                            member = Interpreter.GetBuiltinType(BuiltinTypeId.Long);
                        } else {
                            member = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
                        }
                        return true;
                    } else if (left == BuiltinTypeId.Float || right == BuiltinTypeId.Float) {
                        member = Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                        return true;
                    }
                    break;
            }

            return false;
        }

        private static bool IsSupportedBinopBuiltin(BuiltinTypeId id) {
            switch (id) {
                case BuiltinTypeId.Bool:
                case BuiltinTypeId.Int:
                case BuiltinTypeId.Long:
                case BuiltinTypeId.Float:
                case BuiltinTypeId.Complex:
                case BuiltinTypeId.Str:
                case BuiltinTypeId.Bytes:
                case BuiltinTypeId.Unicode:
                    return true;
            }

            return false;
        }

        private static bool IsIntegerLike(BuiltinTypeId id) => id == BuiltinTypeId.Bool || id == BuiltinTypeId.Int || id == BuiltinTypeId.Long;

        private bool CoalesceComplex(BuiltinTypeId a, BuiltinTypeId b) {
            if (a != BuiltinTypeId.Complex && b == BuiltinTypeId.Complex) {
                var tmp = a;
                a = b;
                b = tmp;
            }

            if (a == BuiltinTypeId.Complex) {
                switch (b) {
                    case BuiltinTypeId.Bool:
                    case BuiltinTypeId.Long:
                    case BuiltinTypeId.Int:
                    case BuiltinTypeId.Float:
                    case BuiltinTypeId.Complex:
                        return true;
                }
            }

            return false;
        }

        private bool IsStringLike(BuiltinTypeId id) => id == BuiltinTypeId.Str || id == BuiltinTypeId.Bytes || id == BuiltinTypeId.Unicode;

        private IMember HandleStringLike(PythonOperator op, BuiltinTypeId str, BuiltinTypeId other) {
            switch (op) {
                case PythonOperator.Multiply when other == BuiltinTypeId.Bool || other == BuiltinTypeId.Int || other == BuiltinTypeId.Long:
                case PythonOperator.Add when str == other:
                    return Interpreter.GetBuiltinType(str);

                case PythonOperator.Add when str == BuiltinTypeId.Unicode || other == BuiltinTypeId.Unicode:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
            }

            return UnknownType;
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
                case PythonOperator.Power: return ("__pow__", "__rpow__");
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
