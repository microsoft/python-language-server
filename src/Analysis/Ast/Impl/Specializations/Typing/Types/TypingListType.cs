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

using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal class TypingListType : PythonCollectionType, ITypingListType {
        /// <summary>
        /// Creates type info for a list-like strongly typed collection, such as List[T].
        /// </summary>
        /// <param name="typeName">Type name.</param>
        /// <param name="itemType">List item type.</param>
        /// <param name="interpreter">Python interpreter</param>
        /// <param name="isMutable">Tells of list represents a mutable collection.</param>
        /// <param name="formatName">If true, type will append item type names to the base type name.</param>
        public TypingListType(string typeName, IPythonType itemType, IPythonInterpreter interpreter, bool isMutable, bool formatName = true) 
            : this(typeName, BuiltinTypeId.List, itemType, interpreter, isMutable, formatName) { }

        /// <summary>
        /// Creates type info for a list-like strongly typed collection, such as List[T].
        /// </summary>
        /// <param name="typeName">Type name.</param>
        /// <param name="typeId">Collection type id. Can be used when list is used to simulate other collections, like a set.</param>
        /// <param name="itemType">List item type.</param>
        /// <param name="interpreter">Python interpreter</param>
        /// <param name="isMutable">Tells of list represents a mutable collection.</param>
        /// <param name="formatName">If true, type will append item type names to the base type name.</param>
        public TypingListType(string typeName, BuiltinTypeId typeId, IPythonType itemType, IPythonInterpreter interpreter, bool isMutable, bool formatName = true)
            : base(null, typeId, interpreter, isMutable) {
            ItemType = itemType;
            Name = formatName ? $"{typeName}[{itemType.Name}]" : typeName;
        }

        public override string Name { get; }
        public override bool IsAbstract => false;
        public override bool IsSpecialized => true;

        public override IMember CreateInstance(string typeName, IArgumentSet args) => new TypingList(this);
        public IPythonType ItemType { get; }

        public override IMember Index(IPythonInstance instance, IArgumentSet args) => new PythonInstance(ItemType);

        public override bool Equals(object obj) {
            if (!(obj is TypingListType other)) {
                return false;
            }

            if (ItemType.IsGenericParameter() || other.ItemType.IsGenericParameter()) {
                // Generic match - i.e. Sequence[T] matches List[str].
                return true;
            }
            return PythonTypeComparer.Instance.Equals(ItemType, other.ItemType);
        }

        public override int GetHashCode() => ItemType.GetHashCode() ^ Name.GetHashCode();
    }
}
