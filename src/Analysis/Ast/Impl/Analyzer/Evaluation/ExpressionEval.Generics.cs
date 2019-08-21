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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        /// <summary>
        /// Creates specific type from expression that involves generic type
        /// and the specific type arguments, such as Generic[T] or constructor
        /// of a generic class.
        /// </summary>
        private IMember GetValueFromGeneric(IMember target, Expression expr) {
            if (!(target is IGenericType t && t.IsGeneric())) {
                return null;
            }

            using (OpenScope(target.GetPythonType()?.DeclaringModule, GetScope(target), out _)) {
                // Try generics
                // Evaluate index to check if the result is a generic parameter.
                // If it is, then this is a declaration expression such as Generic[T]
                // rather than specific type instantiation as in List[str].
                switch (expr) {
                    // Indexing returns type as from A[int]
                    case IndexExpression indexExpr when target is IGenericType gt:
                        // Generic[T1, T2, ...]
                        var indices = EvaluateIndex(indexExpr);
                        return CreateSpecificTypeFromIndex(gt, indices, expr);
                    case CallExpression callExpr when target is PythonClassType c1:
                        // Alternative instantiation:
                        //  class A(Generic[T]): ...
                        //  x = A(1234)
                        var arguments = EvaluateCallArgs(callExpr).ToArray();
                        return CreateClassInstance(c1, arguments, callExpr);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns whether the arguments to Generic are valid
        /// </summary>

        /// <summary>
        /// Given generic type and list of arguments in the expression like
        /// Mapping[T1, int, ...] or Mapping[str, int] where Mapping inherits from Generic[K,T] creates generic class base
        /// (if the former) on specific type (if the latter).
        /// </summary>
        private IMember CreateSpecificTypeFromIndex(IGenericType gt, IReadOnlyList<IMember> args, Expression expr)
            => args.Count > 0 ? gt.CreateSpecificType(new ArgumentSet(args, expr, this)) : UnknownType;

        private IReadOnlyList<IMember> EvaluateIndex(IndexExpression expr) {
            var indices = new List<IMember>();
            if (expr.Index is TupleExpression tex) {
                foreach (var item in tex.Items) {
                    var e = GetValueFromExpression(item);
                    indices.Add(e);
                }
            } else {
                var index = GetValueFromExpression(expr.Index);
                // Don't count null indexes as arguments
                if (index != null) {
                    indices.Add(index);
                }
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

        /// <summary>
        /// Given generic class type and the passed constructor arguments
        /// creates specific type and instance of the type. Attempts to match
        /// supplied arguments to either __init__ signature or to the
        /// list of generic definitions in Generic[T1, T2, ...].
        /// </summary>
        private IMember CreateClassInstance(PythonClassType cls, IReadOnlyList<IMember> constructorArguments, CallExpression callExpr) {
            // Look at the constructor arguments and create argument set
            // based on the __init__ definition.
            var initFunc = cls.GetMember(@"__init__") as IPythonFunctionType;
            var initOverload = initFunc?.DeclaringType == cls ? initFunc.Overloads.FirstOrDefault() : null;

            var argSet = initOverload != null
                    ? new ArgumentSet(initFunc, 0, null, callExpr, this)
                    : new ArgumentSet(constructorArguments, callExpr, this);

            argSet.Evaluate();
            var specificType = cls.CreateSpecificType(argSet);
            return new PythonInstance(specificType);
        }

        private ScopeStatement GetScope(IMember m) {
            switch (m.GetPythonType()) {
                case IPythonClassType ct:
                    return ct.ClassDefinition;
                case IPythonFunctionType ct:
                    return ct.FunctionDefinition;
            }
            return null;
        }

        public static IReadOnlyList<IPythonType> GetTypeArgumentsFromParameters(IPythonFunctionOverload o, IArgumentSet args) {
            if (o.Parameters.Any(p => p.IsGeneric)) {
                // Declaring class is not generic, but the function is and arguments
                // should provide actual specific types.
                // TODO: handle keyword and dict args
                var list = new List<IPythonType>();
                for (var i = 0; i < Math.Min(o.Parameters.Count, args.Arguments.Count); i++) {
                    if (o.Parameters[i].IsGeneric) {
                        list.AddRange(GetSpecificTypeFromArgumentValue(args.Arguments[i].Value));
                    }
                }
                return list;
            }
            return null;
        }

        /// <summary>
        /// Given argument attempts to extract specific types for the function generic
        /// parameter(s). Handles common cases such as dictionary, list and tuple.
        /// Typically used on a value that is being passed to the function in place
        /// of the generic parameter.
        /// </summary>
        /// <remarks>
        /// Consider 'def func(x: Mapping[K, V]) -> K: ...'
        /// </remarks>
        private static IReadOnlyList<IPythonType> GetSpecificTypeFromArgumentValue(object argumentValue) {
            var specificTypes = new List<IPythonType>();
            switch (argumentValue) {
                case IPythonDictionary dict:
                    var keyType = dict.Keys.FirstOrDefault()?.GetPythonType();
                    var valueType = dict.Values.FirstOrDefault()?.GetPythonType();
                    if (!keyType.IsUnknown()) {
                        specificTypes.Add(keyType);
                    }
                    if (!valueType.IsUnknown()) {
                        specificTypes.Add(valueType);
                    }
                    break;
                case IPythonCollection coll:
                    specificTypes.AddRange(coll.Contents.Select(m => m.GetPythonType()));
                    break;
                case IPythonIterable iter:
                    var itemType = iter.GetIterator().Next.GetPythonType();
                    if (!itemType.IsUnknown()) {
                        specificTypes.Add(itemType);
                    } else if (argumentValue is IPythonInstance inst) {
                        specificTypes.Add(inst.GetPythonType());
                    }
                    break;
                case IMember m:
                    if (!m.IsUnknown()) {
                        specificTypes.Add(m.GetPythonType());
                    }
                    break;
            }
            return specificTypes;
        }
    }
}
