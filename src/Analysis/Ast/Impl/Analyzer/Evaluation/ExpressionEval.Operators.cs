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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        private async Task<IMember> GetValueFromUnaryOpAsync(UnaryExpression expr, CancellationToken cancellationToken = default) {
            switch (expr.Op) {
                case PythonOperator.Not:
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                    // Assume all of these return True/False
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);

                case PythonOperator.Invert:
                    return await GetValueFromUnaryOpAsync(expr, "__invert__", cancellationToken);
                case PythonOperator.Negate:
                    return await GetValueFromUnaryOpAsync(expr, "__neg__", cancellationToken);
                case PythonOperator.Pos:
                    return await GetValueFromUnaryOpAsync(expr, "__pos__", cancellationToken);
            }
            return null;
        }

        private async Task<IMember> GetValueFromUnaryOpAsync(UnaryExpression expr, string op, CancellationToken cancellationToken = default) {
            var target = await GetValueFromExpressionAsync(expr.Expression, cancellationToken);
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
                    ? new PythonConstant(-value, c.Type, GetLoc(expr))
                    : instance;
            }
            return null;
        }

        private async Task<IMember> GetValueFromBinaryOpAsync(Expression expr, CancellationToken cancellationToken = default) {
            if (expr is AndExpression || expr is OrExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            }

            if (!(expr is BinaryExpression binop) || binop.Left == null) {
                return null;
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
            }

            var left = await GetValueFromExpressionAsync(binop.Left, cancellationToken);
            var right = await GetValueFromExpressionAsync(binop.Right, cancellationToken);

            switch (binop.Operator) {
                case PythonOperator.Divide:
                case PythonOperator.TrueDivide:
                    if (Interpreter.LanguageVersion.Is3x()) {
                        return Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    }

                    break;
            }

            if (right.GetPythonType()?.TypeId == BuiltinTypeId.Float) {
                return right;
            }

            if (left.GetPythonType()?.TypeId == BuiltinTypeId.Float) {
                return left;
            }

            if (right.GetPythonType()?.TypeId == BuiltinTypeId.Long) {
                return right;
            }

            if (left.GetPythonType()?.TypeId == BuiltinTypeId.Long) {
                return left;
            }

            return left.IsUnknown() ? right : left;
        }
    }
}
