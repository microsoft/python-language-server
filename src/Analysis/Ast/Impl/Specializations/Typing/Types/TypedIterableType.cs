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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal abstract class TypedIterableType : PythonIterableType {
        /// <summary>
        /// Creates type info for an iterable.
        /// </summary>
        /// <param name="typeName">Iterable type name. If null, name of the type id will be used.</param>
        /// <param name="sequenceTypeId">Iterable type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="declaringModule">Declaring module. Can be null of module is 'builtins'.</param>
        /// <param name="contentType">Iterable content type.</param>
        protected TypedIterableType(
            string typeName,
            BuiltinTypeId sequenceTypeId,
            IPythonModule declaringModule,
            IPythonType contentType
            ) : base(typeName, sequenceTypeId, declaringModule, contentType) {
            Name = $"{typeName}[{contentType.Name}]";
        }

        public override string Name { get; }
    }
}
