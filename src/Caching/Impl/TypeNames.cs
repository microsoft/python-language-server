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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching {
    internal static class TypeNames {
        /// <summary>
        /// Constructs persistent member name based on the member and the current module.
        /// Persistent name contains complete information for the member restoration code.
        /// </summary>
        public static string GetPersistentQualifiedName(this IMember m) {
            var t = m.GetPythonType();
            if (!t.IsUnknown()) {
                switch (m) {
                    case IPythonInstance _: // constants and strings map here.
                        return t is ITypingNamedTupleType nt1 ? $"n:{nt1.QualifiedName}" : $"i:{t.QualifiedName}";
                    case IBuiltinsPythonModule b:
                        return $"b:{b.QualifiedName}";
                    case PythonVariableModule vm:
                        return $"p:{vm.QualifiedName}";
                    case IPythonModule mod:
                        return $"m:{mod.QualifiedName}";
                    case ITypingNamedTupleType nt2:
                        return $"n:{nt2.QualifiedName}";
                    case IPythonType pt when pt.DeclaringModule.ModuleType == ModuleType.Builtins:
                        return $"t:{(pt.TypeId == BuiltinTypeId.Ellipsis ? "ellipsis" : pt.Name)}";
                    case IPythonType pt:
                        return $"t:{pt.QualifiedName}";
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

            GetObjectTypeFromPrefix(qualifiedName, ref parts, out var prefixOffset);
            GetModuleNameAndMembers(qualifiedName, ref parts, prefixOffset);

            return !string.IsNullOrEmpty(parts.ModuleName);
        }

        private static void GetObjectTypeFromPrefix(string qualifiedName, ref QualifiedNameParts parts, out int prefixOffset) {
            prefixOffset = 2;
            if (qualifiedName.StartsWith("i:")) {
                parts.ObjectType = ObjectType.Instance;
            } else if (qualifiedName.StartsWith("m:")) {
                parts.ObjectType = ObjectType.Module;
            } else if (qualifiedName.StartsWith("p:")) {
                parts.ObjectType = ObjectType.VariableModule;
            } else if (qualifiedName.StartsWith("b:")) {
                parts.ObjectType = ObjectType.BuiltinModule;
            } else if (qualifiedName.StartsWith("t:")) {
                parts.ObjectType = ObjectType.Type;
            } else if (qualifiedName.StartsWith("n:")) {
                parts.ObjectType = ObjectType.NamedTuple;
            } else {
                // Unprefixed name is typically an argument to another type like Union[int, typing:Any]
                parts.ObjectType = ObjectType.Type;
                prefixOffset = 0;
            }
        }

        private static void GetModuleNameAndMembers(string qualifiedName, ref QualifiedNameParts parts, int prefixOffset) {
            // Strip the prefix, turning i:module:A.B.C into module:A.B.C
            var typeName = qualifiedName.Substring(prefixOffset);

            var moduleSeparatorIndex = typeName.IndexOf(':');
            if (moduleSeparatorIndex < 0) {
                switch (parts.ObjectType) {
                    case ObjectType.Type:
                    case ObjectType.Instance:
                        // No module name means built-in type like 'int' or 'i:str'.
                        parts.ModuleName = @"builtins";
                        parts.MemberNames = new[] { typeName == "..." ? "ellipsis" : typeName };
                        break;
                    default:
                        parts.ModuleName = typeName;
                        parts.MemberNames = Array.Empty<string>();
                        break;
                }
                return;
            }
            
            // Extract module name and member names, of any.
            parts.ModuleName = typeName.Substring(0, moduleSeparatorIndex);
            var memberNamesOffset = parts.ModuleName.Length + 1;
            parts.MemberNames = GetTypeNames(typeName.Substring(memberNamesOffset), '.');
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
