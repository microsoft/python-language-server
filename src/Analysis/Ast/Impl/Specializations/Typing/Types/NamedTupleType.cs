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
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class NamedTupleType : TypingTupleType, ITypingNamedTupleType {
        // Since named tuple operates as a new, separate type, we need to track
        // its location rather than delegating down to the general wrapper over
        // Python built-in tuple.
        private sealed class NamedTupleLocatedMember: LocatedMember {
            public NamedTupleLocatedMember(Location location) : base(location) { }
            public override PythonMemberType MemberType => PythonMemberType.Class;
        }
        private readonly NamedTupleLocatedMember _locatedMember;

        /// <summary>
        /// Creates type info for a strongly-typed tuple, such as Tuple[T1, T2, ...].
        /// </summary>
        public NamedTupleType(string tupleName, IReadOnlyList<string> itemNames, IReadOnlyList<IPythonType> itemTypes, IPythonModule declaringModule, IndexSpan indexSpan)
            : base(itemTypes, declaringModule, declaringModule.Interpreter) {
            Name = tupleName ?? throw new ArgumentNullException(nameof(tupleName));
            ItemNames = itemNames;

            var typeNames = itemTypes.Select(t => t.IsUnknown() ? string.Empty : t.Name);
            var pairs = itemNames.Zip(typeNames, (name, typeName) => string.IsNullOrEmpty(typeName) ? name : $"{name}: {typeName}");
            Documentation = CodeFormatter.FormatSequence(tupleName, '(', pairs);

            _locatedMember = new NamedTupleLocatedMember(new Location(declaringModule, indexSpan));
        }

        public IReadOnlyList<string> ItemNames { get; }

        #region IPythonType
        public override string Name { get; }
        public override string QualifiedName => $"{DeclaringModule.Name}:{Name}"; // Named tuple name is a type name as class.
        public override bool IsSpecialized => true;
        public override string Documentation { get; }
        #endregion

        #region ILocatedMember
        public override Location Location => _locatedMember.Location;
        public override LocationInfo Definition => _locatedMember.Definition;
        public override IReadOnlyList<LocationInfo> References => _locatedMember.References;
        public override void AddReference(Location location) => _locatedMember.AddReference(location);
        public override void RemoveReferences(IPythonModule module) => _locatedMember.RemoveReferences(module);
        #endregion

        public override IMember CreateInstance(string typeName, IArgumentSet args) => new TypingTuple(this);

        // NamedTuple does not create instances, it defines a type.
        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => this;

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
