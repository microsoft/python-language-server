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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Interpreter;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ModuleInfo : AnalysisValue, IReferenceableContainer, IModuleInfo {
        private readonly ProjectEntry _projectEntry;
        private readonly Dictionary<Node, InterpreterScope> _scopes;    // scopes from Ast node to InterpreterScope
        private Dictionary<string, WeakReference> _packageModules;
        private Dictionary<string, Tuple<CallDelegate, bool>> _specialized;
        private readonly Dictionary<string, ModuleReference> _referencedModules;
        private readonly HashSet<string> _unresolvedModules;

        public ModuleInfo(string moduleName, ProjectEntry projectEntry, IModuleContext moduleContext) {
            Name = moduleName;
            _projectEntry = projectEntry;
            Scope = new ModuleScope(this);
            WeakModule = new WeakReference(this);
            InterpreterContext = moduleContext;
            _scopes = new Dictionary<Node, InterpreterScope>();
            _referencedModules = new Dictionary<string, ModuleReference>();
            _unresolvedModules = new HashSet<string>(StringComparer.Ordinal);
        }

        internal void Clear() {
            lock (_projectEntry) {
                Scope.ClearLinkedVariables();
                Scope.ClearVariables();
                Scope.ClearNodeScopes();
                _unresolvedModules.Clear();
                ClearReferencedModules();
            }
        }

        internal void EnsureModuleVariables(PythonAnalyzer state) {
            var entry = ProjectEntry;

            Scope.SetModuleVariable("__builtins__", state.ClassInfos[BuiltinTypeId.Dict].Instance);
            Scope.SetModuleVariable("__file__", GetStr(state, entry.FilePath));
            Scope.SetModuleVariable("__name__", GetStr(state, Name));
            Scope.SetModuleVariable("__package__", GetStr(state, ParentPackage?.Name));
            if (state.LanguageVersion.Is3x()) {
                Scope.SetModuleVariable("__cached__", GetStr(state));
                if (ModulePath.IsInitPyFile(entry.FilePath)) {
                    Scope.SetModuleVariable("__path__", state.ClassInfos[BuiltinTypeId.List].Instance);
                }
                Scope.SetModuleVariable("__spec__", state.ClassInfos[BuiltinTypeId.Object].Instance);
            }
            ModuleDefinition.EnqueueDependents();

        }
        private static IAnalysisSet GetStr(PythonAnalyzer state, string s = null) {
            if (string.IsNullOrEmpty(s)) {
                return state.ClassInfos[BuiltinTypeId.Str].Instance;
            }
            if (state.LanguageVersion.Is2x()) {
                return state.GetConstant(new AsciiString(new UTF8Encoding(false).GetBytes(s), s));
            }
            return state.GetConstant(s);
        }

        /// <inheritdoc />
        /// <summary>
        /// Returns all the absolute module names that need to be resolved from
        /// this module.
        /// Note that a single import statement may add multiple names to this
        /// set, and so the Count property does not accurately reflect the 
        /// actual number of imports required.
        /// </summary>
        public ISet<string> GetAllUnresolvedModules() => _unresolvedModules;

        internal void AddUnresolvedModule(in string fullModuleName) {
            _unresolvedModules.Add(fullModuleName);
            _projectEntry.ProjectState.ModuleHasUnresolvedImports(this, true);
        }

        internal void ClearUnresolvedModules() {
            _unresolvedModules.Clear();
            _projectEntry.ProjectState.ModuleHasUnresolvedImports(this, false);
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            var res = new Dictionary<string, IAnalysisSet>();
            foreach (var kvp in Scope.AllVariables) {
                if (!options.ForEval()) {
                    kvp.Value.ClearOldValues();
                }

                if (kvp.Value._dependencies.Count > 0) {
                    var types = kvp.Value.Types;
                    if (types.Count > 0) {
                        res[kvp.Key] = types;
                    }
                }
            }
            return res;
        }

        public IModuleContext InterpreterContext { get; }

        public ModuleInfo ParentPackage { get; set; }

        public void AddChildPackage(ModuleInfo childPackage, AnalysisUnit curUnit, string realName = null) {
            realName = realName ?? childPackage.Name;
            int lastDot;
            if ((lastDot = realName.LastIndexOf('.')) != -1) {
                realName = realName.Substring(lastDot + 1);
            }

            childPackage.ParentPackage = this;
            Scope.SetVariable(childPackage.ProjectEntry.Tree, curUnit, realName, childPackage.SelfSet, false);

            if (_packageModules == null) {
                _packageModules = new Dictionary<string, WeakReference>();
            }
            _packageModules[realName] = childPackage.WeakModule;
        }

        public IEnumerable<KeyValuePair<string, AnalysisValue>> GetChildrenPackages(IModuleContext moduleContext) {
            if (_packageModules != null) {
                foreach (var keyValue in _packageModules) {
                    if (keyValue.Value.Target is IModule res) {
                        yield return new KeyValuePair<string, AnalysisValue>(keyValue.Key, (AnalysisValue)res);
                    }
                }
            }
        }

        public IModule GetChildPackage(IModuleContext moduleContext, string name) {
            if (_packageModules != null && _packageModules.TryGetValue(name, out var weakMod)) {
                var res = weakMod.Target;
                if (res != null) {
                    return (IModule)res;
                }

                _packageModules.Remove(name);
            } else if (Scope != null && Scope.TryGetVariable(name, out var variable)) {
                return variable.Types.OfType<IModule>().FirstOrDefault();
            }

            return null;
        }

        public bool TryGetModuleReference(string moduleName, out ModuleReference moduleReference) {
            lock (_projectEntry) {
                return _referencedModules.TryGetValue(moduleName, out moduleReference);
            }
        }

        public void AddModuleReference(string moduleName, ModuleReference moduleReference) {
            Check.ArgumentNotNull(nameof(moduleName), moduleName);
            Check.ArgumentNotNull(nameof(moduleReference), moduleReference);

            _referencedModules[moduleName] = moduleReference;
            moduleReference.AddReference(this);
        }

        public void ClearReferencedModules() {
            foreach (var moduleReference in _referencedModules.Values.Distinct()) {
                moduleReference.RemoveReference(this);
            }
            _referencedModules.Clear();
        }

        public void SpecializeFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis) {
            lock (this) {
                if (_specialized == null) {
                    _specialized = new Dictionary<string, Tuple<CallDelegate, bool>>();
                }
                _specialized[name] = Tuple.Create(callable, mergeOriginalAnalysis);
            }
        }

        internal void Specialize() {
            lock (this) {
                if (_specialized != null) {
                    foreach (var keyValue in _specialized) {
                        SpecializeOneFunction(keyValue.Key, keyValue.Value.Item1, keyValue.Value.Item2);
                    }
                }
            }
        }

        private void SpecializeOneFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis) {
            int lastIndex;
            if (Scope.TryGetVariable(name, out var def)) {
                SpecializeVariableDef(def, callable, mergeOriginalAnalysis);
            } else if ((lastIndex = name.LastIndexOf('.')) != -1 &&
                Scope.TryGetVariable(name.Substring(0, lastIndex), out def)) {
                var methodName = name.Substring(lastIndex + 1, name.Length - (lastIndex + 1));
                foreach (var v in def.Types) {
                    if (v is ClassInfo ci && ci.Scope.TryGetVariable(methodName, out var methodDef)) {
                        SpecializeVariableDef(methodDef, callable, mergeOriginalAnalysis);
                    }
                }
            }
        }

        private static void SpecializeVariableDef(VariableDef def, CallDelegate callable, bool mergeOriginalAnalysis) {
            var items = new List<AnalysisValue>();
            foreach (var v in def.Types) {
                if (!(v is SpecializedNamespace) && v.DeclaringModule != null) {
                    items.Add(v);
                }
            }

            def._dependencies = default(SingleDict<IVersioned, ReferenceableDependencyInfo>);
            foreach (var item in items) {
                def.AddTypes(item.DeclaringModule, new SpecializedCallable(item, callable, mergeOriginalAnalysis).SelfSet);
            }
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            return AnalysisSet.Empty;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (unit.ForEval) {
                return Scope.TryGetVariable(name, out var value) ? value.Types : AnalysisSet.Empty;
            }

            ModuleDefinition.AddDependency(unit);
            return Scope.CreateEphemeralVariable(node, unit, name).Types;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            var variable = Scope.CreateVariable(node, unit, name, false);
            if (variable.AddTypes(unit, value, true, ProjectEntry)) {
                ModuleDefinition.EnqueueDependents();
            }

            variable.AddAssignment(node, unit);
        }

        /// <summary>
        /// Gets a weak reference to this module
        /// </summary>
        public WeakReference WeakModule { get; }

        public DependentData ModuleDefinition { get; } = new DependentData();

        public ModuleScope Scope { get; }
        IScope IModule.Scope => Scope;

        public override string Name { get; }

        public ProjectEntry ProjectEntry => _projectEntry;
        IPythonProjectEntry IModule.ProjectEntry => ProjectEntry;

        public override PythonMemberType MemberType => PythonMemberType.Module;

        public override string ToString() => $"Module {base.ToString()}";
        public override string ShortDescription => $"Python module {Name}";

        public override string Description {
            get {
                var result = new StringBuilder("Python module ");
                result.Append(Name);
                var doc = Documentation;
                if (!string.IsNullOrEmpty(doc)) {
                    result.Append("\n\n");
                    result.Append(doc);
                }
                return result.ToString();
            }
        }

        public override string Documentation => ProjectEntry.Tree?.Body?.Documentation.TrimDocumentation() ?? string.Empty;

        public override IEnumerable<ILocationInfo> Locations
             => new[] { new LocationInfo(ProjectEntry.FilePath, ProjectEntry.DocumentUri, 1, 1) };

        public override IPythonType PythonType
            => ProjectEntry.ProjectState.Types[BuiltinTypeId.Module];

        public override BuiltinTypeId TypeId => BuiltinTypeId.Module;

        #region IVariableDefContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            if (Scope.TryGetVariable(name, out var def)) {
                yield return def;
            }
        }

        #endregion

        IAnalysisSet IModule.GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef, IScope linkedScope, string linkedName)
            => GetModuleMember(node, unit, name, addRef, linkedScope as InterpreterScope, linkedName);

        public IAnalysisSet GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef = true, InterpreterScope linkedScope = null, string linkedName = null) {
            var importedValue = Scope.CreateEphemeralVariable(node, unit, name, addRef);
            ModuleDefinition.AddDependency(unit);

            linkedScope?.AddLinkedVariable(linkedName ?? name, importedValue);
            return importedValue.GetTypesNoCopy(unit, DeclaringModule);
        }

        public IEnumerable<string> GetModuleMemberNames(IModuleContext context) {
            return Scope.AllVariables.Select(kv => kv.Key);
        }

        public bool IsMemberDefined(IModuleContext context, string member) {
            if (Scope.TryGetVariable(member, out var v)) {
                return v.Types.Any(m => m.DeclaringModule == _projectEntry);
            }
            return false;
        }

        public void Imported(AnalysisUnit unit) {
            ModuleDefinition.AddDependency(unit);
        }
    }
}
