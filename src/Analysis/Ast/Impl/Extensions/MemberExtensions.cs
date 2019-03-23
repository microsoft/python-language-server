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

using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

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

        public static IPythonType GetPythonType(this IMember m) {
            switch (m) {
                case IPythonType pt:
                    return pt;
                case IPythonInstance pi:
                    return pi.Type;
                case IVariable v when v.Value != null:
                    return v.Value.GetPythonType();
            }
            return null;
        }

        public static T GetPythonType<T>(this IMember m) where T : class, IPythonType
            => m.GetPythonType() as T;

        public static bool IsGeneric(this IMember m) {
            var t = m.GetPythonType();
            if (t is IGenericType || t is IGenericTypeDefinition) {
                return true;
            }
            if (t is IPythonClassType c && c.IsGeneric()) {
                return true;
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

        public static void AddReference(this IMember m, IPythonModule module, Node location) {
            Check.ArgumentNotNull(nameof(module), module);
            Check.ArgumentNotNull(nameof(location), location);
            // If member is a variable and its value is not a located member, then add reference to the variable.
            // If the variable value is located member AND variable name is the same as the value member
            // name, then user the latter. Example: function A is declared as variable A that hold type info
            // of the function so reference is to the function. Another example: class member that is 'int'
            // is 'an instance of int' and instances don't have locations. So the holding variable is the located
            // member. On the other hand, function definition is a variable in scope and value of the variable
            // is the function type info which tracks the location and references.
            ILocatedMember lm;
            if (m is IVariable v && v.Value is ILocatedMember x && (v.Name.EqualsOrdinal(x.GetPythonType()?.Name) || v.Definition.DocumentUri == null)) {
                // Variable is not user-declared and rather is holder of a function or class definition.
                lm = x;
            } else {
                lm = m as ILocatedMember;
            }
            lm?.AddReference(module, location);
        }
    }
}
