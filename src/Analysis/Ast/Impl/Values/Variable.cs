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

namespace Microsoft.Python.Analysis.Values {
    [DebuggerDisplay("{DebuggerDisplay}")]
    internal sealed class Variable : LocatedMember, IVariable {
        public Variable(string name, IMember value, VariableSource source, Location location)
            : base(PythonMemberType.Variable, location) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value is IVariable v ? v.Value : value;
            Source = source;
        }

        public Variable(string name, IVariable parent, Location location)
            : base(PythonMemberType.Variable, location, parent) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = parent.Value;
            Source = VariableSource.Import;
        }

        #region IVariable
        public string Name { get; }
        public VariableSource Source { get; }
        public IMember Value { get; private set; }

        public void Assign(IMember value, Location location) {
            if (value is IVariable v) {
                value = v.Value;
            }
            if (Value == null || Value.GetPythonType().IsUnknown() || value?.GetPythonType().IsUnknown() == false) {
                Debug.Assert(!(value is IVariable));
                Value = value;
            }
            AddReference(location);
        }
        #endregion

        #region ILocatedMember
        public override LocationInfo Definition 
            => GetValueMember()?.Definition ?? base.Definition ?? LocationInfo.Empty;

        public override IReadOnlyList<LocationInfo> References
            => GetValueMember()?.References ?? base.References ?? Array.Empty<LocationInfo>();

        public override void AddReference(Location location) {
            if (location.Module == null || location.IndexSpan == default) {
                return;
            }
            // If value is not a located member, then add reference to the variable.
            // If variable name is the same as the value member name, then the variable
            // is implicit declaration (like declared function or a class) and we need
            // to add reference to the actual type instead.
            var lm = GetValueMember();
            if (lm != null) {
                // Variable is not user-declared and rather is holder of a function or class definition.
                lm.AddReference(location);
            } else {
                base.AddReference(location);
            }
            Parent?.AddReference(location);
        }

        public override void RemoveReferences(IPythonModule module) {
            if (module == null) {
                return;
            }

            var lm = GetValueMember();
            if (lm != null) {
                // Variable is not user-declared and rather is holder of a function or class definition.
                lm.RemoveReferences(module);
            } else {
                base.RemoveReferences(module);
            }
            Parent?.RemoveReferences(module);
        }
        #endregion

        private ILocatedMember GetValueMember()
            => Value is ILocatedMember lm && (Name.EqualsOrdinal(lm.GetPythonType()?.Name) || Location.IndexSpan == default) ? lm : null;

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
