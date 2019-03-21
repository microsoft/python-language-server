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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Python ASCII (bytes) string (default string in 2.x).
    /// </summary>
    internal sealed class PythonAsciiString : PythonConstant {
        public PythonAsciiString(AsciiString s, IPythonInterpreter interpreter, Node location = null)
            : base(s, interpreter.GetBuiltinType(interpreter.GetAsciiTypeId()), location) { }
    }

    /// <summary>
    /// Python Unicode string (default string in 3.x+)
    /// </summary>
    internal sealed class PythonUnicodeString : PythonConstant {
        public PythonUnicodeString(string s, IPythonInterpreter interpreter, Node location = null)
            : base(s, interpreter.GetBuiltinType(interpreter.GetUnicodeTypeId()), location) { }
    }

    internal sealed class PythonFString : PythonInstance, IEquatable<PythonFString> {
        public readonly string UnparsedFString;

        public PythonFString(string unparsedFString, IPythonInterpreter interpreter, Node location = null)
            : base(interpreter.GetBuiltinType(interpreter.GetUnicodeTypeId()), location) {
            UnparsedFString = unparsedFString;
        }


        public bool Equals(PythonFString other) {
            if (!base.Equals(other)) {
                return false;
            }
            return UnparsedFString?.Equals(other?.UnparsedFString) == true;
        }
    }
}
