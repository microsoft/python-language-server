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
using System.Diagnostics;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Type info for a sequence.
    /// </summary>
    internal abstract class PythonSequenceType : PythonTypeWrapper, IPythonSequenceType {
        private readonly PythonIteratorType _iteratorType;

        /// <summary>
        /// Creates type info for a sequence.
        /// </summary>
        /// <param name="typeName">Sequence type name. If null, name of the type id will be used.</param>
        /// <param name="sequenceTypeId">Sequence type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="declaringModule">Declaring module. Can be null of module is 'builtins'.</param>
        /// <param name="contentType">Sequence content type.</param>
        /// <param name="isMutable">Defines if the sequence is mutable.</param>
        protected PythonSequenceType(
            string typeName,
            BuiltinTypeId sequenceTypeId,
            IPythonModule declaringModule,
            IPythonType contentType,
            bool isMutable
            ) : this(typeName, sequenceTypeId, declaringModule, 
                     new[] { contentType ?? declaringModule.Interpreter.UnknownType }, isMutable) { }

        /// <summary>
        /// Creates type info for a sequence.
        /// </summary>
        /// <param name="typeName">Sequence type name. If null, name of the type id will be used.</param>
        /// <param name="sequenceTypeId">Sequence type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="declaringModule">Declaring module. Can be null of module is 'builtins'.</param>
        /// <param name="contentTypes">Sequence content types.</param>
        /// <param name="isMutable">Defines if the sequence is mutable.</param>
        protected PythonSequenceType(
            string typeName,
            BuiltinTypeId sequenceTypeId,
            IPythonModule declaringModule,
            IReadOnlyList<IPythonType> contentTypes,
            bool isMutable
        ) : base(declaringModule.Interpreter.GetBuiltinType(sequenceTypeId), declaringModule) {
            _iteratorType = new PythonIteratorType(sequenceTypeId.GetIteratorTypeId(), DeclaringModule);
            Name = typeName ?? declaringModule.Interpreter.GetBuiltinType(sequenceTypeId).Name;
            IsMutable = isMutable;
        }

        #region IPythonSequenceType
        /// <summary>
        /// Indicates if collection is mutable (such as list) or not (such as tuple).
        /// </summary>
        public bool IsMutable { get; }
        #endregion

        #region IPythonType
        public override string Name { get; }
        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IMember GetMember(string name) => name == @"__iter__" ? _iteratorType : base.GetMember(name);

        public override IMember CreateInstance(IPythonModule declaringModule, LocationInfo location, params object[] args) {
            Debug.Fail("Attempt to create instance of an abstract sequence type.");
            return null;
        }
        #endregion
    }
}
