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
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Specializations.Typing.Values {
    internal class TypingList : PythonCollection {
        private readonly ITypingListType _collectionType;

        public TypingList(ITypingListType collectionType)
            : base(collectionType, Array.Empty<IMember>()) {
            _collectionType = collectionType;
        }

        public override IPythonIterator GetIterator() {
            var iteratorTypeId = _collectionType.TypeId.GetIteratorTypeId();
            var iteratorType = new TypingIteratorType(_collectionType.ItemType, iteratorTypeId, _collectionType.DeclaringModule.Interpreter);
            return new TypingIterator(iteratorType, this);
        }

        public override IMember Index(IArgumentSet args) => _collectionType.Index(this, args);
    }
}
