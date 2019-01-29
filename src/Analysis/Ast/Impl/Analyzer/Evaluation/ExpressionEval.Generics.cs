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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        /// <summary>
        /// Creates specific type from expression that involves generic type
        /// and the specific type arguments.
        /// </summary>
        /// <example>
        ///     x = List[T]
        /// </example>
        /// <example>
        ///     T = TypeVar('T', Exception)
        ///     class A(Generic[T]): ...
        ///     x = A(TypeError)
        /// </example>
        private async Task<IMember> GetValueFromGenericAsync(IMember target, IndexExpression expr, CancellationToken cancellationToken = default) {
            // Evaluate index to check if the result is generic parameter.
            // If it is, then this is a declaration expression such as Generic[T]
            // rather than specific type instantiation as in List[str].
            var indices = await EvaluateIndexAsync(expr, cancellationToken);

            var genericTypeArgs = indices.OfType<IGenericTypeParameter>().ToArray();
            var specificTypes = indices.Where(i => !(i is IGenericTypeParameter)).OfType<IPythonType>().ToArray();

            if (genericTypeArgs.Length > 0 && genericTypeArgs.Length != indices.Count) {
                // TODO: report that some type arguments are not declared with TypeVar.
            }
            if (specificTypes.Length > 0 && specificTypes.Length != indices.Count) {
                // TODO: report that arguments are not specific types or are not declared.
            }

            // Optimistically use what we have
            if (target is IGenericType gt) {
                if (gt.Name.EqualsOrdinal("Generic")) {
                    if (genericTypeArgs.Length > 0) {
                        // Generic[T1, T2, ...] expression. Create generic base for the class.
                        return new GenericClassBaseType(genericTypeArgs, Module, GetLoc(expr));
                    } else {
                        // TODO: report too few type arguments for Generic[].
                        return UnknownType;
                    }
                }

                if (specificTypes.Length > 0) {
                    // If target is a generic type and indexes are specific types, create specific class
                    return gt.CreateSpecificType(specificTypes, Module, GetLoc(expr));
                } else {
                    // TODO: report too few type arguments for the generic[].
                    return UnknownType;
                }
            }

            // This is a bit of a hack since we don't have GenericClassType at the moment.
            // The reason is that PythonClassType is created before ClassDefinition is walked
            // as we resolve classes on demand. Therefore we don't know if class is generic
            // or not at the time of the PythonClassType creation.
            // TODO: figure out if we could make GenericClassType: PythonClassType, IGenericType instead.
            if (target is PythonClassType cls && cls.IsGeneric()) {
                return await cls.CreateSpecificTypeAsync(new ArgumentSet(specificTypes), Module, GetLoc(expr), cancellationToken);
            }

            return null;
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
                indices.Add(index);
            }
            return indices;
        }
    }
}
