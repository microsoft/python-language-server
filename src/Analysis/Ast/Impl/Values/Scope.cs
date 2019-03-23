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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Represents scope where variables can be declared.
    /// </summary>
    internal class Scope : IScope {
        private VariableCollection _nonLocals;
        private VariableCollection _globals;
        private List<Scope> _childScopes;

        protected VariableCollection VariableCollection { get; } = new VariableCollection();

        public Scope(ScopeStatement node, IScope outerScope, IPythonModule module) {
            Node = node;
            OuterScope = outerScope;
            Module = module;
            DeclareBuiltinVariables();
        }

        #region IScope

        public string Name => Node?.Name ?? "<global>";
        public virtual ScopeStatement Node { get; }
        public IScope OuterScope { get; }
        public IPythonModule Module { get; }

        public IReadOnlyList<IScope> Children => (IReadOnlyList<IScope>)_childScopes ?? Array.Empty<IScope>();
        public IVariableCollection Variables => VariableCollection;
        public IVariableCollection NonLocals => _nonLocals ?? VariableCollection.Empty;
        public IVariableCollection Globals => _globals ?? VariableCollection.Empty;

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

        public void DeclareVariable(string name, IMember value, VariableSource source, IPythonModule module = null, Node location = null)
            => VariableCollection.DeclareVariable(name, value, source, module ?? Module, location);

        public void DeclareNonLocal(string name, Node location)
            => (_nonLocals ?? (_nonLocals = new VariableCollection())).DeclareVariable(name, null, VariableSource.Locality, Module, location);

        public void DeclareGlobal(string name, Node location)
            => (_globals ?? (_globals = new VariableCollection())).DeclareVariable(name, null, VariableSource.Locality, Module, location);

        #endregion

        internal void AddChildScope(Scope s) => (_childScopes ?? (_childScopes = new List<Scope>())).Add(s);

        private void DeclareBuiltinVariables() {
            if(Node == null || Module.ModuleType != ModuleType.User || this is IGlobalScope) {
                return;
            }

            var strType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            var objType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Object);

            VariableCollection.DeclareVariable("__name__", strType, VariableSource.Builtin);

            if (Node is FunctionDefinition) {
                var dictType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Dict);
                var tupleType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);

                VariableCollection.DeclareVariable("__closure__", tupleType, VariableSource.Builtin);
                VariableCollection.DeclareVariable("__code__", objType, VariableSource.Builtin);
                VariableCollection.DeclareVariable("__defaults__", tupleType, VariableSource.Builtin);
                VariableCollection.DeclareVariable("__dict__", dictType, VariableSource.Builtin);
                VariableCollection.DeclareVariable("__doc__", strType, VariableSource.Builtin);
                VariableCollection.DeclareVariable("__func__", objType, VariableSource.Builtin);
                VariableCollection.DeclareVariable("__globals__", dictType, VariableSource.Builtin);
            } else if(Node is ClassDefinition) {
                VariableCollection.DeclareVariable("__self__", objType, VariableSource.Builtin);
            }
        }
    }

    internal class EmptyGlobalScope : IGlobalScope {
        public EmptyGlobalScope(IPythonModule module) {
            GlobalScope = this;
            Module = module;
        }
        public IPythonModule Module { get; }
        public string Name => string.Empty;
        public ScopeStatement Node => null;
        public IScope OuterScope => null;
        public IGlobalScope GlobalScope { get; protected set; }
        public IReadOnlyList<IScope> Children => Array.Empty<IScope>();
        public IEnumerable<IScope> EnumerateTowardsGlobal => Enumerable.Repeat(this, 1);
        public IEnumerable<IScope> EnumerateFromGlobal => Enumerable.Repeat(this, 1);
        public IVariableCollection Variables => VariableCollection.Empty;
        public IVariableCollection NonLocals => VariableCollection.Empty;
        public  IVariableCollection Globals => VariableCollection.Empty;

        public void DeclareVariable(string name, IMember value, VariableSource source, IPythonModule module, Node location) { }
    }
}
