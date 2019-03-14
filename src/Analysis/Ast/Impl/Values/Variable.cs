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

namespace Microsoft.Python.Analysis.Values {
    [DebuggerDisplay("{DebuggerDisplay}")]
    internal sealed class Variable : IVariable {
        private List<LocationInfo> _locations;

        public Variable(string name, IMember value, VariableSource source, LocationInfo location = null) {
            Name = name;
            Value = value;
            Source = source;
            Location = location ?? (value is ILocatedMember lm ? lm.Location : LocationInfo.Empty);
        }

        public string Name { get; }
        public VariableSource Source { get; }
        public IMember Value { get; private set; }
        public LocationInfo Location { get; }
        public PythonMemberType MemberType => PythonMemberType.Variable;
        public IReadOnlyList<LocationInfo> Locations => _locations as IReadOnlyList<LocationInfo> ?? new[] { Location };

        public void Assign(IMember value, LocationInfo location) {
            if (Value == null || Value.GetPythonType().IsUnknown() || value?.GetPythonType().IsUnknown() == false) {
                Value = value;
            }
            AddLocation(location);
        }

        private void AddLocation(LocationInfo location) {
            _locations = _locations ?? new List<LocationInfo> { Location };
            _locations.Add(location);
        }

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
