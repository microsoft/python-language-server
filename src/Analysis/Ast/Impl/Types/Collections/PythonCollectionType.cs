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
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types.Collections {
    /// <summary>
    /// Type info for an iterable entity. Most base collection class.
    /// </summary>
    internal class PythonCollectionType : PythonTypeWrapper, IPythonCollectionType {
        /// <summary>
        /// Creates type info for an collection.
        /// </summary>
        /// <param name="collectionTypeId">Collection type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="declaringModule">Declaring module.</param>
        /// <param name="isMutable">Indicates if collection is mutable (like list) or immutable (like tuple).</param>
        public PythonCollectionType(
            BuiltinTypeId collectionTypeId,
            IPythonModule declaringModule,
            bool isMutable
            ) : base(collectionTypeId, declaringModule) {
            TypeId = collectionTypeId;
            IteratorType = new PythonIteratorType(collectionTypeId.GetIteratorTypeId(), declaringModule);
            IsMutable = isMutable;
        }

        #region IPythonCollectionType
        /// <summary>
        /// Indicates if collection is mutable (such as list) or not (such as tuple).
        /// </summary>
        public bool IsMutable { get; }
        public virtual IPythonIterator GetIterator(IPythonInstance instance) => (instance as IPythonCollection)?.GetIterator();
        public IPythonIteratorType IteratorType { get; }
        #endregion

        #region IPythonType
        public override BuiltinTypeId TypeId { get; }
        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IMember GetMember(string name) => name == @"__iter__" ? IteratorType : base.GetMember(name);

        public override IMember CreateInstance(string typeName, IArgumentSet args)
            => new PythonCollection(this, args.Arguments.Select(a => a.Value).OfType<IMember>().ToArray());

        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args)
            => DeclaringModule.Interpreter.GetBuiltinType(TypeId)?.Call(instance, memberName, args);

        public override IMember Index(IPythonInstance instance, IArgumentSet args)
            => (instance as IPythonCollection)?.Index(args) ?? UnknownType;

        public IPythonType CreateSpecificType(IArgumentSet typeArguments) {
            throw new NotImplementedException();
        }

        #endregion

        #region IGenericType
        public IReadOnlyList<IGenericTypeParameter> Parameters => (InnerType as IGenericType)?.Parameters ?? Array.Empty<IGenericTypeParameter>();
        public bool IsGeneric => (InnerType as IPythonClassType)?.IsGeneric == true;
        public IReadOnlyDictionary<string, IPythonType> GenericParameters
            => (InnerType as IPythonClassType)?.GenericParameters ?? EmptyDictionary<string, IPythonType>.Instance;
        #endregion

        #region IPythonClassType
        public IPythonType DeclaringType => (InnerType as IPythonClassType)?.DeclaringType;
        public ClassDefinition ClassDefinition => (InnerType as IPythonClassType)?.ClassDefinition;
        public IReadOnlyList<IPythonType> Mro => (InnerType as IPythonClassType)?.Mro ?? Array.Empty<IPythonType>();
        public IReadOnlyList<IPythonType> Bases => (InnerType as IPythonClassType)?.Bases ?? Array.Empty<IPythonType>();
        #endregion

        public static IPythonCollection CreateList(IPythonModule declaringModule, IArgumentSet args) {
            var exact = true;
            IReadOnlyList<IMember> contents;
            if (args.Arguments.Count > 1) {
                // self and list like in list.__init__ and 'list([1, 'str', 3.0])'
                var arg = args.Arguments[1].Value as PythonCollection;
                exact = arg?.IsExact ?? false;
                contents = arg?.Contents;
            } else {
                contents = args.ListArgument?.Values;
            }
            return CreateList(declaringModule, contents ?? Array.Empty<IMember>(), exact: exact);
        }

        public static IPythonCollection CreateList(IPythonModule declaringModule, IReadOnlyList<IMember> contents, bool flatten = true, bool exact = false) {
            var collectionType = new PythonCollectionType(BuiltinTypeId.List, declaringModule, true);
            return new PythonCollection(collectionType, contents, flatten, exact: exact);
        }

        public static IPythonCollection CreateConcatenatedList(IPythonModule declaringModule, params IPythonCollection[] many) {
            var exact = many?.All(c => c != null && c.IsExact) ?? false;
            var contents = many?.ExcludeDefault().Select(c => c.Contents).SelectMany().ToList() ?? new List<IMember>();
            return CreateList(declaringModule, contents, false, exact: exact);
        }

        public static IPythonCollection CreateTuple(IPythonModule declaringModule, IReadOnlyList<IMember> contents, bool exact = false) {
            var collectionType = new PythonCollectionType(BuiltinTypeId.Tuple, declaringModule, false);
            return new PythonCollection(collectionType, contents, exact: exact);
        }

        public static IPythonCollection CreateConcatenatedTuple(IPythonModule declaringModule, params IPythonCollection[] many) {
            var exact = many?.All(c => c != null && c.IsExact) ?? false;
            var contents = many?.ExcludeDefault().Select(c => c.Contents).SelectMany().ToList() ?? new List<IMember>();
            return CreateTuple(declaringModule, contents, exact: exact);
        }

        public static IPythonCollection CreateSet(IPythonModule declaringModule, IReadOnlyList<IMember> contents, bool flatten = true, bool exact = false) {
            var collectionType = new PythonCollectionType(BuiltinTypeId.Set, declaringModule, true);
            return new PythonCollection(collectionType, contents, flatten, exact: exact);
        }

        public override bool Equals(object obj)
            => obj is IPythonType pt && (PythonTypeComparer.Instance.Equals(pt, this) || PythonTypeComparer.Instance.Equals(pt, InnerType));
        public override int GetHashCode() => PythonTypeComparer.Instance.GetHashCode(this);
    }
}
