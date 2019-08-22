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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class RestoredGlobalScope : IGlobalScope {
        private readonly VariableCollection _scopeVariables = new VariableCollection();
        private ModuleModel _model;

        public RestoredGlobalScope(ModuleModel model, IPythonModule module) {
            _model = model;
            Module = module;
            Name = model.Name;
        }

        public void ReconstructVariables() {
            // Member creation may be non-linear. Consider function A returning instance
            // of a class or type info of a function which hasn't been created yet.
            // Thus first create members so we can find then, then populate them with content.
            var mf = new ModuleFactory(_model, Module);
            var models = _model.TypeVars.Concat<MemberModel>(_model.NamedTuples).Concat(_model.Classes).Concat(_model.Functions).ToArray();

            foreach (var m in models) {
                _scopeVariables.DeclareVariable(m.Name, m.Create(mf, null), VariableSource.Generic, mf.DefaultLocation);
            }
            foreach (var vm in _model.Variables) {
                var v = (IVariable)vm.Create(mf, null);
                _scopeVariables.DeclareVariable(vm.Name, v.Value, VariableSource.Declaration, mf.DefaultLocation);
            }


            foreach (var m in models.Concat(_model.Variables)) {
                m.Populate(mf, null);
            }

            // TODO: re-declare __doc__, __name__, etc.
            _model = null;
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
        public IPythonModule Module { get; }
        IGlobalScope IScope.GlobalScope => this;

        public void DeclareVariable(string name, IMember value, VariableSource source, Location location = default) { }
        public void LinkVariable(string name, IVariable v, Location location) => throw new NotImplementedException() { };
        #endregion
    }
}
