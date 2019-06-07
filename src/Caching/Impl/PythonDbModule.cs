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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class PythonDbModule : SpecializedModule {
        private readonly ModuleModel _model;
        private readonly IMember _unknownType;

        public PythonDbModule(ModuleModel model, IServiceContainer services) 
            : base(model.Name, string.Empty, services) {
            _model = model;
            _unknownType = services.GetService<IPythonInterpreter>().UnknownType;
        }

        protected override string LoadContent() => string.Empty;

        public override IEnumerable<string> GetMemberNames() {
            var classes = _model.Classes.Select(c => c.Name);
            var functions = _model.Functions.Select(c => c.Name);
            var variables = _model.Variables.Select(c => c.Name);
            return classes.Concat(functions).Concat(variables);
        }

        public override IMember GetMember(string name) {
            var v = _model.Variables.FirstOrDefault(c => c.Name == name);
            if(v != null) {
                return new Variable(name, Construct(v.Value), VariableSource.Declaration, new Location(this));
            }
            var v = _model.Variables.FirstOrDefault(c => c.Name == name);
            if (v != null) {
                return new Variable(name, Construct(v.Value), VariableSource.Declaration, new Location(this));
            }

            return _unknownType;
        }

        public override IGlobalScope GlobalScope => base.GlobalScope;

        private IMember Construct(string qualifiedName) {
            var components = Split(qualifiedName, out var moduleName, out var isInstance);
            return null;
        }

        private string[] Split(string qualifiedName, out string moduleName, out bool isInstance) {
            isInstance = qualifiedName.StartsWith("i:");
            qualifiedName = isInstance ? qualifiedName.Substring(2) : qualifiedName;
            var components = qualifiedName.Split('.');
            moduleName = components.Length > 0 ? components[0] : null;
            return components.Length > 0 ? components.Skip(1).ToArray() : Array.Empty<string>();
        }

        private sealed class GlobalScope : IGlobalScope {
            private readonly ModuleModel _model;
            private readonly VariableCollection _variables = new VariableCollection();
            public GlobalScope(ModuleModel model, IPythonModule module) {
                _model = model;
                Module = module;

                foreach (var v in model.Variables) {
                    _variables.DeclareVariable(v.Name, Construct(v.Value), VariableSource.Declaration, new Location(module));
                }
                foreach (var c in model.Classes) {
                    _variables.DeclareVariable(c.Name, Construct(c), VariableSource.Declaration, new Location(module));
                }
                foreach (var f in model.Functions) {
                    _variables.DeclareVariable(f.Name, Construct(f), VariableSource.Declaration, new Location(module));
                }
                // TODO: classes and functions
                // TODO: default variables
            }

            public string Name => _model.Name;
            public ScopeStatement Node => null;
            public IScope OuterScope => null;
            public IReadOnlyList<IScope> Children => Array.Empty<IScope>();
            public IEnumerable<IScope> EnumerateTowardsGlobal => Enumerable.Empty<IScope>();
            public IEnumerable<IScope> EnumerateFromGlobal => Enumerable.Empty<IScope>();
            public IVariableCollection Variables => _variables;
            public IVariableCollection NonLocals => VariableCollection.Empty;
            public IVariableCollection Globals => VariableCollection.Empty;
            public IPythonModule Module { get; }
            IGlobalScope IScope.GlobalScope => this;

            public void DeclareVariable(string name, IMember value, VariableSource source, Location location = default) { }
            public void LinkVariable(string name, IVariable v, Location location) => throw new NotImplementedException() { };
        }
    }
}
