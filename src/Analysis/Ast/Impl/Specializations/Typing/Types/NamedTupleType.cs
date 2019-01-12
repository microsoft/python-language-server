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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class NamedTupleType : TypingTupleType, ITypingNamedTupleType {
        /// <summary>
        /// Creates type info for a strongly-typed tuple, such as Tuple[T1, T2, ...].
        /// </summary>
        public NamedTupleType(string tupleName, IReadOnlyList<string> itemNames, IReadOnlyList<IPythonType> itemTypes, IPythonInterpreter interpreter)
            : base(itemTypes, interpreter) {
            TupleName = tupleName ?? throw new ArgumentNullException(nameof(tupleName));
            ItemNames = itemNames;

            var pairs = itemNames.Zip(itemTypes.Select(t => t.Name), (name, typeName) => $"{name}: {typeName}");
            Name = CodeFormatter.FormatSequence(tupleName, '(', pairs);
        }

        public string TupleName { get; }
        public IReadOnlyList<string> ItemNames { get; }

        public override string Name { get; }

        public override IMember CreateInstance(string typeName, LocationInfo location, IReadOnlyList<object> args)
            => new TypingTuple(this, location);

        public override IEnumerable<string> GetMemberNames() => ItemNames.Concat(base.GetMemberNames());

        public override IMember GetMember(string name) {
            var index = ItemNames.IndexOf(n => n == name);
            if (index >= 0 && index < ItemTypes.Count) {
                return new PythonInstance(ItemTypes[index]);
            }
            return base.GetMember(name);
        }
    }
}
