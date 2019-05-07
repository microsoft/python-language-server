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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values {
    [DebuggerDisplay("Count: {Count}")]
    internal sealed class VariableCollection : IVariableCollection {
        public static readonly IVariableCollection Empty = new VariableCollection();
        private readonly object _syncObj = new object();
        private readonly Dictionary<string, Variable> _variables = new Dictionary<string, Variable>();

        public List<Variable>.Enumerator GetEnumerator() {
            lock (_syncObj) {
                return _variables.Values.ToList().GetEnumerator();
            }
        }

        #region ICollection
        public int Count {
            get {
                lock (_syncObj) {
                    return _variables.Count;
                }
            }
        }

        IEnumerator<IVariable> IEnumerable<IVariable>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region IVariableCollection
        public IVariable this[string name] {
            get {
                lock (_syncObj) {
                    return _variables.TryGetValue(name, out var v) ? v : default;
                }
            }
        }

        public bool Contains(string name) {
            lock (_syncObj) {
                return _variables.ContainsKey(name);
            }
        }

        public IReadOnlyList<string> Names {
            get {
                lock (_syncObj) {
                    return _variables.Keys.ToArray();
                }
            }
        }

        public bool TryGetVariable(string key, out IVariable value) {
            lock (_syncObj) {
                if (_variables.TryGetValue(key, out var v)) {
                    value = v;
                    return true;
                }

                value = null;
                return false;
            }
        }
        #endregion

        #region IMemberContainer
        public IMember GetMember(string name) {
            lock (_syncObj) {
                return _variables.TryGetValue(name, out var v) ? v : null;
            }
        }

        public IEnumerable<string> GetMemberNames() {
            lock (_syncObj) {
                return _variables.Keys.ToArray();
            }
        }

        #endregion

        internal void DeclareVariable(string name, IMember value, VariableSource source, Location location = default) {
            name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException(nameof(name));
            lock (_syncObj) {
                if (_variables.TryGetValue(name, out var existing)) {
                    existing.Assign(value, location);
                } else {
                    _variables[name] = new Variable(name, value, source, location);
                }
            }
        }

        internal void LinkVariable(string name, IVariable v, Location location) {
            name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException(nameof(name));
            var variable =  new Variable(name, v, location);
            lock (_syncObj) {
                _variables[name] = variable;
            }
        }

        internal void RemoveVariable(string name) {
            lock (_syncObj) {
                _variables.Remove(name);
            }
        }
    }
}
