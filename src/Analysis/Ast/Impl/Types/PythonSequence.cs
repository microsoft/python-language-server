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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    internal class PythonSequence : PythonTypeWrapper, IPythonSequence {
        private readonly IReadOnlyList<IMember> _contents;

        public PythonSequence(
            IPythonType sequenceType,
            IPythonModule declaringModule,
            IEnumerable<IMember> contents,
            IPythonType iteratorBase
        ): base(sequenceType, declaringModule) {
            _contents = (contents ?? throw new ArgumentNullException(nameof(contents))).ToArray();
            Iterator = new PythonIterator(iteratorBase, _contents, declaringModule);
        }

        public IMember GetValueAt(IPythonInstance instance, int index) 
            // TODO: report index out of bounds warning
            => index >= 0 && index < _contents.Count ? _contents[index] : null;

        public IEnumerable<IMember> GetContents(IPythonInstance instance) => _contents;

        public IPythonIterator Iterator { get; }
 
        public override string Name => InnerType?.Name ?? "tuple";
        public override BuiltinTypeId TypeId => InnerType?.TypeId ?? BuiltinTypeId.Tuple;
        public override PythonMemberType MemberType => InnerType?.MemberType ?? PythonMemberType.Class;
    }
}
