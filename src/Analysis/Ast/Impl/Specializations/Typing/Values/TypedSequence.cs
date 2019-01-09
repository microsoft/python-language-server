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

using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Specializations.Typing.Values {
    internal class TypedSequence : PythonSequence {
        private readonly TypedSequenceType _seqType;

        public TypedSequence(TypedSequenceType seqtType, LocationInfo location = null)
            : base(seqtType, location ?? LocationInfo.Empty) {
            _seqType = seqtType;
        }

        public override IPythonIterator GetIterator() {
            var iteratorType = new TypingIteratorType(_seqType.DeclaringModule, _seqType.ContentType, _seqType.IteratorTypeId);
            return new TypedIterator(iteratorType, _seqType.ContentType);
        }
    }
}
