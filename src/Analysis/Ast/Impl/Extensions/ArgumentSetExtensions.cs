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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis {
    public static class ArgumentSetExtensions {
        public static IReadOnlyList<T> Values<T>(this IArgumentSet args)
            => args.Arguments.Select(a => a.Value).OfType<T>().ToArray();

        public static IReadOnlyList<KeyValuePair<string, T>> Arguments<T>(this IArgumentSet args) where T : class
            => args.Arguments.Select(a => new KeyValuePair<string, T>(a.Name, a.Value as T)).ToArray();

        public static T Argument<T>(this IArgumentSet args, int index) where T : class
            => args.Arguments[index].Value as T;

        public static T GetArgumentValue<T>(this IArgumentSet args, string name) where T : class {
            var value = args.Arguments.FirstOrDefault(a => name.Equals(a.Name))?.Value;
            if (value == null && args.DictionaryArgument?.Arguments != null && args.DictionaryArgument.Arguments.TryGetValue(name, out var m)) {
                return m as T;
            }
            return value as T;
        }

        internal static void DeclareParametersInScope(this IArgumentSet args, ExpressionEval eval) {
            if (eval == null) {
                return;
            }

            // For class method no need to add extra parameters, but first parameter type should be the class.
            // For static and unbound methods do not add or set anything.
            // For regular bound methods add first parameter and set it to the class.

            foreach (var a in args.Arguments) {
                if (a.Value is IMember m && !string.IsNullOrEmpty(a.Name)) {
                    eval.DeclareVariable(a.Name, m, VariableSource.Declaration, eval.Module, a.Location);
                }
            }

            if (args.ListArgument != null && !string.IsNullOrEmpty(args.ListArgument.Name)) {
                var type = new PythonCollectionType(null, BuiltinTypeId.List, eval.Interpreter, false);
                var list = new PythonCollection(type, args.ListArgument.Values);
                eval.DeclareVariable(args.ListArgument.Name, list, VariableSource.Declaration, eval.Module, args.ListArgument.Location);
            }

            if (args.DictionaryArgument != null) {
                foreach (var kvp in args.DictionaryArgument.Arguments) {
                    eval.DeclareVariable(kvp.Key, kvp.Value, VariableSource.Declaration, eval.Module, args.DictionaryArgument.Location);
                }
            }
        }
    }
}
