﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal class TypingListType : PythonCollectionType, ITypingListType {
        /// <summary>
        /// Creates type info for a list-like strongly typed collection, such as List[T].
        /// </summary>
        /// <param name="typeName">Type name.</param>
        /// <param name="itemType">List item type.</param>
        /// <param name="interpreter">Python interpreter</param>
        /// <param name="isMutable">Tells of list represents a mutable collection.</param>
        public TypingListType(string typeName, IPythonType itemType, IPythonInterpreter interpreter, bool isMutable) 
            : this(typeName, BuiltinTypeId.List, itemType, interpreter, isMutable) { }

        /// <summary>
        /// Creates type info for a list-like strongly typed collection, such as List[T].
        /// </summary>
        /// <param name="typeName">Type name.</param>
        /// <param name="typeId">Collection type id. Can be used when list is used to simulate other collections, like a set.</param>
        /// <param name="itemType">List item type.</param>
        /// <param name="interpreter">Python interpreter</param>
        /// <param name="isMutable">Tells of list represents a mutable collection.</param>
        public TypingListType(string typeName, BuiltinTypeId typeId, IPythonType itemType, IPythonInterpreter interpreter, bool isMutable)
            : base(null, BuiltinTypeId.List, interpreter, isMutable) {
            ItemType = itemType;
            Name = $"{typeName}[{itemType.Name}]";
        }

        public override string Name { get; }
        public override bool IsAbstract => false;
        public override IMember CreateInstance(string typeName, LocationInfo location, IReadOnlyList<object> args) 
            => new TypingList(this, location);
        public IPythonType ItemType { get; }

        public override IMember Index(IPythonInstance instance, object index) => new PythonInstance(ItemType);
    }
}
