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

        public PythonDictionary(PythonDictionaryType dictType, LocationInfo location, IReadOnlyDictionary<IMember, IMember> contents) :
            base(dictType, location, contents.Keys.ToArray()) {
            foreach (var kvp in contents) {
                _contents[kvp.Key] = kvp.Value;
            }
            _interpreter = dictType.DeclaringModule.Interpreter;
        }

        public PythonDictionary(IPythonInterpreter interpreter, LocationInfo location, IMember contents) :
            base(CollectionTypesCache.GetType<PythonDictionaryType>(interpreter), location, Array.Empty<IMember>()) {
            if (contents is IPythonDictionary dict) {
                foreach (var key in dict.Keys) {
                    _contents[key] = dict[key];
                }
                Contents = _contents.Keys.ToArray();
            }
            _interpreter = interpreter;
        }

        public PythonDictionary(IPythonInterpreter interpreter, LocationInfo location, IReadOnlyDictionary<IMember, IMember> contents) :
            this(CollectionTypesCache.GetType<PythonDictionaryType>(interpreter), location, contents) {
            _interpreter = interpreter;
        }

        public IEnumerable<IMember> Keys => _contents.Keys.ToArray();
        public IEnumerable<IMember> Values => _contents.Values.ToArray();

        public IReadOnlyList<IPythonCollection> Items
            => _contents.Select(kvp => PythonCollectionType.CreateTuple(Type.DeclaringModule, Location, new[] { kvp.Key, kvp.Value })).ToArray();

        public IMember this[IMember key] =>
            _contents.TryGetValue(key, out var value) ? value : UnknownType;

        public override IPythonIterator GetIterator() => Call(@"iterkeys", Array.Empty<object>()) as IPythonIterator;

        public override IMember Index(object key) => key is IMember m ? this[m] : UnknownType;

        public override IMember Call(string memberName, IReadOnlyList<object> args) {
            // Specializations
            switch (memberName) {
                case @"get":
                    return args.Count > 0 ? Index(args[0]) : _interpreter.UnknownType;
                case @"items":
                    return PythonCollectionType.CreateList(Type.DeclaringModule, LocationInfo.Empty, Items, false);
                case @"keys":
                    return PythonCollectionType.CreateList(Type.DeclaringModule, LocationInfo.Empty, Keys.ToArray());
                case @"values":
                    return PythonCollectionType.CreateList(Type.DeclaringModule, LocationInfo.Empty, Values.ToArray());
                case @"iterkeys":
                    return PythonCollectionType.CreateList(Type.DeclaringModule, LocationInfo.Empty, Keys.ToArray()).GetIterator();
                case @"itervalues":
                    return PythonCollectionType.CreateList(Type.DeclaringModule, LocationInfo.Empty, Values.ToArray()).GetIterator();
                case @"iteritems":
                    return PythonCollectionType.CreateList(Type.DeclaringModule, LocationInfo.Empty, Items, false).GetIterator();
                case @"pop":
                    return Values.FirstOrDefault() ?? _interpreter.UnknownType;
                case @"popitem":
                    return Items.Count > 0 ? Items[0] as IMember : _interpreter.UnknownType;
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

            public int GetHashCode(IMember obj) => 0; // Force call to Equals.
        }

        public IEnumerator<KeyValuePair<IMember, IMember>> GetEnumerator() => _contents.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _contents.GetEnumerator();
        public int Count => _contents.Count;
        public bool ContainsKey(IMember key) => _contents.ContainsKey(key);
        public bool TryGetValue(IMember key, out IMember value) => _contents.TryGetValue(key, out value);
    }
}
