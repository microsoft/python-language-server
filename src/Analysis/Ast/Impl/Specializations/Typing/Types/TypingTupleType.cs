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
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal class TypingTupleType : PythonCollectionType, ITypingTupleType {
        /// <summary>
        /// Creates type info for a strongly-typed tuple, such as Tuple[T1, T2, ...].
        /// </summary>
        /// <param name="itemTypes">Tuple item types.</param>
        /// <param name="interpreter">Python interpreter.</param>
        public TypingTupleType(IReadOnlyList<IPythonType> itemTypes, IPythonInterpreter interpreter) 
            : base(null, BuiltinTypeId.Tuple, interpreter, false) {
            ItemTypes = itemTypes;
            Name = CodeFormatter.FormatSequence("Tuple" ,'[', itemTypes);
        }

        public IReadOnlyList<IPythonType> ItemTypes { get; }

        public override string Name { get; }
        public override bool IsAbstract => false;

        public override IMember CreateInstance(string typeName, LocationInfo location, IReadOnlyList<object> args)
            => new TypingTuple(this, location);

        public override IMember Index(IPythonInstance instance, object index) {
            var n = PythonCollection.GetIndex(index);
            return n >= 0 && n < ItemTypes.Count ? ItemTypes[n] : UnknownType;
        }
    }
}
