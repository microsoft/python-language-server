// Python Tools for Visual Studio
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

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonLookup : AstPythonTypeWrapper, IPythonLookupType, IPythonIterableType {
        private readonly IReadOnlyDictionary<IPythonType, IReadOnlyList<IPythonType>> _mapping;

        public AstPythonLookup(
            IPythonType lookupType,
            IPythonModule declaringModule,
            IEnumerable<IPythonType> keys,
            IEnumerable<IPythonType> values,
            IEnumerable<KeyValuePair<IPythonType, IEnumerable<IPythonType>>> mapping,
            IPythonIteratorType iterator
        ): base(lookupType, declaringModule) {
            KeyTypes = (keys ?? throw new ArgumentNullException(nameof(keys))).ToArray();
            ValueTypes = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
            _mapping = mapping?.ToDictionary(k => k.Key, k => (IReadOnlyList<IPythonType>)k.Value.ToArray());
            IteratorType = iterator;
        }

        public IEnumerable<IPythonType> KeyTypes { get; }
        public IEnumerable<IPythonType> ValueTypes { get; }
        public IEnumerable<IPythonType> GetIndex(IPythonType key) {
            if (_mapping != null && _mapping.TryGetValue(key, out var res)) {
                return res;
            }
            return Enumerable.Empty<IPythonType>();
        }

        public IPythonIteratorType IteratorType { get; }

        public override string Name => InnerType?.Name ?? "tuple";
        public override BuiltinTypeId TypeId => InnerType?.TypeId ?? BuiltinTypeId.Tuple;
        public override bool IsBuiltIn => InnerType?.IsBuiltIn ?? true;
        public override PythonMemberType MemberType => InnerType?.MemberType ?? PythonMemberType.Class;
    }
}
