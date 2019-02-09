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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal delegate IPythonType SpecificTypeConstructor(
        IReadOnlyList<IPythonType> typeArgs, 
        IPythonModule declaringModule, 
        LocationInfo location);

    /// <summary>
    /// Base class for generic types and type declarations.
    /// </summary>
    internal class GenericType : IGenericType {
        private readonly SpecificTypeConstructor _specificTypeConstructor;

        /// <summary>
        /// Constructs generic type with generic parameters. Typically used
        /// in generic classes such as when handling Generic[_T] base.
        /// </summary>
        public GenericType(string name, IPythonModule declaringModule, IReadOnlyList<IGenericTypeParameter> parameters)
            : this(name, declaringModule) {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        /// <summary>
        /// Constructs generic type with dynamic type constructor.
        /// Typically used in type specialization scenarios.
        /// </summary>
        public GenericType(string name, IPythonModule declaringModule, SpecificTypeConstructor specificTypeConstructor)
            : this(name, declaringModule) {
            _specificTypeConstructor = specificTypeConstructor ?? throw new ArgumentNullException(nameof(specificTypeConstructor));
        }

        private GenericType(string name, IPythonModule declaringModule) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DeclaringModule = declaringModule ?? throw new ArgumentNullException(nameof(declaringModule));
        }

        /// <summary>
        /// Type parameters such as in Tuple[T1, T2. ...] or
        /// Generic[_T1, _T2, ...] as returned by TypeVar.
        /// </summary>
        public IReadOnlyList<IGenericTypeParameter> Parameters { get; } = Array.Empty<IGenericTypeParameter>();

        /// <summary>
        /// Creates instance of a type information with the specific
        /// type arguments from a generic template.
        /// </summary>
        public IPythonType CreateSpecificType(IReadOnlyList<IPythonType> typeArguments, IPythonModule declaringModule, LocationInfo location = null)
            => _specificTypeConstructor(typeArguments, declaringModule, location);

        #region IPythonType
        public string Name { get; }
        public IPythonModule DeclaringModule { get; }
        public PythonMemberType MemberType => PythonMemberType.Generic;
        public IMember GetMember(string name) => null;
        public IEnumerable<string> GetMemberNames() => Enumerable.Empty<string>();
        public BuiltinTypeId TypeId => BuiltinTypeId.Unknown;
        public virtual string Documentation => Name;
        public bool IsBuiltin => false;
        public bool IsAbstract => true;
        public bool IsSpecialized => true;

        public IMember CreateInstance(string typeName, LocationInfo location, IArgumentSet args) {
            var types = args.Values<IPythonType>();
            if (types.Count != args.Arguments.Count) {
                throw new ArgumentException(@"Generic type instance construction arguments must be all of IPythonType", nameof(args));
            }
            var specific = CreateSpecificType(types, DeclaringModule, location);
            return specific == null
                ? DeclaringModule.Interpreter.UnknownType
                : specific.CreateInstance(typeName, location, null);
        }

        public virtual IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => DeclaringModule.Interpreter.UnknownType;
        public virtual IMember Index(IPythonInstance instance, object index) => DeclaringModule.Interpreter.UnknownType;

        public IPythonType CreateSpecificType(IArgumentSet typeArguments, IPythonModule declaringModule, LocationInfo location)
            => CreateSpecificType(typeArguments.Arguments.Select(a => a.Value).OfType<IPythonType>().ToArray(), declaringModule, location);
        #endregion
    }
}
