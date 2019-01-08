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
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Types.Collections {
    internal sealed class PythonListType : PythonSequenceType {
        public PythonListType(IPythonInterpreter interpreter)
            : base(null, BuiltinTypeId.List, interpreter.ModuleResolution.BuiltinsModule, Array.Empty<IPythonType>(), true) {
        }

        public override IMember CreateInstance(LocationInfo location, IReadOnlyList<object> args)
            => new PythonList(this, location, args.OfType<IMember>().ToArray());

        // Constructor call
        public override IMember Call(IPythonInstance instance, string memberName, IReadOnlyList<object> args) 
            => CreateInstance(instance?.Location ?? LocationInfo.Empty, args);

        public override BuiltinTypeId TypeId => BuiltinTypeId.List;
        public override PythonMemberType MemberType => PythonMemberType.Class;
    }
}
