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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        /// <summary>
        /// Creates specific type from expression that involves generic type
        /// and the specific type arguments, such as Generic[T] or constructor
        /// of a generic class.
        /// </summary>
        private async Task<IMember> GetValueFromGenericAsync(IMember target, Expression expr, CancellationToken cancellationToken = default) {
            if (!(target is PythonClassType c && c.IsGeneric()) && !(target.GetPythonType() is IGenericType)) {
                return null;
            }
            // Evaluate index to check if the result is generic parameter.
            // If it is, then this is a declaration expression such as Generic[T]
            // rather than specific type instantiation as in List[str].

            switch (expr) {
                // Indexing returns type as from A[int]
                case IndexExpression indexExpr when target is IGenericType gt:
                    // Generic[T1, T2, ...]
                    var indices = await EvaluateIndexAsync(indexExpr, cancellationToken);
                    return CreateSpecificTypeFromIndex(gt, indices, expr);

                case CallExpression callExpr when target is PythonClassType c1:
                    // Alternative instantiation:
                    //  class A(Generic[T]): ...
                    //  x = A(1234)
                    var arguments = (await EvaluateCallArgsAsync(callExpr, cancellationToken)).ToArray();
                    return await CreateClassInstanceAsync(c1, arguments, callExpr, cancellationToken);
            }
            return null;
        }

        private IMember CreateSpecificTypeFromIndex(IGenericType gt, IReadOnlyList<IMember> indices, Expression expr) {
            // See which ones are generic parameters as defined by TypeVar() 
            // and which are specific types. Normally there should not be a mix.
            var genericTypeArgs = indices.OfType<IGenericTypeDefinition>().ToArray();
            var specificTypes = indices.Where(i => !(i is IGenericTypeDefinition)).OfType<IPythonType>().ToArray();

            if (genericTypeArgs.Length > 0 && genericTypeArgs.Length != indices.Count) {
                // TODO: report that some type arguments are not declared with TypeVar.
            }
            if (specificTypes.Length > 0 && specificTypes.Length != indices.Count) {
                // TODO: report that arguments are not specific types or are not declared.
            }

            if (gt.Name.EqualsOrdinal("Generic")) {
                // Generic[T1, T2, ...] expression. Create generic base for the class.
                if (genericTypeArgs.Length > 0) {
                    return new GenericClassParameter(genericTypeArgs, Module, GetLoc(expr));
                } else {
                    // TODO: report too few type arguments for Generic[].
                    return UnknownType;
                }
            }

            // For other types just use supplied arguments
            if (indices.Count > 0) {
                return gt.CreateSpecificType(new ArgumentSet(indices), Module, GetLoc(expr));
            }
            // TODO: report too few type arguments for the generic expression.
            return UnknownType;

        }

        private async Task<IReadOnlyList<IMember>> EvaluateIndexAsync(IndexExpression expr, CancellationToken cancellationToken = default) {
            var indices = new List<IMember>();
            if (expr.Index is TupleExpression tex) {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var item in tex.Items) {
                    var e = await GetValueFromExpressionAsync(item, cancellationToken);
                    indices.Add(e);
                }
            } else {
                var index = await GetValueFromExpressionAsync(expr.Index, cancellationToken);
                indices.Add(index ?? UnknownType);
            }
            return indices;
        }

        private async Task<IReadOnlyList<IMember>> EvaluateCallArgsAsync(CallExpression expr, CancellationToken cancellationToken = default) {
            var indices = new List<IMember>();
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var e in expr.Args.Select(a => a.Expression)) {
                var value = await GetValueFromExpressionAsync(e, cancellationToken) ?? UnknownType;
                indices.Add(value);
            }
            return indices;
        }

        private async Task<IMember> CreateClassInstanceAsync(PythonClassType cls, IReadOnlyList<IMember> constructorArguments, CallExpression callExpr, CancellationToken cancellationToken = default) {
            // Look at the constructor arguments and create argument set
            // based on the __init__ definition.
            var location = GetLoc(callExpr);
            var initFunc = cls.GetMember(@"__init__") as IPythonFunctionType;
            var initOverload = initFunc?.DeclaringType == cls ? initFunc.Overloads.FirstOrDefault() : null;

            var argSet = initOverload != null
                    ? new ArgumentSet(initFunc, 0, null, callExpr, this)
                    : new ArgumentSet(constructorArguments);

            await argSet.EvaluateAsync(cancellationToken);
            var specificType = cls.CreateSpecificType(argSet, Module, location);
            return new PythonInstance(specificType, location);
        }
    }
}
