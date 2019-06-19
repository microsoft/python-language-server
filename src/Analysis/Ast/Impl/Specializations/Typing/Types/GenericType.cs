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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal delegate IPythonType SpecificTypeConstructor(IReadOnlyList<IPythonType> typeArgs);

    /// <summary>
    /// Base class for generic types and type declarations.
    /// </summary>
    internal class GenericType : LocatedMember, IGenericType {
        internal SpecificTypeConstructor SpecificTypeConstructor { get; }

        /// <summary>
        /// Constructs generic type with generic type parameters. Typically used
        /// in generic classes such as when handling Generic[_T] base.
        /// </summary>
        public GenericType(string name, IReadOnlyList<IGenericTypeDefinition> parameters, IPythonModule declaringModule) 
            : this(name, declaringModule) {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        /// <summary>
        /// Constructs generic type with dynamic type constructor.
        /// Typically used in type specialization scenarios.
        /// </summary>
        /// <param name="name">Type name including parameters, such as Iterator[T]</param>
        /// <param name="specificTypeConstructor">Constructor of specific types.</param>
        /// <param name="declaringModule">Declaring module.</param>
        /// <param name="typeId">Type id. Used in type comparisons such as when matching
        /// function arguments. For example, Iterator[T] normally has type id of ListIterator.</param>
        /// <param name="parameters">Optional type parameters as declared by TypeVar.</param>
        public GenericType(
            string name,
            SpecificTypeConstructor specificTypeConstructor,
            IPythonModule declaringModule,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown,
            IReadOnlyList<IGenericTypeDefinition> parameters = null
            ) : this(name, declaringModule) {
            SpecificTypeConstructor = specificTypeConstructor ?? throw new ArgumentNullException(nameof(specificTypeConstructor));
            TypeId = typeId;
            Parameters = parameters ?? Array.Empty<IGenericTypeDefinition>();
        }

        private GenericType(string name, IPythonModule declaringModule) : base(declaringModule) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public override PythonMemberType MemberType => PythonMemberType.Generic;

        /// <summary>
        /// Type parameters such as in Tuple[T1, T2. ...] or
        /// Generic[_T1, _T2, ...] as returned by TypeVar.
        /// </summary>
        public IReadOnlyList<IGenericTypeDefinition> Parameters { get; }

        /// <summary>
        /// Creates instance of a type information with the specific
        /// type arguments from a generic template.
        /// </summary>
        public IPythonType CreateSpecificType(IReadOnlyList<IPythonType> typeArguments)
            => SpecificTypeConstructor(typeArguments);

        #region IPythonType
        public string Name { get; }
        public string QualifiedName => $"{DeclaringModule.Name}:{Name}";
        public IMember GetMember(string name) => null;
        public IEnumerable<string> GetMemberNames() => Enumerable.Empty<string>();
        public BuiltinTypeId TypeId { get; } = BuiltinTypeId.Unknown;
        public virtual string Documentation => Name;
        public bool IsBuiltin => false;
        public bool IsAbstract => true;
        public bool IsSpecialized => true;

        public IMember CreateInstance(string typeName, IArgumentSet args) {
            var types = args.Values<IPythonType>();
            if (types.Count != args.Arguments.Count) {
                throw new ArgumentException(@"Generic type instance construction arguments must be all of IPythonType", nameof(args));
            }
            var specific = CreateSpecificType(types);
            return specific == null
                ? DeclaringModule.Interpreter.UnknownType
                : specific.CreateInstance(typeName);
        }

        public virtual IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => DeclaringModule.Interpreter.UnknownType;
        public virtual IMember Index(IPythonInstance instance, object index) => DeclaringModule.Interpreter.UnknownType;

        public IPythonType CreateSpecificType(IArgumentSet typeArguments)
            => CreateSpecificType(typeArguments.Arguments.Select(a => a.Value).OfType<IPythonType>().ToArray());
        #endregion

        public override bool Equals(object other) {
            if (other == null) {
                return false;
            }
            if (TypeId != BuiltinTypeId.Unknown && other is IPythonType t && t.TypeId == TypeId) {
                return true;
            }
            return this == other;
        }

        public override int GetHashCode()
            => TypeId != BuiltinTypeId.Unknown ? TypeId.GetHashCode() : base.GetHashCode();
    }
}
