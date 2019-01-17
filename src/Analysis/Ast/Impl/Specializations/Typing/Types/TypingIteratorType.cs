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
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    /// <summary>
    /// Describes iterator for a typed collection.
    /// </summary>
    internal sealed class TypingIteratorType : PythonIteratorType, ITypingIteratorType {
        /// <summary>
        /// Implements iteration over list-like typed collection such as List[T]
        /// or Sequence[T]. Similar to the Iterator[T]. The iterator does not
        /// track items in the collection, it repeats the same item endlessly.
        /// </summary>
        public TypingIteratorType(IPythonType itemType, BuiltinTypeId iteratorType, IPythonInterpreter interpreter)
            : base(iteratorType, interpreter) {
            ItemTypes = new[] { itemType };
            Repeat = true;
            Name = $"Iterator[{itemType.Name}]";
        }

        /// <summary>
        /// Implements iteration over tuple-like typed collection such as Tuple[T1, T2, T3, ...]
        /// The iterator goes along declared items types and stops when there are no more types.
        /// </summary>
        public TypingIteratorType(IReadOnlyList<IPythonType> itemTypes, BuiltinTypeId iteratorType, IPythonInterpreter interpreter)
            : base(iteratorType, interpreter) {
            Check.ArgumentOutOfRange(nameof(itemTypes), () => itemTypes.Count == 0);
            ItemTypes = itemTypes;
            Name = $"Iterator[{CodeFormatter.FormatSequence(string.Empty, '(', itemTypes)}]";
        }

        public IReadOnlyList<IPythonType> ItemTypes { get; }
        public bool Repeat { get; }
        public override string Name { get; }

        public override bool Equals(object obj) {
            if (!(obj is IPythonType other)) {
                return false;
            }

            if (obj is TypingIteratorType iterator) {
                // Compare item types
                if (ItemTypes.Count != iterator.ItemTypes.Count) {
                    return false;
                }
                for (var i = 0; i < ItemTypes.Count; i++) {
                    if (ItemTypes[i].IsGenericParameter() || iterator.ItemTypes[i].IsGenericParameter()) {
                        continue;
                    }
                    if (!PythonTypeComparer.Instance.Equals(ItemTypes[i], iterator.ItemTypes[i])) {
                        return false;
                    }
                }
            }
            return other.TypeId == TypeId || PythonTypeComparer.Instance.Equals(this, other);
        }

        public override int GetHashCode()
            => ItemTypes.Aggregate(0, (current, item) => current ^ item.GetHashCode()) ^ Name.GetHashCode();
    }
}
