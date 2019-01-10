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
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal class TypingListType : PythonCollectionType, ITypingListType {
        /// <summary>
        /// Creates type info for a list-type typed collection.
        /// </summary>
        /// <param name="typeName">Collection type name. </param>
        /// <param name="sequenceTypeId">Collection type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="declaringModule">Declaring module. Can be null of module is 'builtins'.</param>
        /// <param name="itemType">List item type.</param>
        /// <param name="isMutable">Indicates if collection is mutable.</param>
        public TypingListType(
            string typeName,
            BuiltinTypeId sequenceTypeId,
            IPythonModule declaringModule,
            IPythonType itemType,
            bool isMutable
            ) : base(typeName, sequenceTypeId, declaringModule, isMutable) {
            Check.ArgumentNotNullOrEmpty(typeName, nameof(typeName));
            ItemType = itemType;
            Name = $"{typeName}[{itemType.Name}]";
        }

        public override string Name { get; }
        public override bool IsAbstract => false;
        public override IMember CreateInstance(string typeName, LocationInfo location, IReadOnlyList<object> args) => new TypingList(this, location);
        public IPythonType ItemType { get; }
    }
}
