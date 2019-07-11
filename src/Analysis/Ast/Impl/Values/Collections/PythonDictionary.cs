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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;

namespace Microsoft.Python.Analysis.Values.Collections {
    /// <summary>
    /// Default mutable list with mixed content.
    /// </summary>
    internal class PythonDictionary : PythonCollection, IPythonDictionary {
        private readonly Dictionary<IMember, IMember> _contents = new Dictionary<IMember, IMember>(new KeyComparer());
        private readonly IPythonInterpreter _interpreter;

        public PythonDictionary(PythonDictionaryType dictType, IReadOnlyDictionary<IMember, IMember> contents, bool exact = false) :
            base(dictType, contents.Keys.ToArray(), exact: exact) {
            foreach (var kvp in contents) {
                _contents[kvp.Key] = kvp.Value;
            }
            _interpreter = dictType.DeclaringModule.Interpreter;
        }

        public PythonDictionary(IPythonInterpreter interpreter, IMember contents, bool exact = false) :
            base(new PythonDictionaryType(interpreter), Array.Empty<IMember>(), exact: exact) {
            if (contents is IPythonDictionary dict) {
                foreach (var key in dict.Keys) {
                    _contents[key] = dict[key];
                }
                Contents = _contents.Keys.ToArray();
            }
            _interpreter = interpreter;
        }

        public PythonDictionary(IPythonInterpreter interpreter, IReadOnlyDictionary<IMember, IMember> contents, bool exact = false) :
            this(new PythonDictionaryType(interpreter), contents, exact: exact) {
            _interpreter = interpreter;
        }

        public IEnumerable<IMember> Keys => _contents.Keys.ToArray();
        public IEnumerable<IMember> Values => _contents.Values.ToArray();

        public IReadOnlyList<IPythonCollection> Items
            => _contents.Select(kvp => PythonCollectionType.CreateTuple(Type.DeclaringModule.Interpreter, new[] { kvp.Key, kvp.Value })).ToArray();

        public IMember this[IMember key] =>
            _contents.TryGetValue(key, out var value) ? value : UnknownType;

        public override IPythonIterator GetIterator() =>
            Call(@"iterkeys", ArgumentSet.WithoutContext) as IPythonIterator ?? new EmptyIterator(Type.DeclaringModule.Interpreter.UnknownType);

        public override IMember Index(IArgumentSet args) {
            if (args.Arguments.Count == 1) {
                return args.Arguments[0].Value is IMember m ? this[m] : UnknownType;
            }
            return UnknownType;
        }

        public override IMember Call(string memberName, IArgumentSet args) {
            // Specializations
            switch (memberName) {
                case @"get":
                    // d = {}
                    // d.get("test", 3.14), 3.14 is the default value so we infer the type of the return from it
                    if (args.Arguments.Count > 1) {
                        var defaultArg = args.Arguments[1].Value as IMember;
                        return Index(new ArgumentSet(new List<IMember>() { defaultArg }, args.Expression, args.Eval));
                    }
                    return _interpreter.UnknownType;
                case @"items":
                    return CreateList(Items);
                case @"keys":
                    return CreateList(Keys.ToArray());
                case @"values":
                    return CreateList(Values.ToArray());
                case @"iterkeys":
                    return CreateList(Keys.ToArray()).GetIterator();
                case @"itervalues":
                    return CreateList(Values.ToArray()).GetIterator();
                case @"iteritems":
                    return CreateList(Items).GetIterator();
                case @"pop":
                    return Values.FirstOrDefault() ?? _interpreter.UnknownType;
                case @"popitem":
                    return Items.Count > 0 ? Items[0] as IMember : _interpreter.UnknownType;
            }
            return base.Call(memberName, args);
        }

        private IPythonCollection CreateList(IReadOnlyList<IMember> items)
            => PythonCollectionType.CreateList(Type.DeclaringModule.Interpreter, items, false);

        private sealed class KeyComparer : IEqualityComparer<IMember> {
            public bool Equals(IMember x, IMember y) {
                if (x is IPythonConstant cx && y is IPythonConstant cy) {
                    return cx.Value.Equals(cy.Value);
                }
                return x?.Equals(y) == true;
            }

            public int GetHashCode(IMember obj) => 0; // Force call to Equals.
        }

        public IEnumerator<KeyValuePair<IMember, IMember>> GetEnumerator() => _contents.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _contents.GetEnumerator();
        public int Count => _contents.Count;
        public bool ContainsKey(IMember key) => _contents.ContainsKey(key);
        public bool TryGetValue(IMember key, out IMember value) => _contents.TryGetValue(key, out value);
    }
}
