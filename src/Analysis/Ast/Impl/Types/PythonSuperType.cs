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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class PythonSuperType : PythonType, IPythonClassType, IPythonInstance {
        private IReadOnlyList<IPythonType> _mro;
        public PythonSuperType(Location location, IReadOnlyList<IPythonType> mro) 
            : base("super", location, string.Empty, BuiltinTypeId.Type) {
            _mro = mro;
        }

        public override IPythonInstance CreateInstance(IArgumentSet args) => this;

        public IPythonType Type => this;

        public ClassDefinition ClassDefinition => throw new System.NotImplementedException();

        public IReadOnlyList<IPythonType> Mro => throw new System.NotImplementedException();

        public IReadOnlyList<IPythonType> Bases => throw new System.NotImplementedException();

        public IReadOnlyDictionary<string, IPythonType> GenericParameters => throw new System.NotImplementedException();

        public IPythonType DeclaringType => throw new System.NotImplementedException();

        public IReadOnlyList<IGenericTypeParameter> Parameters => throw new System.NotImplementedException();

        public bool IsGeneric => false;

        public IMember Call(string memberName, IArgumentSet args) => DeclaringModule.Interpreter.UnknownType;

        public IPythonIterator GetIterator() => new EmptyIterator(DeclaringModule.Interpreter.UnknownType);

        public IMember Index(IArgumentSet args) => DeclaringModule.Interpreter.UnknownType;

        public IPythonType CreateSpecificType(IArgumentSet typeArguments) {
            throw new System.NotImplementedException();
        }

        public override IMember GetMember(string name) {
            foreach (var cls in Mro?.Skip(1).MaybeEnumerate()) {
                var member = cls.GetMember(name);
                if (member != null) {
                    return member;
                }
            }

            return null;
        }
        public override IEnumerable<string> GetMemberNames() {

        }
    }
}

