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
        private readonly ConcurrentDictionary<string, Variable> _variables = new ConcurrentDictionary<string, Variable>();

        #region ICollection
        public int Count => _variables.Count;
        public IEnumerator<IVariable> GetEnumerator() => _variables.Values.ToList().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region IVariableCollection
        public IVariable this[string name] => _variables.TryGetValue(name, out var v) ? v : null;
        public bool Contains(string name) => _variables.ContainsKey(name);
        public IReadOnlyList<string> Names => _variables.Keys.ToArray();

        public bool TryGetVariable(string key, out IVariable value) {
            value = null;
            if (_variables.TryGetValue(key, out var v)) {
                value = v;
                return true;
            }
            return false;
        }
        #endregion

        #region IMemberContainer
        public IMember GetMember(string name) => _variables.TryGetValue(name, out var v) ? v : null;
        public IEnumerable<string> GetMemberNames() => _variables.Keys.ToArray();
        #endregion

        internal void DeclareVariable(string name, IMember value, VariableSource source, Location location = default) {
            name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException(nameof(name));
            if (_variables.TryGetValue(name, out var existing)) {
                existing.Assign(value, location);
            } else {
                _variables[name] = new Variable(name, value, source, location);
            }
        }

        internal void LinkVariable(string name, IVariable v, Location location) {
            name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException(nameof(name));
            _variables[name] = new Variable(name, v, location);
        }

        internal void RemoveVariable(string name) => _variables.TryRemove(name, out _);
    }
}
