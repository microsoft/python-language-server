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
    internal class Variable : LocatedMember, IVariable {
        public Variable(string name, IMember value, VariableSource source, Location location) : base(location) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value is IVariable v ? v.Value : value;
            Source = source;
        }

        #region IVariable
        public string Name { get; }
        public VariableSource Source { get; }
        public IMember Value { get; private set; }
        public bool IsClassMember { get; internal set; }

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
        public override PythonMemberType MemberType => PythonMemberType.Variable;

        public override LocationInfo Definition {
            get {
                if (!Location.IsValid) {
                    var lmv = GetImplicitlyDeclaredValue();
                    if (lmv != null) {
                        return lmv.Definition;
                    }
                }
                return base.Definition ?? LocationInfo.Empty;
            }
        }

        public override IReadOnlyList<LocationInfo> References {
            get {
                if (!Location.IsValid) {
                    var lmv = GetImplicitlyDeclaredValue();
                    if (lmv != null) {
                        return lmv.References;
                    }
                }
                return base.References ?? Array.Empty<LocationInfo>();
            }
        }

        public override void AddReference(Location location) {
            if (location.IsValid) {
                if (!AddOrRemoveReference(lm => lm?.AddReference(location))) {
                    base.AddReference(location);
                }
            }
        }

        public override void RemoveReferences(IPythonModule module) {
            if (module == null) {
                return;
            }
            if (!AddOrRemoveReference(lm => lm?.RemoveReferences(module))) {
                base.RemoveReferences(module);
            }
        }

        protected virtual bool AddOrRemoveReference(Action<ILocatedMember> action) {
            // Values:
            //   a) If value is not a located member, then add reference to the variable.
            //   b) If variable name is the same as the value member name, then the variable
            //      is implicit declaration (like declared function or a class) and we need
            //      to add reference to the actual type instead.
            var lmv = GetImplicitlyDeclaredValue();
            if (lmv != null) {
                // Variable is not user-declared and rather is holder of a function or class definition.
                action(lmv);
                return true;
            }
            return false;
        }

        #endregion

        private ILocatedMember GetImplicitlyDeclaredValue()
            => Value is ILocatedMember lm && Name.EqualsOrdinal(lm.GetPythonType()?.Name) && !Location.IsValid ? lm : null;

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
