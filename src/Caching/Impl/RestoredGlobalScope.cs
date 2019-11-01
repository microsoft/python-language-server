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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class RestoredGlobalScope : IGlobalScope {
        private readonly VariableCollection _scopeVariables = new VariableCollection();
        private ModuleModel _model; // Non-readonly b/c of DEBUG conditional.
        private ModuleFactory _factory; // Non-readonly b/c of DEBUG conditional.
        private bool _typeVarsCreated;

        public RestoredGlobalScope(ModuleModel model, IPythonModule module, IServiceContainer services) {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Name = model.Name;
            _factory = new ModuleFactory(_model, Module, this, services);
            DeclareVariables(services);
        }

        public void ReconstructVariable(string name) {
            EnsureTypeVars();
            var model = _model.NamedTuples.FirstOrDefault(x => x.Name == name)
                ?? _model.Classes.Cast<MemberModel>().FirstOrDefault(x => x.Name == name)
                ?? _model.Functions.Cast<MemberModel>().FirstOrDefault(x => x.Name == name)
                //?? _model.SubModules<MemberModel>.FirstOrDefault(x => x.Name == name)
                ?? _model.Variables.FirstOrDefault(x => x.Name == name);

            model?.Populate(_factory, null, this);

            // TODO: re-declare __doc__, __name__, etc.
#if !DEBUG
            _model = null;
            _factory = null;
#endif
        }

        private void EnsureTypeVars() {
            if (!_typeVarsCreated) {
                foreach (var m in _model.TypeVars) {
                    m.Populate(_factory, null, this);
                }
                _typeVarsCreated = true;
            }
        }

        private void DeclareVariables(IServiceContainer services) {
            // Member creation may be non-linear. Consider function A returning instance
            // of a class or type info of a function which hasn't been created yet.
            // Thus first create members so we can find then, then populate them with content.
            var mf = new ModuleFactory(_model, Module, this, services);

            // Generics first
            foreach (var m in _model.TypeVars) {
                _scopeVariables.DeclareVariable(m.Name, m.Create(mf, null, this), VariableSource.Generic, mf.DefaultLocation);
            }

            var members = _model.NamedTuples
                .Concat<MemberModel>(_model.Classes).Concat(_model.Functions); //.Concat(_model.SubModules);
            foreach (var m in members) {
                _scopeVariables.DeclareVariable(m.Name, m.Create(mf, null, this), VariableSource.Declaration, mf.DefaultLocation);
            }

            // Declare variables in the order of appearance since later variables
            // may use types declared in the preceding ones.
            foreach (var vm in _model.Variables.OrderBy(m => m.IndexSpan.Start)) {
                var v = (IVariable)vm.Create(mf, null, this);
                _scopeVariables.DeclareVariable(vm.Name, v.Value, VariableSource.Declaration, mf.DefaultLocation);
            }
        }

        #region IScope
        public string Name { get; }
        public ScopeStatement Node => null;
        public IScope OuterScope => null;
        public IReadOnlyList<IScope> Children => Array.Empty<IScope>();
        public IEnumerable<IScope> EnumerateTowardsGlobal => Enumerable.Empty<IScope>();
        public IEnumerable<IScope> EnumerateFromGlobal => Enumerable.Empty<IScope>();
        public IVariableCollection Variables => _scopeVariables;
        public IVariableCollection NonLocals => VariableCollection.Empty;
        public IVariableCollection Globals => VariableCollection.Empty;
        public IVariableCollection Imported => VariableCollection.Empty;
        public IPythonModule Module { get; }
        IGlobalScope IScope.GlobalScope => this;

        public void DeclareVariable(string name, IMember value, VariableSource source, Location location = default) { }
        public void LinkVariable(string name, IVariable v, Location location) => throw new NotImplementedException() { };
        #endregion
    }
}
