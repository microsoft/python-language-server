// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinModule : BuiltinNamespace<IPythonModule>, IReferenceableContainer, IModule {
        private readonly MemberReferences _references = new MemberReferences();

        public BuiltinModule(IPythonModule module, PythonAnalyzer projectState)
            : base(module, projectState) {
            InterpreterModule = module;
        }

        public IPythonModule InterpreterModule { get; }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _references.AddReference(node, unit, name);
            }
            return res;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            var res = ProjectState.GetAllMembers(InterpreterModule, moduleContext);
            foreach (var value in _specializedValues.MaybeEnumerate()) {
                if (!res.TryGetValue(value.Key, out var existing)) {
                    res[value.Key] = value.Value;
                } else {
                    res[value.Key] = existing.Union(value.Value);
                }
            }
            return res;
        }

        public override string Documentation => _type.Documentation;
        public override string Description => InterpreterModule.Name;
        public override string Name => InterpreterModule.Name;
        public override IPythonType PythonType => ProjectState.Types[BuiltinTypeId.Module];
        public override PythonMemberType MemberType => InterpreterModule.MemberType;
        public IPythonProjectEntry ProjectEntry => null;
        public IScope Scope => null;

        public override BuiltinTypeId TypeId => BuiltinTypeId.Module;

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _references.GetDefinitions(name, InterpreterModule, ProjectState._defaultContext);
        }

        #endregion

        internal IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            return _type.GetMemberNames(moduleContext)
                .Union(_specializedValues.MaybeEnumerate().Keys());
        }

        public IModule GetChildPackage(IModuleContext context, string name) {
            var mem = _type.GetMember(context, name);
            if (mem != null) {
                return ProjectState.GetAnalysisValueFromObjects(mem) as IModule;
            }
            return null;
        }

        public IEnumerable<KeyValuePair<string, AnalysisValue>> GetChildrenPackages(IModuleContext context) {
            return _type.GetChildrenModules()
                .Select(name => new KeyValuePair<string, AnalysisValue>(name, ProjectState.GetAnalysisValueFromObjects(_type.GetMember(context, name))));
        }

        public void SpecializeFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis) {
            int lastIndex;
            IAnalysisSet def;
            if (TryGetMember(name, out def)) {
                foreach (var v in def) {
                    if (!(v is SpecializedNamespace)) {
                        this[name] = new SpecializedCallable(v, callable, mergeOriginalAnalysis).SelfSet;
                        break;
                    }
                }
            } else if ((lastIndex = name.LastIndexOf('.')) != -1 &&
                TryGetMember(name.Substring(0, lastIndex), out def)) {
                var methodName = name.Substring(lastIndex + 1, name.Length - (lastIndex + 1));
                foreach (var v in def) {
                    BuiltinClassInfo ci = v as BuiltinClassInfo;
                    if (ci != null) {
                        IAnalysisSet classValue;
                        if (ci.TryGetMember(methodName, out classValue)) {
                            ci[methodName] = new SpecializedCallable(classValue.FirstOrDefault(), callable, mergeOriginalAnalysis).SelfSet;
                        } else {
                            ci[methodName] = new SpecializedCallable(null, callable, mergeOriginalAnalysis).SelfSet;
                        }

                        IAnalysisSet instValue;
                        if (ci.Instance.TryGetMember(methodName, out instValue)) {
                            ci.Instance[methodName] = new SpecializedCallable(
                                instValue.FirstOrDefault(),
                                ci.Instance,
                                callable,
                                mergeOriginalAnalysis).SelfSet;
                        } else {
                            ci.Instance[methodName] = new SpecializedCallable(
                                null,
                                ci.Instance,
                                callable,
                                mergeOriginalAnalysis).SelfSet;
                        }
                    }
                }
            } else {
                this[name] = new SpecializedCallable(null, callable, false);
            }
        }

        public void AddDependency(AnalysisUnit unit) {
            Imported(unit);
        }

        public override ILocatedMember GetLocatedMember() {
            return InterpreterModule as ILocatedMember;
        }


        public IAnalysisSet GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef = true, IScope linkedScope = null, string linkedName = null) {
            var res = GetMember(node, unit, name);
            Imported(unit);
            return res;
        }


        public IEnumerable<string> GetModuleMemberNames(IModuleContext context) {
            return GetMemberNames(context);
        }

        public void Imported(AnalysisUnit unit) {
            InterpreterModule.Imported(unit.DeclaringModule.InterpreterContext);
            unit.State.Modules.ImportChildren(InterpreterModule);
        }
    }
}
