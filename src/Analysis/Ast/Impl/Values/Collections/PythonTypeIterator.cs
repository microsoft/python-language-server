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

using System.Collections.Generic;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;

namespace Microsoft.Python.Analysis.Values.Collections {
    internal sealed class PythonTypeIterator : PythonInstance, IPythonIterator {
        private readonly BuiltinTypeId _contentType;
        public PythonTypeIterator(BuiltinTypeId iteratorType, BuiltinTypeId contentType, IPythonInterpreter interpreter)
            : base(new PythonIteratorType(iteratorType, interpreter)) {
            _contentType = contentType;
        }

        public IMember Next => Type.DeclaringModule.Interpreter.GetBuiltinType(_contentType);

        public static IPythonIterator FromTypeId(IPythonInterpreter interpreter, BuiltinTypeId typeId) {
            switch (typeId) {
                case BuiltinTypeId.Str:
                    return new PythonTypeIterator(BuiltinTypeId.StrIterator, BuiltinTypeId.Str, interpreter);
                case BuiltinTypeId.Bytes:
                    return new PythonTypeIterator(BuiltinTypeId.BytesIterator, BuiltinTypeId.Bytes, interpreter);
                case BuiltinTypeId.Unicode:
                    return new PythonTypeIterator(BuiltinTypeId.UnicodeIterator, BuiltinTypeId.Unicode, interpreter);
                default:
                    // TODO: Add more?
                    return null;
            }
        }

        public override IMember Call(string memberName, IReadOnlyList<object> args) {
            // Specializations
            switch (memberName) {
                case @"__next__":
                case @"next":
                    return Next;
            }
            return base.Call(memberName, args);
        }
    }
}
