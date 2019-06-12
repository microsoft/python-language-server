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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class PythonDbModule : SpecializedModule {
        private readonly GlobalScopeImpl _globalScope;
        private readonly IPythonInterpreter _interpreter;

        public PythonDbModule(ModuleModel model, IServiceContainer services)
            : base(model.Name, string.Empty, services) {

            _globalScope = new GlobalScopeImpl(model, this, services);
            Documentation = model.Documentation;
        }

        protected override string LoadContent() => string.Empty;

        public override string Documentation { get; }
        public override IEnumerable<string> GetMemberNames() => _globalScope.Variables.Names;
        public override IMember GetMember(string name) => _globalScope.Variables[name];
        public override IGlobalScope GlobalScope => _globalScope;

        private sealed class GlobalScopeImpl : IGlobalScope {
            private class ModelMemberPair {
                public MemberModel Model;
                public IMember Member;
            }
            private readonly VariableCollection _variables = new VariableCollection();
            private readonly Dictionary<string, ModelMemberPair> _members;
            private readonly IPythonInterpreter _interpreter;

            public GlobalScopeImpl(ModuleModel model, IPythonModule module, IServiceContainer services) {
                Module = module;
                Name = model.Name;

                _interpreter = services.GetService<IPythonInterpreter>();
                _members = model.Variables
                    .Concat<MemberModel>(model.Classes)
                    .Concat(model.Functions)
                    .ToDictionary(k => k.Name, v => new ModelMemberPair {
                        Model = v,
                        Member = null
                    });

                // TODO: store real location in models

                // Member creation may be non-linear. Consider function A returning instance
                // of a class or type info of a function which hasn't been created yet.
                // Thus check if member has already been created first.
                var location = new Location(module);
                foreach (var cm in model.Classes) {
                    var cls = ConstructClass(cm);
                    _variables.DeclareVariable(cm.Name, cls, VariableSource.Declaration, location);
                }

                foreach (var fm in model.Functions) {
                    var ft = ConstructFunction(fm, null);
                    _variables.DeclareVariable(fm.Name, ft, VariableSource.Declaration, location);
                }

                foreach (var vm in model.Variables) {
                    var m = _members[vm.Name].Member;
                    if (m == null) {
                        m = ConstructMember(vm.Value);
                        if (m != null) {
                            _variables.DeclareVariable(vm.Name, m, VariableSource.Declaration, location);
                            _members[vm.Name].Member = _variables[vm.Name];
                        }
                    }
                }
                // TODO: re-declare __doc__, __name__, etc.
                // No longer need model data, free up some memory.
                _members.Clear();
            }

            private IPythonClassType ConstructClass(ClassModel cm) {
                var pair = _members[cm.Name];

                var m = pair.Member;
                if (m != null) {
                    var ct = m as IPythonClassType;
                    Debug.Assert(ct != null);
                    return ct;
                }

                var cls = new PythonClassType(cm.Name, new Location(Module));
                foreach (var f in cm.Methods) {
                    cls.AddMember(f.Name, ConstructFunction(f, cls), false);
                }
                foreach (var p in cm.Properties) {
                    cls.AddMember(p.Name, ConstructProperty(p, cls), false);
                }
                foreach (var c in cm.InnerClasses) {
                    cls.AddMember(c.Name, ConstructClass(c), false);
                }

                pair.Member = cls;
                return cls;
            }

            private IPythonFunctionType ConstructFunction(FunctionModel fm, IPythonClassType cls) {
                var pair = _members[fm.Name];

                var m = pair.Member;
                if (m != null) {
                    var ft = m as IPythonFunctionType;
                    Debug.Assert(ft != null);
                    return ft;
                }

                var f = new PythonFunctionType(fm.Name, new Location(Module), cls, fm.Documentation);
                foreach (var om in fm.Overloads) {
                    var o = new PythonFunctionOverload(fm.Name, new Location(Module));
                    o.SetDocumentation(fm.Documentation); // TODO: own documentation?
                    o.SetReturnValue(ConstructMember(om.ReturnType), true);
                    o.SetParameters(om.Parameters.Select(ConstructParameter).ToArray());
                    f.AddOverload(o);
                }
                pair.Member = f;
                return f;
            }

            private IVariable ConstructVariable(VariableModel vm) {
                var pair = _members[vm.Name];
                var m = pair.Member;
                if (m != null) {
                    var v = m as IVariable;
                    Debug.Assert(v != null);
                    return v;
                }

                m = ConstructMember(vm.Value);
                if (m != null) {
                    _variables.DeclareVariable(vm.Name, m, VariableSource.Declaration, new Location(Module));
                    var v = _variables[vm.Name];
                    _members[vm.Name].Member = v;
                    return v;
                }

                return null;
            }

            private IPythonPropertyType ConstructProperty(PropertyModel pm, IPythonClassType cls) {
                var prop = new PythonPropertyType(pm.Name, new Location(Module), cls, (pm.Attributes & FunctionAttributes.Abstract) != 0);
                prop.SetDocumentation(pm.Documentation);
                var o = new PythonFunctionOverload(pm.Name, new Location(Module));
                o.SetDocumentation(pm.Documentation); // TODO: own documentation?
                o.SetReturnValue(ConstructMember(pm.ReturnType), true);
                prop.AddOverload(o);
                return prop;
            }

            private IParameterInfo ConstructParameter(ParameterModel pm)
                => new ParameterInfo(pm.Name, ConstructType(pm.Type), pm.Kind, ConstructMember(pm.DefaultValue));

            private IPythonType ConstructType(string qualifiedName) => ConstructMember(qualifiedName)?.GetPythonType();

            private IMember ConstructMember(string qualifiedName) {
                if (!SplitQualifiedName(qualifiedName, out var moduleName, out var typeNameParts, out var isInstance)) {
                    return null;
                }

                Debug.Assert(typeNameParts.Count > 0);
                var member = moduleName == Module.Name
                    ? GetMemberFromThisModule(typeNameParts)
                    : GetMemberFromModule(moduleName, typeNameParts);

                return isInstance && member != null ? new PythonInstance(member.GetPythonType()) : member;
            }

            private IMember GetMemberFromModule(string moduleName, IReadOnlyList<string> typeNameParts) {
                // Module resolution will call back to the module database
                // to get persisted analysis, if available.
                var module = _interpreter.ModuleResolution.GetOrLoadModule(moduleName);
                var member = module?.GetMember(typeNameParts[0]);
                foreach (var p in typeNameParts.Skip(1)) {
                    var mc = member as IMemberContainer;

                    Debug.Assert(mc != null);
                    member = mc?.GetMember(p);

                    if (member == null) {
                        Debug.Assert(member != null);
                        break;
                    }
                }
                return member;
            }

            private IMember GetMemberFromThisModule(IReadOnlyList<string> typeNameParts) {
                if (typeNameParts.Count == 0 || !_members.TryGetValue(typeNameParts[0], out var memberData)) {
                    return null;
                }

                if (memberData.Member != null) {
                    return memberData.Member;
                }

                // TODO: nested classes, etc
                switch (memberData.Model) {
                    case ClassModel cm:
                        Debug.Assert(typeNameParts.Count == 1);
                        return ConstructClass(cm);

                    case FunctionModel fm:
                        Debug.Assert(typeNameParts.Count == 1);
                        return ConstructFunction(fm, null);

                    case VariableModel vm:
                        Debug.Assert(typeNameParts.Count == 1);
                        return ConstructVariable(vm);
                }
                return null;
            }

            private bool SplitQualifiedName(string qualifiedName, out string moduleName, out List<string> typeNameParts, out bool isInstance) {
                moduleName = null;
                typeNameParts = new List<string>();

                isInstance = qualifiedName.StartsWith("i:");
                qualifiedName = isInstance ? qualifiedName.Substring(2) : qualifiedName;
                var components = qualifiedName.Split('.');
                switch (components.Length) {
                    case 0:
                        return false;
                    case 1:
                        moduleName = @"builtins";
                        typeNameParts.Add(components[0]);
                        return true;
                    default:
                        moduleName = components[0];
                        typeNameParts.AddRange(components.Skip(1));
                        return true;
                }
            }

            public string Name { get; }
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
