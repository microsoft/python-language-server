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
using System.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching {
    internal enum StringType {
        None,
        Ascii,
        Unicode,
        FString
    }

    internal struct QualifiedNameParts {
        /// <summary>Module name.</summary>
        public string ModuleName;
        /// <summary>Module member names such as 'A', 'B', 'C' from module:A.B.C.</summary>
        public IReadOnlyList<string> MemberNames;
        /// <summary>If true, the qualified name describes instance of a type./// </summary>
        public bool IsInstance;
        /// <summary>If true, module is <see cref="PythonVariableModule"/>.</summary>
        public bool IsVariableModule;
        /// <summary>If true, the qualified name describes constant.</summary>
        public bool IsConstant;
        /// <summary>Describes string type.</summary>
        public StringType StringType;
    }

    internal static class TypeNames {
        public static string GetPersistentQualifiedName(this IMember m) {
            var t = m.GetPythonType();
            if (!t.IsUnknown()) {
                switch (m) {
                    case IPythonInstance _: // constants and strings map here.
                        return $"i:{t.QualifiedName}";
                    case IBuiltinsPythonModule b:
                        return $":{b.QualifiedName}";
                    case PythonVariableModule vm:
                        return $"::{vm.QualifiedName}";
                    case IPythonModule mod:
                        return $":{mod.QualifiedName}";
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
        /// <param name="qualifiedName">Qualified name to split. May include instance prefix.</param>
        /// <param name="parts">Qualified name parts.</param>
        public static bool DeconstructQualifiedName(string qualifiedName, out QualifiedNameParts parts) {
            parts = new QualifiedNameParts();

            if (string.IsNullOrEmpty(qualifiedName)) {
                return false;
            }

            parts.IsInstance = qualifiedName.StartsWith("i:");
            parts.IsVariableModule = qualifiedName.StartsWith("::");
            qualifiedName = parts.IsInstance ? qualifiedName.Substring(2) : qualifiedName;

            if (qualifiedName == "..." || qualifiedName == "ellipsis") {
                parts.ModuleName = @"builtins";
                parts.MemberNames = new[] { "ellipsis" };
                return true;
            }

            var moduleSeparatorIndex = qualifiedName.IndexOf(':');
            switch (moduleSeparatorIndex) {
                case -1:
                    // Unqualified type means built-in type like 'str'.
                    parts.ModuleName = @"builtins";
                    parts.MemberNames = new[] { qualifiedName };
                    break;
                case 0:
                    // Type is module persisted as ':sys' or '::sys';
                    var memberSeparatorIndex = qualifiedName.IndexOf('.');
                    var moduleNameOffset = parts.IsVariableModule ? 2 : 1;
                    parts.ModuleName = memberSeparatorIndex < 0 
                        ? qualifiedName.Substring(moduleNameOffset) 
                        : qualifiedName.Substring(moduleNameOffset, memberSeparatorIndex - moduleNameOffset);
                    parts.MemberNames = GetTypeNames(qualifiedName.Substring(parts.ModuleName.Length + moduleNameOffset), '.');
                    break;
                default:
                    parts.ModuleName = qualifiedName.Substring(0, moduleSeparatorIndex);
                    // First chunk is qualified module name except dots in braces.
                    // Builtin types don't have module prefix.
                    parts.MemberNames = GetTypeNames(qualifiedName.Substring(moduleSeparatorIndex + 1), '.');
                    break;
            }

            return !string.IsNullOrEmpty(parts.ModuleName);
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
