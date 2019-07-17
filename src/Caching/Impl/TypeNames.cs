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

using System.Collections.Generic;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching {
    internal static class TypeNames {
        public static string GetPersistentQualifiedName(this IMember m) {
            var t = m.GetPythonType();
            if (!t.IsUnknown()) {
                switch (m) {
                    case IPythonInstance _:
                        return $"i:{t.QualifiedName}";
                    case IPythonType pt when pt.DeclaringModule.ModuleType == ModuleType.Builtins:
                        return pt.TypeId == BuiltinTypeId.Ellipsis ? "ellipsis" : pt.Name;
                    case IPythonModule mod:
                        return $":{mod.QualifiedName}";
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
        /// <param name="qualifiedName">Qualified name to split. May include instance prefix.</param>
        /// <param name="moduleName">Module name.</param>
        /// <param name="memberNames">Module member names such as 'A', 'B', 'C' from module:A.B.C.</param>
        /// <param name="isInstance">If true, the qualified name describes instance of a type.</param>
        public static bool DeconstructQualifiedName(string qualifiedName, out string moduleName, out IReadOnlyList<string> memberNames, out bool isInstance) {
            moduleName = null;
            memberNames = null;
            isInstance = false;

            if (string.IsNullOrEmpty(qualifiedName)) {
                return false;
            }

            isInstance = qualifiedName.StartsWith("i:");
            qualifiedName = isInstance ? qualifiedName.Substring(2) : qualifiedName;

            if (qualifiedName == "..." || qualifiedName == "ellipsis") {
                moduleName = @"builtins";
                memberNames = new[] { "ellipsis" };
                return true;
            }

            var moduleSeparatorIndex = qualifiedName.IndexOf(':');
            switch (moduleSeparatorIndex) {
                case -1:
                    // Unqualified type means built-in type like 'str'.
                    moduleName = @"builtins";
                    memberNames = new[] { qualifiedName };
                    break;
                case 0:
                    // Type is module persisted as ':sys';
                    var memberSeparatorIndex = qualifiedName.IndexOf('.');
                    moduleName = memberSeparatorIndex < 0 ? qualifiedName.Substring(1) : qualifiedName.Substring(1, memberSeparatorIndex - 1);
                    memberNames = GetTypeNames(qualifiedName.Substring(moduleName.Length + 1), '.');
                    break;
                default:
                    moduleName = qualifiedName.Substring(0, moduleSeparatorIndex);
                    // First chunk is qualified module name except dots in braces.
                    // Builtin types don't have module prefix.
                    memberNames = GetTypeNames(qualifiedName.Substring(moduleSeparatorIndex + 1), '.');
                    break;
            }

            return !string.IsNullOrEmpty(moduleName);
        }

        public static IReadOnlyList<string> GetTypeNames(string qualifiedTypeName, char separator) {
            var parts = new List<string>();
            for (var i = 0; i < qualifiedTypeName.Length; i++) {
                var part = GetTypeName(qualifiedTypeName, ref i, separator);
                if (string.IsNullOrEmpty(part)) {
                    break;
                }
                parts.Add(part.Trim());
            }
            return parts;
        }

        public static string GetTypeName(string s, ref int i, char separator) {
            var braceCounter = new Stack<char>();
            var start = i;
            for (; i < s.Length; i++) {
                var ch = s[i];

                if (ch == '[') {
                    braceCounter.Push(ch);
                    continue;
                }

                if (ch == ']') {
                    if (braceCounter.Count > 0) {
                        braceCounter.Pop();
                    }
                }

                if (braceCounter.Count == 0 && ch == separator) {
                    break;
                }
            }

            return s.Substring(start, i - start).Trim();
        }
    }
}
