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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis {
    public static class MemberExtensions {
        public static bool IsUnknown(this IMember m) {
            switch (m) {
                case null:
                case IPythonType pt when pt.IsUnknown():
                case IPythonInstance pi when pi.IsUnknown():
                case IVariable v when v.Value == null || v.Value.IsUnknown():
                    return true;
                default:
                    return m.MemberType == PythonMemberType.Unknown;
            }
        }

        public static bool IsOfType(this IMember m, BuiltinTypeId typeId)
            => m?.GetPythonType().TypeId == typeId;

        public static string GetString(this IMember m) {
            return (m as IPythonConstant)?.GetString();
        }

        public static IPythonType GetPythonType(this IMember m) {
            switch (m) {
                case IPythonType pt:
                    return pt;
                case IPythonInstance pi:
                    return pi.Type;
                case IVariable v when v.Value != null:
                    Debug.Assert(!(v.Value is IVariable));
                    return v.Value.GetPythonType();
            }
            return null;
        }

        public static T GetPythonType<T>(this IMember m) where T : class, IPythonType
            => m.GetPythonType() as T;

        public static bool IsGeneric(this IMember m) {
            var t = m.GetPythonType();

            if (t is IPythonType pt) {
                return pt.IsGeneric();
            }

            if (m?.MemberType == PythonMemberType.Generic) {
                return true;
            }

            return m is IVariable v && v.Value?.MemberType == PythonMemberType.Generic;
        }

        public static bool TryGetConstant<T>(this IMember m, out T value) {
            if (m is IPythonConstant c && c.TryGetValue<T>(out var v)) {
                value = v;
                return true;
            }
            value = default;
            return false;
        }

        public static void AddReference(this IMember m, Location location)
            => (m as ILocatedMember)?.AddReference(location);

        public static string GetName(this IMember m) {
            switch (m) {
                case IVariable v:
                    return v.Name;
                case IPythonType t:
                    return t.Name;
            }
            return null;
        }

        public static ILocatedMember GetRootDefinition(this ILocatedMember lm) {
            if (!(lm is IImportedMember im) || im.Parent == null) {
                return lm;
            }

            var parent = im.Parent;
            for (; parent != null;) {
                if (!(parent is IImportedMember im1) || im1.Parent == null) {
                    break;
                }
                parent = im1.Parent;
            }
            return parent;
        }

        public static bool IsDeclaredAfter(this IMember m, Location loc)
            => m is ILocatedMember lm && lm.IsDeclaredAfter(loc);

        public static bool IsDeclaredAfter(this ILocatedMember lm, ILocatedMember other)
            => lm.IsDeclaredAfter(other.Location);
        public static bool IsDeclaredAfter(this ILocatedMember lm, Location loc)
            => lm.Location.IndexSpan.Start > loc.IndexSpan.Start;
        public static bool IsDeclaredAfterOrAt(this ILocatedMember lm, ILocatedMember other)
            => lm.IsDeclaredAfterOrAt(other.Location);
        public static bool IsDeclaredAfterOrAt(this ILocatedMember lm, Location loc)
            => lm.Location.IndexSpan.Start >= loc.IndexSpan.Start;

        public static IPythonFunctionType TryGetFunctionType(this IMember m) {
            var t = m.GetPythonType();
            return t is IPythonClassType cls
                ? cls.GetMember<IPythonFunctionType>("__init__")
                : t as IPythonFunctionType;
        }

    }
}
