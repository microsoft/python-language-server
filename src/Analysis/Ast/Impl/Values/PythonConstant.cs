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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    internal class PythonConstant : PythonInstance, IPythonConstant, IEquatable<IPythonConstant> {
        public PythonConstant(object value, IPythonType type, Node definition = null)
            : base(type, definition) {
            Value = value;
        }
        public object Value { get; }

        public bool TryGetValue<T>(out T value) {
            if (Value is T variable) {
                value = variable;
                return true;
            }
            value = default;
            return false;
        }

        public bool Equals(IPythonConstant other) {
            if(!base.Equals(other)) {
                return false;
            }
            return Value?.Equals(other?.Value) == true;
        }
    }
}
