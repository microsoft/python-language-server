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
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Represents scope where variables can be declared.
    /// </summary>
    internal class Scope : IScope {
        private VariableCollection _variables;
        private VariableCollection _nonLocals;
        private VariableCollection _globals;
        private VariableCollection _imported;
        private Dictionary<ScopeStatement, Scope> _childScopes;

        public Scope(ScopeStatement node, IScope outerScope, IPythonModule module) {
            Check.ArgumentNotNull(nameof(module), module);

            OuterScope = outerScope;
            Module = module;
            if (node != null) {
                Module.AddAstNode(this, node);
            }
            DeclareBuiltinVariables();
        }

        #region IScope
        public string Name => Node?.Name ?? "<global>";
        public virtual ScopeStatement Node => Module.GetAstNode<ScopeStatement>(this) ?? Module.GetAst();
        public IScope OuterScope { get; }
        public IPythonModule Module { get; }

        public IReadOnlyList<IScope> Children => _childScopes?.Values.ToArray() ?? Array.Empty<IScope>();
        public IScope GetChildScope(ScopeStatement node) => _childScopes != null && _childScopes.TryGetValue(node, out var s) ? s : null;

        public IVariableCollection Variables => _variables ?? VariableCollection.Empty;
        public IVariableCollection NonLocals => _nonLocals ?? VariableCollection.Empty;
        public IVariableCollection Globals => _globals ?? VariableCollection.Empty;
        public IVariableCollection Imported => _imported ?? VariableCollection.Empty;

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

        public void DeclareVariable(string name, IMember value, VariableSource source, Location location = default)
            => VariableCollection.DeclareVariable(name, value, source, location);

        public void LinkVariable(string name, IVariable v, Location location)
            => VariableCollection.LinkVariable(name, v, location);

        public void DeclareNonLocal(string name, Location location)
            => (_nonLocals ?? (_nonLocals = new VariableCollection())).DeclareVariable(name, null, VariableSource.Locality, location);

        public void DeclareGlobal(string name, Location location)
            => (_globals ?? (_globals = new VariableCollection())).DeclareVariable(name, null, VariableSource.Locality, location);

        public void DeclareImported(string name, IMember value, Location location = default)
            => (_imported ?? (_imported = new VariableCollection())).DeclareVariable(name, value, VariableSource.Import, location);
        #endregion

        internal void AddChildScope(Scope s) => (_childScopes ?? (_childScopes = new Dictionary<ScopeStatement, Scope>()))[s.Node] = s;

        internal void ReplaceVariable(IVariable v) {
            VariableCollection.RemoveVariable(v.Name);
            VariableCollection.DeclareVariable(v.Name, v.Value, v.Source, v.Location);
        }

        private VariableCollection VariableCollection => _variables ?? (_variables = new VariableCollection());

        private void DeclareBuiltinVariables() {
            if (Node == null || Module.ModuleType != ModuleType.User || this is IGlobalScope) {
                return;
            }

            var location = new Location(Module);
            var strType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            var objType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Object);

            DeclareBuiltinVariable("__name__", strType, location);

            if (Node is FunctionDefinition) {
                var dictType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Dict);
                var tupleType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);

                DeclareBuiltinVariable("__closure__", tupleType, location);
                DeclareBuiltinVariable("__code__", objType, location);
                DeclareBuiltinVariable("__defaults__", tupleType, location);
                DeclareBuiltinVariable("__dict__", dictType, location);
                DeclareBuiltinVariable("__doc__", strType, location);
                DeclareBuiltinVariable("__func__", objType, location);
                DeclareBuiltinVariable("__globals__", dictType, location);
            } else if (Node is ClassDefinition) {
                DeclareBuiltinVariable("__self__", objType, location);
            }
        }

        protected void DeclareBuiltinVariable(string name, IPythonType type, Location location) 
            => VariableCollection.DeclareVariable(name, type, VariableSource.Builtin, location);
    }
}
