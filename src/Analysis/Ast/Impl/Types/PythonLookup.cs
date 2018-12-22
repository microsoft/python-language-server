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

namespace Microsoft.Python.Analysis.Types {
    class PythonLookup : PythonTypeWrapper, IPythonLookup, IPythonIterable {
        private readonly IReadOnlyDictionary<IMember, IReadOnlyList<IMember>> _mapping;

        public PythonLookup(
            IPythonType lookupType,
            IPythonModule declaringModule,
            IEnumerable<IMember> keys,
            IEnumerable<IMember> values,
            IEnumerable<KeyValuePair<IMember, IEnumerable<IMember>>> mapping,
            IPythonIterator iterator
        ): base(lookupType, declaringModule) {
            Keys = (keys ?? throw new ArgumentNullException(nameof(keys))).ToArray();
            Values = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
            _mapping = mapping?.ToDictionary(k => k.Key, k => (IReadOnlyList<IMember>)k.Value.ToArray());
            Iterator = iterator;
        }

        public IEnumerable<IMember> Keys { get; }
        public IEnumerable<IMember> Values { get; }
        public IEnumerable<IMember> GetAt(IMember key) {
            if (_mapping != null && _mapping.TryGetValue(key, out var res)) {
                return res;
            }
            return Enumerable.Empty<IPythonType>();
        }

        public IPythonIterator Iterator { get; }

        public override string Name => InnerType?.Name ?? "tuple";
        public override BuiltinTypeId TypeId => InnerType?.TypeId ?? BuiltinTypeId.Tuple;
        public override PythonMemberType MemberType => InnerType?.MemberType ?? PythonMemberType.Class;
    }
}
