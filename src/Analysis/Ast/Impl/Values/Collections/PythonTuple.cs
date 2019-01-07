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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Default immutable tuple.
    /// </summary>
    internal class PythonTuple : PythonSequence {
        public PythonTuple(PythonTupleType tupleType, LocationInfo location, params object[] contents) :
            base(tupleType, location, contents) { }

        public PythonTuple(IPythonInterpreter interpreter, LocationInfo location, params object[] contents) :
            this(PythonTupleType.GetPythonTupleType(interpreter), location, contents) { }
    }
}
