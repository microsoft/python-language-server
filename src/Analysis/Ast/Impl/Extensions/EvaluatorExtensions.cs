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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis {
    public static class EvaluatorExtensions {
        public static IMember LookupNameInScopes(this IExpressionEvaluator eval, string name, out IScope scope, LookupOptions options = LookupOptions.Normal)
            => eval.LookupNameInScopes(name, out scope, out _, options);
        public static IMember LookupNameInScopes(this IExpressionEvaluator eval, string name, LookupOptions options = LookupOptions.Normal)
            => eval.LookupNameInScopes(name, out _, out _, options);

        public static IMember LookupImportedNameInScopes(this IExpressionEvaluator eval, string name, out IScope scope) {
            scope = null;
            foreach (var s in eval.CurrentScope.EnumerateTowardsGlobal) {
                var v = s.Imported[name];
                if (v != null) {
                    scope = s;
                    return v.Value;
                }
            }
            return null;
        }

        public static IDisposable OpenScope(this IExpressionEvaluator eval, IPythonClassType cls)
         => eval.OpenScope(cls.DeclaringModule, cls.ClassDefinition);
        public static IDisposable OpenScope(this IExpressionEvaluator eval, IPythonFunctionType ft)
            => eval.OpenScope(ft.DeclaringModule, ft.FunctionDefinition);
    }
}
