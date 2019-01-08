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
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Default mutable list with mixed content.
    /// </summary>
    internal sealed class PythonDictionary : PythonSequence, IPythonDictionary {
        private readonly Dictionary<IMember, IMember> _contents;
        private readonly IPythonInterpreter _interpreter;

        public PythonDictionary(PythonDictionaryType dictType, LocationInfo location, IDictionary<IMember, IMember> contents) :
            base(dictType, location, contents.Keys.ToArray()) {
            _contents = new Dictionary<IMember, IMember>(contents, new KeyComparer());
        }

        public PythonDictionary(IPythonInterpreter interpreter, LocationInfo location, IDictionary<IMember, IMember> contents) :
            this(PythonDictionaryType.GetPythonDictionaryType(interpreter), location, contents) {
            _interpreter = interpreter;
        }

        public IReadOnlyList<IMember> Keys => _contents.Keys.ToArray();
        public IReadOnlyList<IMember> Values => _contents.Values.ToArray();
        public IReadOnlyList<IPythonSequence> Items
            => _contents.Select(kvp => new PythonTuple(_interpreter, Location, new[] { kvp.Key, kvp.Value })).ToArray();
        public IMember this[IMember key]
            => _contents.TryGetValue(key, out var value) ? value : Type.DeclaringModule.Interpreter.UnknownType;

        public override IMember Index(object key)
            => key is IMember m ? this[m] : Type.DeclaringModule.Interpreter.UnknownType;

        public override IMember Call(string memberName, IReadOnlyList<object> args) {
            // Specializations
            switch (memberName) {
                case @"get":
                    return args.Count > 0 ? Index(args[0]) : _interpreter.UnknownType;
                case @"items":
                    return new PythonList(_interpreter, LocationInfo.Empty, Items);
                case @"keys":
                    return new PythonList(_interpreter, LocationInfo.Empty, Keys);
                case @"values":
                    return new PythonList(_interpreter, LocationInfo.Empty, Values);
                case @"pop":
                    return Index(0);
            }
            return base.Call(memberName, args);
        }

        private sealed class KeyComparer : IEqualityComparer<IMember> {
            public bool Equals(IMember x, IMember y) {
                if (x is IPythonConstant cx && y is IPythonConstant cy) {
                    return cx.Value.Equals(cy.Value);
                }
                return x?.Equals(y) == true;
            }

            public int GetHashCode(IMember obj) => 0;
        }
    }
}
