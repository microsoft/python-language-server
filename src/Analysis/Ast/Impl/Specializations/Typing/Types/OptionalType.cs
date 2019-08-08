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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class OptionalType : PythonTypeWrapper, IPythonUnionType {
        public OptionalType(IPythonModule declaringModule, IPythonType type) : base(type, declaringModule) {
            Name = $"Optional[{type.Name}]";
            QualifiedName = $"typing:Optional[{type.QualifiedName}]";
        }
        public override string Name { get; }
        public override string QualifiedName { get; }
        public override PythonMemberType MemberType => PythonMemberType.Union;
        public override bool IsSpecialized => true;

        public IEnumerator<IPythonType> GetEnumerator()
            => Enumerable.Repeat(DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.NoneType), 1)
                .Concat(Enumerable.Repeat(InnerType, 1)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IPythonUnionType Add(IPythonType t) => this;
        public IPythonUnionType Add(IPythonUnionType types) => this;

        public override IMember CreateInstance(string typeName, IArgumentSet args)
            => InnerType.CreateInstance(typeName, args);
    }
}
