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

using System.Diagnostics;
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

        public static bool DeconstructQualifiedName(string qualifiedName, out string moduleQualifiedName, out string moduleName, out string typeName, out bool isInstance) {
            moduleQualifiedName = null;
            moduleName = null;
            typeName = null;
            isInstance = false;

            if (string.IsNullOrEmpty(qualifiedName)) {
                return false;
            }

            isInstance = qualifiedName.StartsWith("i:");
            qualifiedName = isInstance ? qualifiedName.Substring(2) : qualifiedName;

            if (qualifiedName == "..." || qualifiedName == "ellipsis") {
                moduleName = @"builtins";
                moduleQualifiedName = moduleName;
                typeName = "ellipsis";
                return true;
            }

            // First chunk is qualified module name except dots in braces.
            // Builtin types don't have module prefix.
            typeName = GetModuleNames(qualifiedName, out moduleQualifiedName, out moduleName)
                ? qualifiedName.Substring(moduleQualifiedName.Length + 1)
                : qualifiedName;

            moduleQualifiedName = moduleQualifiedName ?? @"builtins";
            moduleName = moduleName ?? @"builtins";
            return true;
        }

        private static bool GetModuleNames(string s, out string moduleQualifiedName, out string moduleName) {
            moduleQualifiedName = null;
            moduleName = null;

            var firstDot = -1;
            var skip = false;

            for (var i = 0; i < s.Length; i++) {
                var ch = s[i];

                if (ch == '(') {
                    skip = true;
                    continue;
                }

                if (ch == ')') {
                    skip = false;
                }

                if (!skip && ch == '.') {
                    firstDot = i;
                    break;
                }
            }

            if (firstDot < 0) {
                return false;
            }

            moduleQualifiedName = s.Substring(0, firstDot);
            moduleName = ModuleQualifiedName.GetModuleName(moduleQualifiedName);
            return true;
        }
    }
}
