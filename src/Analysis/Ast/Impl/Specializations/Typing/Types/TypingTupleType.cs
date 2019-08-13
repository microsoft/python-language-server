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
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal class TypingTupleType : PythonCollectionType, ITypingTupleType {
        /// <summary>
        /// Creates type info for a strongly-typed tuple, such as Tuple[T1, T2, ...].
        /// </summary>
        /// <param name="itemTypes">Tuple item types.</param>
        /// <param name="declaringModule">Declaring module. If null, then 'typing' is assumed.</param>
        /// <param name="interpreter">Python interpreter.</param>
        public TypingTupleType(IReadOnlyList<IPythonType> itemTypes, IPythonModule declaringModule, IPythonInterpreter interpreter)
            : base(BuiltinTypeId.Tuple, declaringModule ?? interpreter.ModuleResolution.GetSpecializedModule("typing"), false) {
            ItemTypes = itemTypes.Count > 0 ? itemTypes : new[] { interpreter.UnknownType };
            Name = CodeFormatter.FormatSequence("Tuple", '[', ItemTypes);
            QualifiedName = CodeFormatter.FormatSequence("typing:Tuple", '[', ItemTypes.Select(t => t.QualifiedName));
        }

        public IReadOnlyList<IPythonType> ItemTypes { get; }

        public override string Name { get; }
        public override string QualifiedName { get; }

        public override bool IsAbstract => false;
        public override bool IsSpecialized => true;

        public override IMember CreateInstance(string typeName, IArgumentSet args)
            => new TypingTuple(this);

        public override IMember Index(IPythonInstance instance, IArgumentSet args) {
            var n = PythonCollection.GetIndex(args);
            if (n < 0) {
                n = ItemTypes.Count + n; // -1 means last, etc.
            }
            if (n >= 0 && n < ItemTypes.Count) {
                return ItemTypes[n];
            }
            return UnknownType;
        }

        public override bool Equals(object obj) {
            if (!(obj is TypingTupleType other)) {
                return false;
            }
            if (ItemTypes.Count != other.ItemTypes.Count) {
                return false;
            }
            for (var i = 0; i < ItemTypes.Count; i++) {
                if (ItemTypes[i].IsGenericParameter() || other.ItemTypes[i].IsGenericParameter()) {
                    continue;
                }
                if (!PythonTypeComparer.Instance.Equals(ItemTypes[i], other.ItemTypes[i])) {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
            => ItemTypes.Aggregate(0, (current, item) => current ^ item.GetHashCode()) ^ Name.GetHashCode();
    }
}
