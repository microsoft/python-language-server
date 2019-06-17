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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching {
    internal static class TypeNames {
        public static string GetQualifiedName(this IMember m) {
            var t = m.GetPythonType();
            if (!t.IsUnknown()) {
                switch (m) {
                    case IPythonInstance _:
                        return $"i:{GetQualifiedName(t)}";
                    case IPythonType pt when pt.DeclaringModule.ModuleType == ModuleType.Builtins:
                        return pt.TypeId == BuiltinTypeId.Ellipsis ? "ellipsis" : pt.Name;
                    case IPythonType pt:
                        return pt.QualifiedName;
                    case null:
                        break;
                }
            }
            return null;
        }

        /// <summary>
        /// Splits qualified type name in form of i:A(3.6).B.C into parts. as well as determines if
        /// qualified name designates instance (prefixed with 'i:').
        /// </summary>
        /// <param name="rawQualifiedName">Raw qualified name to split. May include instance prefix.</param>
        /// <param name="typeQualifiedName">Qualified name without optional instance prefix, such as A(3.6).B(3.6).C</param>
        /// <param name="nameParts">Name parts such as 'A(3.6)', 'B(3.6)', 'C'.</param>
        /// <param name="isInstance">If true, the qualified name describes instance of a type.</param>
        public static bool DeconstructQualifiedName(string rawQualifiedName, out string typeQualifiedName, out IReadOnlyList<string> nameParts, out bool isInstance) {
            typeQualifiedName = null;
            nameParts = null;
            isInstance = false;

            if (string.IsNullOrEmpty(rawQualifiedName)) {
                return false;
            }

            isInstance = rawQualifiedName.StartsWith("i:");
            typeQualifiedName = isInstance ? rawQualifiedName.Substring(2) : rawQualifiedName;

            if (typeQualifiedName == "..." || typeQualifiedName == "ellipsis") {
                nameParts = new[] { @"builtins", "ellipsis" };
                return true;
            }

            // First chunk is qualified module name except dots in braces.
            // Builtin types don't have module prefix.
            nameParts = GetParts(typeQualifiedName);
            return nameParts.Count > 0;
        }

        public static string GetNameWithoutVersion(string qualifiedName) {
            var index = qualifiedName.IndexOf('(');
            return index > 0 ? qualifiedName.Substring(0, index) : qualifiedName;
        }

        private static IReadOnlyList<string> GetParts(string qualifiedTypeName) {
            var parts = new List<string>();
            for (var i = 0; i < qualifiedTypeName.Length; i++) {
                var part = GetSubPart(qualifiedTypeName, ref i);
                if (string.IsNullOrEmpty(part)) {
                    break;
                }
                parts.Add(part);
            }
            return parts;
        }

        private static string GetSubPart(string s, ref int i) {
            var skip = false;
            var start = i;
            for (; i < s.Length; i++) {
                var ch = s[i];

                if (ch == '(') {
                    skip = true;
                    continue;
                }

                if (ch == ')') {
                    skip = false;
                }

                if (!skip && ch == '.') {
                    i++;
                    break;
                }
            }

            return s.Substring(start, i - start);
        }
    }
}
