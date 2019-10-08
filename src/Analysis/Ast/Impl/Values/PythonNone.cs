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
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Values {
    internal sealed class PythonNone : PythonType, IPythonInstance {
        public PythonNone(IBuiltinsPythonModule builtins) : base("None", new Location(builtins), string.Empty, BuiltinTypeId.None) { }

        public override IPythonInstance CreateInstance(IArgumentSet args) => this;

        public IPythonType Type => this;

        public IMember Call(string memberName, IArgumentSet args) => DeclaringModule.Interpreter.UnknownType;

        public IPythonIterator GetIterator() => new EmptyIterator(DeclaringModule.Interpreter.UnknownType);

        public IMember Index(IArgumentSet args) => DeclaringModule.Interpreter.UnknownType;
    }
}
