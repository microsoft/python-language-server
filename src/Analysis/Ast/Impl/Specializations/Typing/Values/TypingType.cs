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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Values {
    /// <summary>
    /// Holds type info of the generic parameter.
    /// </summary>
    internal sealed class TypingType : IPythonType {
        private readonly IPythonType _type;

        public TypingType(IPythonModule declaringModule, IPythonType type) {
            _type = type ?? throw  new ArgumentNullException(nameof(type));
            DeclaringModule = declaringModule;
            Name = $"Type[{_type.Name}]";
        }

        public string Name { get; }
        public IPythonModule DeclaringModule { get; }
        public BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public string Documentation => Name;
        public bool IsBuiltin => false;
        public bool IsAbstract => false;
        public PythonMemberType MemberType => PythonMemberType.Class;
        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => _type.Call(instance, memberName, args);
        public IMember CreateInstance(string typeName, LocationInfo location, IArgumentSet args ) => _type;
        public IMember GetMember(string name) => _type.GetMember(name);
        public IEnumerable<string> GetMemberNames() => _type.GetMemberNames();
        public IMember Index(IPythonInstance instance, object index) => _type.Index(instance, index);
    }
}
