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
    internal sealed class PythonTupleType : PythonSequenceType {
        private static PythonTupleType _instance;

        public static PythonTupleType GetPythonTupleType(IPythonInterpreter interpreter)
            => _instance.IsUnknown() ? _instance = new PythonTupleType(interpreter) : _instance;

        private PythonTupleType(IPythonInterpreter interpreter)
            : base(null, BuiltinTypeId.Tuple, interpreter.ModuleResolution.BuiltinsModule, Array.Empty<IPythonType>(), false) { }

        public override IMember CreateInstance(IPythonModule declaringModule, LocationInfo location, IReadOnlyList<object> args)
            => new PythonTuple(this, location, args);
    }
}
