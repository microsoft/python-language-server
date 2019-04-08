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

using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis {
    public static class PythonClassExtensions {
        public static bool IsGeneric(this IPythonClassType cls) 
            => cls.Bases != null && cls.Bases.Any(b => b is IGenericType || b is IGenericClassParameter);

        public static void AddMemberReference(this IPythonType type, string name, IExpressionEvaluator eval, Location location) {
            var m = type.GetMember(name);
            if (m is LocatedMember lm) {
                lm.AddReference(location);
            } else if(type is IPythonClassType cls) {
                using (eval.OpenScope(cls.DeclaringModule, cls.ClassDefinition)) {
                    eval.LookupNameInScopes(name, out _, out var v, LookupOptions.Local);
                    v?.AddReference(location);
                }
            }
        }
    }
}
