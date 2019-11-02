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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    [DebuggerDisplay("n:{" + nameof(Name) + "}")]
    internal sealed class NamedTupleModel: MemberModel {
        public string[] ItemNames { get; set; }
        public string[] ItemTypes { get; set; }

        [NonSerialized] private NamedTupleType _namedTuple;

        public NamedTupleModel() { } // For de-serializer from JSON

        public NamedTupleModel(ITypingNamedTupleType nt, IServiceContainer services) {
            Id = nt.Name.GetStableHash();
            Name = nt.Name;
            DeclaringModuleId = nt.DeclaringModule.GetUniqueId(services);
            QualifiedName = nt.QualifiedName;
            IndexSpan = nt.Location.IndexSpan.ToModel();
            ItemNames = nt.ItemNames.ToArray();
            ItemTypes = nt.ItemTypes.Select(t => t.QualifiedName).ToArray();
        }

        protected override IMember DeclareMember(IPythonType declaringType) {
            if (_namedTuple == null) {
                var itemTypes = ItemTypes.Select(_mf.ConstructType).ToArray();
                _namedTuple = new NamedTupleType(Name, ItemNames, itemTypes, _mf.Module, IndexSpan.ToSpan());
            }
            return _namedTuple;
        }

        protected override void FinalizeMember() { }
    }
}
