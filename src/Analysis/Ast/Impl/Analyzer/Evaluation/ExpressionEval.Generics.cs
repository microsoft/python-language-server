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
using System.Linq;
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
        private IMember GetValueFromGeneric(IMember target, Expression expr) {
            if (!(target is PythonClassType c && c.IsGeneric()) && !(target is IGenericType)) {
                return null;
            }
            // Evaluate index to check if the result is generic parameter.
            // If it is, then this is a declaration expression such as Generic[T]
            // rather than specific type instantiation as in List[str].

            IPythonType[] specificTypes;
            var returnInstance = false;
            switch (expr) {
                // Indexing returns type as from A[int]
                case IndexExpression indexExpr:
                    // Generic[T1, T2, ...] or A[type]()
                    var indices = EvaluateIndex(indexExpr);
                    // See which ones are generic parameters as defined by TypeVar() 
                    // and which are specific types. Normally there should not be a mix.
                    var genericTypeArgs = indices.OfType<IGenericTypeParameter>().ToArray();
                    specificTypes = indices.Where(i => !(i is IGenericTypeParameter)).OfType<IPythonType>().ToArray();

                    if (specificTypes.Length == 0 && genericTypeArgs.Length > 0) {
                        // The expression is still generic. For example, generic return
                        // annotation of a class method, such as 'def func(self) -> A[_E]: ...'.
                        // Leave it alone, we don't want resolve generic with generic.
                        return null;
                    }

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
                            return gt.CreateSpecificType(new ArgumentSet(specificTypes), Module, GetLoc(expr));
                        } else {
                            // TODO: report too few type arguments for the Generic[].
                            return UnknownType;
                        }
                    }
                    break;

                case CallExpression callExpr:
                    // Alternative instantiation:
                    //  class A(Generic[T]): ...
                    //  x = A(1234)
                    specificTypes = EvaluateCallArgs(callExpr).Select(x => x.GetPythonType()).ToArray();
                    // Callable returns instance (as opposed to a type with index expression)
                    returnInstance = true;
                    break;

                default:
                    return null;
            }

            // This is a bit of a hack since we don't have GenericClassType at the moment.
            // The reason is that PythonClassType is created before ClassDefinition is walked
            // as we resolve classes on demand. Therefore we don't know if class is generic
            // or not at the time of the PythonClassType creation.
            // TODO: figure out if we could make GenericClassType: PythonClassType, IGenericType instead.
            if (target is PythonClassType cls) {
                var location = GetLoc(expr);
                var type = cls.CreateSpecificType(new ArgumentSet(specificTypes), Module, location);
                return returnInstance ? new PythonInstance(type, GetLoc(expr)) : (IMember)type;
            }
            return null;
        }

        private IReadOnlyList<IMember> EvaluateIndex(IndexExpression expr) {
            var indices = new List<IMember>();
            if (expr.Index is TupleExpression tex) {
                foreach (var item in tex.Items) {
                    var e = GetValueFromExpression(item);
                    indices.Add(e);
                }
            } else {
                var index = GetValueFromExpression(expr.Index);
                indices.Add(index ?? UnknownType);
            }
            return indices;
        }

        private IReadOnlyList<IMember> EvaluateCallArgs(CallExpression expr) {
            var indices = new List<IMember>();
            foreach (var e in expr.Args.Select(a => a.Expression)) {
                var value = GetValueFromExpression(e) ?? UnknownType;
                indices.Add(value);
            }
            return indices;
        }
    }
}
