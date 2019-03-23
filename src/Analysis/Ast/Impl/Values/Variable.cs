﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    [DebuggerDisplay("{DebuggerDisplay}")]
    internal sealed class Variable : LocatedMember, IVariable {
        public Variable(string name, IMember value, VariableSource source, IPythonModule declaringModule, Node location)
            : base(PythonMemberType.Variable, declaringModule, location) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
            Source = source;
        }

        #region IVariable
        public string Name { get; }
        public VariableSource Source { get; }
        public IMember Value { get; private set; }

        public void Assign(IMember value, IPythonModule module, Node location) {
            if (Value == null || Value.GetPythonType().IsUnknown() || value?.GetPythonType().IsUnknown() == false) {
                Value = value;
            }
            AddReference(module, location);
        }
        #endregion

        #region ILocatedMember
        public override LocationInfo Definition
            => base.Definition.DocumentUri != null ? base.Definition : (Value as ILocatedMember)?.Definition;
        public override IReadOnlyList<LocationInfo> References
            => base.Definition.DocumentUri != null ? base.References : (Value as ILocatedMember)?.References;
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
