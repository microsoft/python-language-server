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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Types {
    public static class BuiltinTypeIdExtensions {
        /// <summary>
        /// Indicates whether an ID should be remapped by an interpreter.
        /// </summary>
        public static bool IsVirtualId(this BuiltinTypeId id) => id == BuiltinTypeId.Str ||
                id == BuiltinTypeId.StrIterator ||
                (int)id > (int)LastTypeId;

        public static BuiltinTypeId LastTypeId => BuiltinTypeId.CallableIterator;

        public static string GetModuleName(this BuiltinTypeId id, Version version)
            => id.GetModuleName(version.Major == 3);

        public static string GetModuleName(this BuiltinTypeId id, PythonLanguageVersion languageVersion)
            => id.GetModuleName(languageVersion.IsNone() || languageVersion.Is3x());

        private static string GetModuleName(this BuiltinTypeId id, bool is3x)
            => is3x ? "builtins" : "__builtin__";

        public static string GetTypeName(this BuiltinTypeId id, Version version)
            => id.GetTypeName(version.Major == 3);

        public static string GetTypeName(this BuiltinTypeId id, PythonLanguageVersion languageVersion)
            => id.GetTypeName(languageVersion.IsNone() || languageVersion.Is3x());

        private static string GetTypeName(this BuiltinTypeId id, bool is3x) {
            string name;
            switch (id) {
                case BuiltinTypeId.Bool: name = "bool"; break;
                case BuiltinTypeId.Complex: name = "complex"; break;
                case BuiltinTypeId.Dict: name = "dict"; break;
                case BuiltinTypeId.Float: name = "float"; break;
                case BuiltinTypeId.Int: name = "int"; break;
                case BuiltinTypeId.List: name = "list"; break;
                case BuiltinTypeId.Long: name = is3x ? "int" : "long"; break;
                case BuiltinTypeId.Object: name = "object"; break;
                case BuiltinTypeId.Set: name = "set"; break;
                case BuiltinTypeId.Str: name = "str"; break;
                case BuiltinTypeId.Unicode: name = is3x ? "str" : "unicode"; break;
                case BuiltinTypeId.Bytes: name = is3x ? "bytes" : "str"; break;
                case BuiltinTypeId.Tuple: name = "tuple"; break;
                case BuiltinTypeId.Type: name = "type"; break;

                case BuiltinTypeId.DictKeys: name = "dict_keys"; break;
                case BuiltinTypeId.DictValues: name = "dict_values"; break;
                case BuiltinTypeId.DictItems: name = "dict_items"; break;
                case BuiltinTypeId.Function: name = "function"; break;
                case BuiltinTypeId.Generator: name = "generator"; break;
                case BuiltinTypeId.NoneType: name = "NoneType"; break;
                case BuiltinTypeId.Ellipsis: name = "ellipsis"; break;
                case BuiltinTypeId.Module: name = "module_type"; break;
                case BuiltinTypeId.ListIterator: name = "list_iterator"; break;
                case BuiltinTypeId.TupleIterator: name = "tuple_iterator"; break;
                case BuiltinTypeId.SetIterator: name = "set_iterator"; break;
                case BuiltinTypeId.StrIterator: name = "str_iterator"; break;
                case BuiltinTypeId.UnicodeIterator: name = is3x ? "str_iterator" : "unicode_iterator"; break;
                case BuiltinTypeId.BytesIterator: name = is3x ? "bytes_iterator" : "str_iterator"; break;
                case BuiltinTypeId.CallableIterator: name = "callable_iterator"; break;

                case BuiltinTypeId.Property: name = "property"; break;
                case BuiltinTypeId.Method: name = "method"; break;
                case BuiltinTypeId.ClassMethod: name = "classmethod"; break;
                case BuiltinTypeId.StaticMethod: name = "staticmethod"; break;
                case BuiltinTypeId.FrozenSet: name = "frozenset"; break;

                case BuiltinTypeId.Unknown:
                default:
                    return null;
            }
            return name;
        }

        public static BuiltinTypeId GetTypeId(this string name) {
            switch (name) {
                case "int": return BuiltinTypeId.Int;
                case "long": return BuiltinTypeId.Long;
                case "bool": return BuiltinTypeId.Bool;
                case "float": return BuiltinTypeId.Float;
                case "str": return BuiltinTypeId.Str;
                case "complex": return BuiltinTypeId.Complex;
                case "dict": return BuiltinTypeId.Dict;
                case "list": return BuiltinTypeId.List;
                case "object": return BuiltinTypeId.Object;

                case "set": return BuiltinTypeId.Set;
                case "unicode": return BuiltinTypeId.Unicode;
                case "bytes": return BuiltinTypeId.Bytes;
                case "tuple": return BuiltinTypeId.Tuple;
                case "type": return BuiltinTypeId.Type;
                case "frozenset": return BuiltinTypeId.FrozenSet;

                case "dict_keys": return BuiltinTypeId.DictKeys;
                case "dict_values": return BuiltinTypeId.DictValues;
                case "dict_items": return BuiltinTypeId.DictItems;

                case "function": return BuiltinTypeId.Function;
                case "generator": return BuiltinTypeId.Generator;
                case "NoneType": return BuiltinTypeId.NoneType;
                case "ellipsis": return BuiltinTypeId.Ellipsis;
                case "module_type": return BuiltinTypeId.Module;

                case "list_iterator": return BuiltinTypeId.ListIterator;
                case "tuple_iterator": return BuiltinTypeId.TupleIterator;
                case "set_iterator": return BuiltinTypeId.SetIterator;
                case "str_iterator": return BuiltinTypeId.StrIterator;
                case "unicode_iterator": return BuiltinTypeId.UnicodeIterator;
                case "bytes_iterator": return BuiltinTypeId.BytesIterator;
                case "callable_iterator": return BuiltinTypeId.CallableIterator;

                case "property": return BuiltinTypeId.Property;
                case "method": return BuiltinTypeId.Method;
                case "classmethod": return BuiltinTypeId.ClassMethod;
                case "staticmethod": return BuiltinTypeId.StaticMethod;
            }
            return BuiltinTypeId.Unknown;
        }

        internal static PythonMemberType GetMemberId(this BuiltinTypeId id) {
            switch (id) {
                case BuiltinTypeId.Bool:
                case BuiltinTypeId.Complex:
                case BuiltinTypeId.Float:
                case BuiltinTypeId.Int:
                case BuiltinTypeId.Long:
                case BuiltinTypeId.Str:
                case BuiltinTypeId.Unicode:
                case BuiltinTypeId.NoneType:
                case BuiltinTypeId.Ellipsis:
                case BuiltinTypeId.Dict:
                case BuiltinTypeId.List:
                case BuiltinTypeId.Object:
                case BuiltinTypeId.Set:
                case BuiltinTypeId.Bytes:
                case BuiltinTypeId.Tuple:
                case BuiltinTypeId.DictKeys:
                case BuiltinTypeId.DictValues:
                case BuiltinTypeId.DictItems:
                case BuiltinTypeId.Generator:
                case BuiltinTypeId.FrozenSet:
                case BuiltinTypeId.ListIterator:
                case BuiltinTypeId.TupleIterator:
                case BuiltinTypeId.SetIterator:
                case BuiltinTypeId.StrIterator:
                case BuiltinTypeId.UnicodeIterator:
                case BuiltinTypeId.BytesIterator:
                case BuiltinTypeId.CallableIterator:
                    return PythonMemberType.Instance;

                case BuiltinTypeId.Type:
                    return PythonMemberType.Class;

                case BuiltinTypeId.Module:
                    return PythonMemberType.Module;

                case BuiltinTypeId.Function:
                case BuiltinTypeId.ClassMethod:
                case BuiltinTypeId.StaticMethod:
                    return PythonMemberType.Function;

                case BuiltinTypeId.Property:
                    return PythonMemberType.Property;

                case BuiltinTypeId.Method:
                    return PythonMemberType.Method;
            }
            return PythonMemberType.Unknown;
        }

        public static BuiltinTypeId GetIteratorTypeId(this BuiltinTypeId typeId) {
            switch (typeId) {
                case BuiltinTypeId.Bytes:
                    return BuiltinTypeId.BytesIterator;
                case BuiltinTypeId.Set:
                    return BuiltinTypeId.SetIterator;
                case BuiltinTypeId.Str:
                    return BuiltinTypeId.StrIterator;
                case BuiltinTypeId.Tuple:
                    return BuiltinTypeId.TupleIterator;
                case BuiltinTypeId.Unicode:
                    return BuiltinTypeId.UnicodeIterator;
                default:
                    return BuiltinTypeId.ListIterator;
            }
        }
    }
}
