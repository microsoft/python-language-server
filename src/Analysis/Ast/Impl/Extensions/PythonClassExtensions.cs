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
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis {
    public static class PythonClassExtensions {
        public static bool IsGeneric(this IPythonClassType cls)
            => cls.Bases != null && cls.Bases.Any(b => b is IGenericType || b is IGenericClassParameter);

        public static void AddMemberReference(this IPythonType type, string name, IExpressionEvaluator eval, Location location) {
            var m = type.GetMember(name);
            if (m is LocatedMember lm) {
                lm.AddReference(location);
            } else if (type is IPythonClassType cls) {
                using (eval.OpenScope(cls)) {
                    eval.LookupNameInScopes(name, out _, out var v, LookupOptions.Local);
                    v?.AddReference(location);
                }
            } else if (type is IPythonModule module && module.GlobalScope != null && module.GlobalScope.Variables.TryGetVariable(name, out var variable)) {
                variable.AddReference(location);
            }
        }

        /// <summary>
        /// Converts mangled name of a private member to its original form,
        /// by removing private prefix, such as '_ClassName__x' to '__x'.
        /// </summary>
        public static string UnmangleMemberName(this IPythonClassType cls, string memberName) {
            if (memberName.Length == 0 || (memberName.Length > 0 && memberName[0] != '_')) {
                return memberName;
            }

            var prefix = $"_{cls.Name}__";
            return memberName.StartsWithOrdinal(prefix)
                ? memberName.Substring(cls.Name.Length + 1)
                : memberName;
        }

        /// <summary>
        /// Determines if particular member is private by checking its name to mangled form
        /// of Python class private members, such as '_ClassName__memberName'.
        /// </summary>
        public static bool IsPrivateMember(this IPythonClassType cls, string memberName) {
            var unmangledName = cls.UnmangleMemberName(memberName);
            return unmangledName.StartsWithOrdinal("__") && memberName.EqualsOrdinal($"_{cls.Name}{unmangledName}");
        }
    }
}
