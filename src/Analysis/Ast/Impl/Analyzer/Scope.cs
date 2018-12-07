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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Represents scope where variables can be declared.
    /// </summary>
    internal sealed class Scope : IScope {
        private readonly Dictionary<string, IMember> _variables = new Dictionary<string, IMember>();
        private List<Scope> _childScopes;

        public Scope(Node node, IScope outerScope, bool visibleToChildren = true) {
            Node = node;
            OuterScope = outerScope;
            VisibleToChildren = visibleToChildren;
        }

        public static Scope CreateGlobalScope() => new Scope(null, null);

        #region IScope
        public string Name => Node?.NodeName ?? "<global>";
        public Node Node { get; }
        public IScope OuterScope { get; }
        public bool VisibleToChildren { get; }

        public IReadOnlyList<IScope> Children => _childScopes ?? Array.Empty<IScope>() as IReadOnlyList<IScope>;
        public IReadOnlyDictionary<string, IMember> Variables => _variables;

        public IScope GlobalScope {
            get {
                IScope scope = this;
                while (scope.OuterScope != null) {
                    scope = scope.OuterScope;
                }
                return scope;
            }
        }

        public IEnumerable<IScope> EnumerateTowardsGlobal {
            get {
                for (IScope scope = this; scope != null; scope = scope.OuterScope) {
                    yield return scope;
                }
            }
        }

        public IEnumerable<IScope> EnumerateFromGlobal => EnumerateTowardsGlobal.Reverse();
        #endregion

        public List<Scope> ChildScopes => _childScopes ?? (_childScopes = new List<Scope>());
        public void DeclareVariable(string name, IMember m) => _variables[name] = m;
    }
}
