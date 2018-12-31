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
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    /// <summary>
    /// Base class for generic types and type declarations.
    /// </summary>
    internal class GenericType : IGenericType {
        private readonly Func<IReadOnlyList<IPythonType>, IPythonModule, LocationInfo, IPythonType> _typeConstructor;
        protected IPythonModule TypingModule { get; }

        public GenericType(string name, IPythonModule declaringModule, 
            Func<IReadOnlyList<IPythonType>, IPythonModule, LocationInfo, IPythonType> typeConstructor) {

            Name = name ?? throw new ArgumentNullException(nameof(name));
            DeclaringModule = declaringModule ?? throw new ArgumentNullException(nameof(declaringModule));
            _typeConstructor = typeConstructor ?? throw new ArgumentNullException(nameof(typeConstructor));

            TypingModule = DeclaringModule.Interpreter.ModuleResolution.GetSpecializedModule("typing");
            Debug.Assert(TypingModule != null, "Typing must be specialized for generic types to work.");
        }

        public IPythonType CreateSpecificType(IReadOnlyList<IPythonType> typeArguments, IPythonModule declaringModule, LocationInfo location = null)
            => _typeConstructor(typeArguments, declaringModule, location);

        #region IPythonType
        public string Name { get; }
        public IPythonModule DeclaringModule { get; }
        public PythonMemberType MemberType => PythonMemberType.Generic;
        public IMember GetMember(string name) => null;
        public IEnumerable<string> GetMemberNames() => Enumerable.Empty<string>();
        public BuiltinTypeId TypeId => BuiltinTypeId.Unknown;
        public virtual string Documentation => (TypingModule?.GetMember(Name) as IPythonType)?.Documentation;
        public bool IsBuiltin => false;
        public bool IsAbstract => true;

        public IMember CreateInstance(IPythonModule declaringModule, LocationInfo location, params object[] args) {
            var types = args.OfType<IPythonType>().ToArray();
            if (types.Length != args.Length) {
                throw new ArgumentException(@"Generic type instance construction arguments must be all of IPythonType", nameof(args));
            }
            var specific = CreateSpecificType(types, DeclaringModule, location);
            return specific == null 
                ? DeclaringModule.Interpreter.UnknownType 
                : specific.CreateInstance(declaringModule, location);
        }
        #endregion
    }
}
