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
    internal sealed class ImportedVariable : Variable, IImportedMember {
        public ImportedVariable(string name, IVariable parent, Location location)
            : base(name, parent.Value, VariableSource.Import, location) {
            Parent = parent;
            Parent?.AddReference(location);
        }

        #region IImportedMember
        public ILocatedMember Parent { get; }
        #endregion

        #region ILocatedMember
        public override LocationInfo Definition {
            get {
                if (!Location.IsValid && Parent != null) {
                    return Parent?.Definition ?? LocationInfo.Empty;
                }
                return base.Definition;
            }
        }

        public override IReadOnlyList<LocationInfo> References {
            get {
                if (!Location.IsValid && Parent != null) {
                    return Parent?.References ?? Array.Empty<LocationInfo>();
                }
                return base.References;
            }
        }

        protected override bool AddOrRemoveReference(Action<ILocatedMember> action) {
            // Variable can be 
            //   a) Declared locally in the module. In this case it has non-default
            //      definition location and no parent (link).
            //   b) Imported from another module via 'from module import X'.
            //      In this case it has non-default definition location and non-null parent (link).
            //   c) Imported from another module via 'from module import *'.
            //      In this case it has default location (which means it is not explicitly declared)
            //      and the non-null parent (link).
            var explicitlyDeclared = Location.IsValid;
            if (!explicitlyDeclared && Parent != null) {
                action(Parent);
                return true;
            }
            // Explicitly declared. 
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
            action(Parent);
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
