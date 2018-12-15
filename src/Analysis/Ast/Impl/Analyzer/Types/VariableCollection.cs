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
using System.Linq;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    internal sealed class VariableCollection : IVariableCollection {
        public static readonly IVariableCollection Empty = new VariableCollection();
        private readonly ConcurrentDictionary<string, IPythonType> _variables = new ConcurrentDictionary<string, IPythonType>();

        #region ICollection
        public int Count => _variables.Count;
        public IEnumerator<IVariable> GetEnumerator() => _variables.Select(kvp => new Variable(kvp)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region IMemberContainer
        public IPythonType GetMember(string name) 
            => _variables.TryGetValue(name, out var t) ? t : null;

        public IEnumerable<string> GetMemberNames() => _variables.Keys;
        public bool Remove(IVariable item) => throw new NotImplementedException();
        #endregion

        internal void DeclareVariable(string name, IPythonType type) => _variables[name] = type;
    }
}
