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
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Specializations.Typing.Values {
    internal abstract class SpecializedClass: PythonTypeWrapper, IPythonClassType {
        protected SpecializedClass(BuiltinTypeId typeId, IPythonModule declaringModule) 
            : base(typeId, declaringModule) {
        }

        #region IPythonClassType
        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet argSet) => this;
        public override IMember Index(IPythonInstance instance, IArgumentSet args) => null;
        public IPythonType CreateSpecificType(IArgumentSet typeArguments) => this;
        public IPythonType DeclaringType => null;
        public IReadOnlyList<IGenericTypeParameter> Parameters => Array.Empty<IGenericTypeParameter>();
        public bool IsGeneric => false;
        public ClassDefinition ClassDefinition => null;
        public IReadOnlyList<IPythonType> Mro => Array.Empty<IPythonType>();
        public IReadOnlyList<IPythonType> Bases => new[] { DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Object) };
        public IReadOnlyDictionary<string, IPythonType> GenericParameters => EmptyDictionary<string, IPythonType>.Instance;
        #endregion
    }
}
