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

using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Specializations {
    internal sealed class Type : SpecializedClass {
        public Type(IPythonModule declaringModule) : base(BuiltinTypeId.Type, declaringModule) { }

        public override PythonMemberType MemberType => PythonMemberType.Class;

        public override IMember CreateInstance(IArgumentSet args) {
            var argMembers = args.Values<IMember>();
            // type(self, ...)
            return argMembers.Count > 1 ? argMembers[1].GetPythonType().ToBound() : this;
        }
    }
}
