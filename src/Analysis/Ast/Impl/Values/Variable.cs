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
using System.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Values {
    [DebuggerDisplay("{DebuggerDisplay}")]
    internal sealed class Variable : LocatedMember, IVariable {
        public Variable(string name, IMember value, VariableSource source, IPythonModule declaringModule, IndexSpan location)
            : base(PythonMemberType.Variable, declaringModule, location) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value is IVariable v ? v.Value : value;
            Source = source;
        }

        public Variable(string name, IVariable parent, IPythonModule declaringModule, IndexSpan location)
            : base(PythonMemberType.Variable, declaringModule, location, parent) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = parent.Value;
            Source = VariableSource.Import;
        }

        #region IVariable
        public string Name { get; }
        public VariableSource Source { get; }
        public IMember Value { get; private set; }

        public void Assign(IMember value, IPythonModule module, IndexSpan location) {
            if (value is IVariable v) {
                value = v.Value;
            }
            if (Value == null || Value.GetPythonType().IsUnknown() || value?.GetPythonType().IsUnknown() == false) {
                Debug.Assert(!(value is IVariable));
                Value = value;
            }
            AddReference(module, location);
        }
        #endregion

        #region ILocatedMember
        public override LocationInfo Definition
            => Location.Module != null && Location.IndexSpan != default
                ? base.Definition
                : (Value as ILocatedMember)?.Definition ?? LocationInfo.Empty;

        public override IReadOnlyList<LocationInfo> References
            => Location.Module != null && Location.IndexSpan != default
                ? base.References
                : (Value as ILocatedMember)?.References;

        public override void AddReference(IPythonModule module, IndexSpan location) {
            if (module == null || location == default) {
                return;
            }
            // If value is not a located member, then add reference to the variable.
            // If variable name is the same as the value member name, then the variable
            // is implicit declaration (like declared function or a class) and we need
            // to add reference to the actual type instead.
            if (Value is ILocatedMember lm && (Name.EqualsOrdinal(lm.GetPythonType()?.Name) || Location.IndexSpan == default)) {
                // Variable is not user-declared and rather is holder of a function or class definition.
                lm.AddReference(module, location);
            } else {
                base.AddReference(module, location);
            }
            Parent?.AddReference(module, location);
        }

        public override void RemoveReferences(IPythonModule module) {
            if (module == null) {
                return;
            }
            if (Value is ILocatedMember lm && (Name.EqualsOrdinal(lm.GetPythonType()?.Name) || Location.IndexSpan == default)) {
                // Variable is not user-declared and rather is holder of a function or class definition.
                lm.RemoveReferences(module);
            } else {
                base.RemoveReferences(module);
            }
            Parent?.RemoveReferences(module);
        }
        #endregion

        private string DebuggerDisplay {
            get {
                switch (Value) {
                    case IPythonInstance pi:
                        return $"{Name} : instance of {pi.Type.Name}";
                    case IPythonType pt:
                        return $"{Name} : typeInfo of {pt.Name}";
                    default:
                        return $"{Name} : member {Value?.MemberType}";
                }
            }
        }
    }
}
