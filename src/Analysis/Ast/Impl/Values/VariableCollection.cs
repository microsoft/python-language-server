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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values {
    [DebuggerDisplay("Count: {Count}")]
    internal sealed class VariableCollection : IVariableCollection {
        public static readonly VariableCollection Empty = new VariableCollection();
        private Dictionary<string, Variable> _variables;
        private readonly object _lock = new object();

        private Dictionary<string, Variable> Variables {
            get {
                lock (_lock) {
                    return _variables ?? (_variables = new Dictionary<string, Variable>());
                }
            }
        }

        #region ICollection
        public int Count {
            get {
                lock (_lock) {
                    return _variables?.Count ?? 0;
                }
            }
        }

        public IEnumerator<IVariable> GetEnumerator() {
            lock (_lock) {
                return _variables != null
                    ? _variables.Values.ToList().GetEnumerator()
                    : Enumerable.Empty<IVariable>().GetEnumerator();
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region IVariableCollection

        public IVariable this[string name] {
            get {
                lock (_lock) {
                    return _variables != null 
                        ? _variables.TryGetValue(name, out var v) ? v : null
                        : null;
                }
            }
        }

        public bool Contains(string name) {
            lock (_lock) {
                return _variables != null && _variables.ContainsKey(name);
            }
        }

        public IReadOnlyList<string> Names {
            get {
                lock (_lock) {
                    return _variables != null ? _variables.Keys.ToArray() : Array.Empty<string>();
                }
            }
        }

        public bool TryGetVariable(string key, out IVariable value) {
            value = null;
            lock (_lock) {
                if (_variables != null && _variables.TryGetValue(key, out var v)) {
                    value = v;
                }
            }
            return value != null;
        }
        #endregion

        #region IMemberContainer
        public IMember GetMember(string name) => TryGetVariable(name, out var v) ? v : null;
        public IEnumerable<string> GetMemberNames() => Names;
        #endregion

        internal void DeclareVariable(string name, IMember value, VariableSource source, Location location = default) {
            name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException(nameof(name));
            lock (_lock) {
                if (Variables.TryGetValue(name, out var existing)) {
                    existing.Assign(value, location);
                } else {
                    Variables[name] = new Variable(name, value, source, location);
                }
            }
        }

        internal void LinkVariable(string name, IVariable v, Location location) {
            name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException(nameof(name));
            lock (_lock) {
                Variables[name] = new Variable(name, v, location);
            }
        }

        internal void RemoveVariable(string name) {
            lock (_lock) {
                _variables?.Remove(name);
            }
        }
    }
}
