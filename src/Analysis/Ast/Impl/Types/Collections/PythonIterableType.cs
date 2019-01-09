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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types.Collections {
    /// <summary>
    /// Type info for an iterable entity. Most base collection class.
    /// </summary>
    internal abstract class PythonIterableType : PythonTypeWrapper, IPythonIterableType {
        private readonly PythonIteratorType _iteratorType;
        private string _typeName;

        protected IReadOnlyList<IPythonType> ContentTypes { get; }

        /// <summary>
        /// Creates type info for an iterable.
        /// </summary>
        /// <param name="typeName">Iterable type name. If null, name of the type id will be used.</param>
        /// <param name="sequenceTypeId">Iterable type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="declaringModule">Declaring module. Can be null of module is 'builtins'.</param>
        /// <param name="contentType">Sequence content type.</param>
        protected PythonIterableType(
            string typeName,
            BuiltinTypeId sequenceTypeId,
            IPythonModule declaringModule,
            IPythonType contentType
            ) : this(typeName, sequenceTypeId, declaringModule,
                     new[] { contentType ?? declaringModule.Interpreter.UnknownType }) { }

        /// <summary>
        /// Creates type info for an iterable.
        /// </summary>
        /// <param name="typeName">Iterable type name. If null, name of the type id will be used.</param>
        /// <param name="sequenceTypeId">Iterable type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="declaringModule">Declaring module. Can be null of module is 'builtins'.</param>
        /// <param name="contentTypes">Sequence content types.</param>
        protected PythonIterableType(
            string typeName,
            BuiltinTypeId sequenceTypeId,
            IPythonModule declaringModule,
            IReadOnlyList<IPythonType> contentTypes
        ) : base(sequenceTypeId, declaringModule) {
            _iteratorType = new PythonIteratorType(sequenceTypeId.GetIteratorTypeId(), DeclaringModule);
            _typeName = typeName;
            ContentTypes = contentTypes ?? Array.Empty<IPythonType>();
        }

        #region IPythonIterableType
        public IPythonIterator GetIterator(IPythonInstance instance) => (instance as IPythonIterable)?.GetIterator();
        #endregion

        #region IPythonType
        public override string Name {
            get {
                if (_typeName == null) {
                    var type = DeclaringModule.Interpreter.GetBuiltinType(TypeId);
                    if(!type.IsUnknown()) {
                        _typeName = type.Name;
                    }
                }
                return _typeName ?? "<not set>";;
            }
        }

        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IMember GetMember(string name) => name == @"__iter__" ? _iteratorType : base.GetMember(name);
        public override bool IsAbstract => true;
        #endregion
    }
}
