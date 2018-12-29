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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Represents scope where variables can be declared.
    /// </summary>
    internal class Scope : IScope {
        private VariableCollection _variables;
        private List<Scope> _childScopes;

        public Scope(Node node, IScope outerScope, bool visibleToChildren = true) {
            Node = node;
            OuterScope = outerScope;
            VisibleToChildren = visibleToChildren;
        }

        #region IScope
        public string Name => Node?.NodeName ?? "<global>";
        public Node Node { get; }
        public IScope OuterScope { get; }
        public bool VisibleToChildren { get; }

        public IReadOnlyList<IScope> Children => _childScopes ?? Array.Empty<IScope>() as IReadOnlyList<IScope>;
        public IVariableCollection Variables => _variables ?? VariableCollection.Empty;

        public IGlobalScope GlobalScope {
            get {
                IScope scope = this;
                while (scope.OuterScope != null) {
                    scope = scope.OuterScope;
                }
                Debug.Assert(scope is IGlobalScope);
                return scope as IGlobalScope;
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
        public void DeclareVariable(string name, IMember value, LocationInfo location)
            => (_variables ?? (_variables = new VariableCollection())).DeclareVariable(name, value, location);
        #endregion

        public void AddChildScope(Scope s) => (_childScopes ?? (_childScopes = new List<Scope>())).Add(s);
        public IReadOnlyList<Scope> ToChainTowardsGlobal() => EnumerateTowardsGlobal.OfType<Scope>().ToList();
    }

    internal class EmptyGlobalScope : IGlobalScope {
        public EmptyGlobalScope(IPythonModule module) {
            GlobalScope = this;
            Module = module;
        }
        public IPythonModule Module { get; }
        public string Name => string.Empty;
        public Node Node => null;
        public IScope OuterScope => null;
        public IGlobalScope GlobalScope { get; protected set; }
        public bool VisibleToChildren => true;
        public IReadOnlyList<IScope> Children => Array.Empty<IScope>();
        public IEnumerable<IScope> EnumerateTowardsGlobal => Enumerable.Repeat(this, 1);
        public IEnumerable<IScope> EnumerateFromGlobal => Enumerable.Repeat(this, 1);
        public IVariableCollection Variables => VariableCollection.Empty;
        public void DeclareVariable(string name, IMember value, LocationInfo location) { }

    }
}
