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
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    [DebuggerDisplay("{DebuggerDisplay}")]
    internal sealed class Variable : LocatedMember, IVariable {
        private readonly List<Node> _references = new List<Node>();

        public Variable(string name, IMember value, VariableSource source, IPythonModule declaringModule, Node location)
            : base(PythonMemberType.Variable, declaringModule, location) {
            Check.ArgumentNotNull(nameof(location), location);
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Source = source;
        }

        public string Name { get; }
        public VariableSource Source { get; }
        public IMember Value { get; private set; }

        public void Assign(IMember value, Node location) {
            Check.ArgumentNotNull(nameof(value), value);
            Check.ArgumentNotNull(nameof(location), location);

            if (Value == null || Value.GetPythonType().IsUnknown() || value?.GetPythonType().IsUnknown() == false) {
                Value = value;
            }
            AddReference(location);
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
