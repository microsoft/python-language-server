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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;

namespace Microsoft.Python.Analysis.Values.Collections {
    internal class PythonSequenceIterator : PythonIterator {
        private readonly IPythonSequence _owner;
        private int _index;

        public PythonSequenceIterator(IPythonSequence owner)
            : base(new PythonIteratorType(owner.GetPythonType().TypeId.GetIteratorTypeId(), owner.Type.DeclaringModule)) {
            _owner = owner;
        }

        public override IMember Next => _owner.Index(_index++) ?? UnknownType;
    }
}
