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
using System.Diagnostics;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Represents an instance of type or the type information.
    /// Actual instance has <see cref="MemberType"/> set to <see cref="PythonMemberType.Instance"/>.
    /// Type information is marked as the type it describes, such as <see cref="PythonMemberType.Class"/>.
    /// </summary>
    [DebuggerDisplay("Instance of {Type.Name}")]
    internal class PythonInstance : IPythonInstance {
        public PythonInstance(IPythonType type, LocationInfo location = null) {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Location = location ?? LocationInfo.Empty;
        }

        public virtual IPythonType Type { get; }
        public LocationInfo Location { get; }
        public virtual PythonMemberType MemberType => PythonMemberType.Instance;
        public virtual IMember Call(string memberName, params object[] args) => Type.GetMember(memberName);
        public virtual IMember Index(object index) => Type.DeclaringModule.Interpreter.UnknownType;
    }
}
