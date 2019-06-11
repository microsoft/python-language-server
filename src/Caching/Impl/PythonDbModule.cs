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
using System.Runtime.Versioning;
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
        private readonly GlobalScopeImpl _globalScope;

        public PythonDbModule(ModuleModel model, IServiceContainer services) 
            : base(model.Name, string.Empty, services) {
            _model = model;
            _unknownType = services.GetService<IPythonInterpreter>().UnknownType;
            _globalScope = new GlobalScopeImpl(model, this);
        }

        protected override string LoadContent() => string.Empty;

        public override string Documentation => _model.Documentation;
        public override IEnumerable<string> GetMemberNames() => _globalScope.Variables.Names;
        public override IMember GetMember(string name) => _globalScope.Variables[name];
        public override IGlobalScope GlobalScope => _globalScope;

        private sealed class GlobalScopeImpl : IGlobalScope {
            private readonly VariableCollection _variables = new VariableCollection();

            public GlobalScopeImpl(ModuleModel model, IPythonModule module) {
                Module = module;

                foreach (var c in model.Classes) {
                    _variables.DeclareVariable(c.Name, ConstructClass(c), VariableSource.Declaration, new Location(module));
                }
                foreach (var f in model.Functions) {
                    _variables.DeclareVariable(f.Name, ConstructFunction(f), VariableSource.Declaration, new Location(module));
                }
                foreach (var v in model.Variables) {
                    var m = ConstructMember(v.Value);
                    if (m != null) {
                        _variables.DeclareVariable(v.Name, ConstructMember(v.Value), VariableSource.Declaration, new Location(module));
                    }
                }
            }

            private IPythonClassType ConstructClass(ClassModel cm) {
                var cls = new PythonClassType(cm.Name, new Location(Module));
                foreach (var f in cm.Methods) {
                    cls.AddMember(f.Name, ConstructFunction(f), false);
                }
                foreach (var p in cm.Properties) {
                    cls.AddMember(p.Name, ConstructProperty(p), false);
                }
                foreach (var c in cm.InnerClasses) {
                    cls.AddMember(c.Name, ConstructClass(c), false);
                }
            }

            private IPythonFunctionType ConstructFunction(FunctionModel fm, IPythonClassType cls) {
                var ft = new PythonFunctionType(fm.Name, new Location(Module), cls, fm.Documentation);
                foreach(var om in fm.Overloads) {
                    var o = new PythonFunctionOverload(fm.Name, new Location(Module));
                    o.SetDocumentation(fm.Documentation); // TODO: own documentation?
                    o.SetReturnValue(ConstructMember(om.ReturnType), true);
                    o.SetParameters(om.Parameters.Select(p => ConstructParameter(p)))
                    ft.AddOverload(o);
                }
            }

            private IPythonPropertyType ConstructProperty(PropertyModel pm, IPythonClassType cls) 
                => new PythonPropertyType(pm.Name, new Location(Module), cls, (pm.Attributes & FunctionAttributes.Abstract) != 0);

            private IParameterInfo ConstructParameter(ParameterModel pm)
                => new ParameterInfo(pm.Name, ConstructType(pm.Type), pm.Kind, ConstructMember(pm.DefaultValue));

            private IPythonType ConstructType(string qualifiedName) { }

            private IMember ConstructMember(string qualifiedName) {
                if (!SplitQualifiedName(qualifiedName, out var moduleName, out var typeName, out var isInstance)) {
                    return null;
                }

                if(moduleName == Module.Name) {
                    if(_model.g)
                }
                return isInstance ? new PythonInstance() : ;
            }

            private bool SplitQualifiedName(string qualifiedName, out string moduleName, out string typeName, out bool isInstance) {
                moduleName = null;
                typeName = null;

                isInstance = qualifiedName.StartsWith("i:");
                qualifiedName = isInstance ? qualifiedName.Substring(2) : qualifiedName;
                var components = qualifiedName.Split('.');
                switch (components.Length) {
                    case 1:
                        moduleName = "builtins";
                        typeName = components[0];
                        return true;
                    case 2:
                        moduleName = components[0];
                        typeName = components[1];
                        return true;
                }
                return false;
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
