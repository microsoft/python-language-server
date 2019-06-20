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

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class AnyType : LocatedMember, IPythonType {
        public AnyType(IPythonModule declaringModule) : base(declaringModule) { }

        public override PythonMemberType MemberType => PythonMemberType.Class;
        public string Name => "Any";
        public string QualifiedName => this.GetQualifiedName();
        public BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public string Documentation => Name;
        public bool IsBuiltin => false;
        public bool IsAbstract => false;
        public bool IsSpecialized => true;

        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args)
            => DeclaringModule.Interpreter.UnknownType;
        public IMember CreateInstance(string typeName, IArgumentSet args) => new PythonInstance(this);

        public IMember GetMember(string name) => null;
        public IEnumerable<string> GetMemberNames() => Array.Empty<string>();

        public IMember Index(IPythonInstance instance, object index)
            => DeclaringModule.Interpreter.UnknownType;
    }
}
