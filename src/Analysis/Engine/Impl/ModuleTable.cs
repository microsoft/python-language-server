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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Maintains the list of modules loaded into the PythonAnalyzer.
    /// 
    /// This keeps track of the builtin modules as well as the user defined modules.  It's wraps
    /// up various elements we need to keep track of such as thread safety and lazy loading of built-in
    /// modules.
    /// </summary>
    class ModuleTable {
        private readonly IPythonInterpreter _interpreter;
        private readonly PythonAnalyzer _analyzer;
        private readonly ConcurrentDictionary<IPythonModule, BuiltinModule> _builtinModuleTable = new ConcurrentDictionary<IPythonModule, BuiltinModule>();
        private readonly ConcurrentDictionary<string, ModuleReference> _modules = new ConcurrentDictionary<string, ModuleReference>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, Func<BuiltinModule, BuiltinModule>> _builtinModuleType = new ConcurrentDictionary<string, Func<BuiltinModule, BuiltinModule>>();

        public ModuleTable(PythonAnalyzer analyzer, IPythonInterpreter interpreter) {
            Check.ArgumentNotNull(nameof(analyzer), analyzer);
            _analyzer = analyzer;
            _interpreter = interpreter;

        }
        
        public void AddBuiltinModuleWrapper(string moduleName, Func<BuiltinModule, BuiltinModule> moduleWrapper) {
            _builtinModuleType[moduleName] = moduleWrapper;
        }

        /// <summary>
        /// Gets a reference to a module that has already been imported. You
        /// probably want to use <see cref="TryImport"/>.
        /// </summary>
        /// <returns>
        /// True if an attempt to import the module was made during the analysis
        /// that used this module table. The reference may be null, or the
        /// module within the reference may be null, even if this function
        /// returns true.
        /// </returns>
        /// <remarks>
        /// This exists for inspecting the results of an analysis (for example,
        /// <see cref="SaveAnalysis"/>). To get access to a module while
        /// analyzing code, even (especially!) if the module may not exist,
        /// you should call <see cref="TryImport"/>.
        /// </remarks>
        internal bool TryGetImportedModule(string name, out ModuleReference res) {
            return _modules.TryGetValue(name, out res);
        }

        /// <summary>
        /// Gets a reference to a module.
        /// </summary>
        /// <param name="name">The full import name of the module.</param>
        /// <param name="token"></param>
        /// <returns>
        /// True if the module is available. This means that <c>moduleReference.Module</c>
        /// is not null. If this function returns false, <paramref name="res"/>
        /// may be valid and should not be replaced, but it is an unresolved
        /// reference.
        /// </returns>
        public async Task<ModuleReference> TryImportAsync(string name, CancellationToken token) {
            var firstImport = false;

            if (!_modules.TryGetValue(name, out var moduleReference) || moduleReference == null) {
                var pythonModule = await ImportModuleAsync(name, token).ConfigureAwait(false);
                moduleReference = SetModule(name, GetBuiltinModule(pythonModule));
                firstImport = true;
            }

            if (moduleReference.Module == null) {
                var pythonModule = await ImportModuleAsync(name, token).ConfigureAwait(false);
                moduleReference.Module = GetBuiltinModule(pythonModule);
            }

            if (firstImport && moduleReference.Module != null) {
                _analyzer.DoDelayedSpecialization(name);
            }

            return moduleReference.Module == null ? null : moduleReference;
        }

        private Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token) {
            return _interpreter is IPythonInterpreter2 interpreter2
                ? interpreter2.ImportModuleAsync(name, token)
                : Task.Run(() => _interpreter.ImportModule(name), token);
        }

        /// <summary>
        /// Gets a reference to a module.
        /// </summary>
        /// <param name="name">The full import name of the module.</param>
        /// <param name="moduleReference">The module reference object.</param>
        /// <returns>
        /// True if the module is available. This means that <c>moduleReference.Module</c>
        /// is not null. If this function returns false, <paramref name="moduleReference"/>
        /// may be valid and should not be replaced, but it is an unresolved
        /// reference.
        /// </returns>
        public bool TryImport(string name, out ModuleReference moduleReference) {
            var firstImport = false;

            if (!_modules.TryGetValue(name, out moduleReference) || moduleReference == null) {
                var pythonModule = _interpreter.ImportModule(name);
                moduleReference = SetModule(name, GetBuiltinModule(pythonModule));
                firstImport = true;
            }

            if (moduleReference.Module == null) {
                moduleReference.Module = GetBuiltinModule(_interpreter.ImportModule(name));
            }

            if (firstImport && moduleReference.Module != null) {
                _analyzer.DoDelayedSpecialization(name);
            }

            return moduleReference.Module != null;
        }

        public bool TryRemove(string name, out ModuleReference res) => _modules.TryRemove(name, out res);
        public ModuleReference SetModule(string name, IModule module) => _modules.AddOrUpdate(name, new ModuleReference(module, name), (_, reference) => {
                reference.Module = module;
                return reference;
            });

        public ModuleReference GetOrAdd(string name) {
            return _modules.GetOrAdd(name, _ => new ModuleReference(name: name));
        }

        /// <summary>
        /// Reloads the modules when the interpreter says they've changed.
        /// Modules that are already in the table as builtins are replaced or
        /// removed, but no new modules are added.
        /// </summary>
        public void Reload() {
            var newNames = new HashSet<string>(_interpreter.GetModuleNames(), StringComparer.Ordinal);

            foreach (var keyValue in _modules) {
                var name = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (moduleRef.Module is BuiltinModule builtinModule) {
                    IPythonModule newModule = null;
                    if (newNames.Contains(name)) {
                        newModule = _interpreter.ImportModule(name);
                    }

                    if (newModule == null) {
                        // this module was unloaded
                        ModuleReference dummy;
                        TryRemove(name, out dummy);

                        _builtinModuleTable.TryRemove(builtinModule.InterpreterModule, out _);
                        foreach (var child in builtinModule.InterpreterModule.GetChildrenModules()) {
                            TryRemove(builtinModule.Name + "." + child, out dummy);
                        }
                    } else if (builtinModule.InterpreterModule != newModule) {
                        // this module was replaced with a new module
                        _builtinModuleTable.TryRemove(builtinModule.InterpreterModule, out _);
                        moduleRef.Module = GetBuiltinModule(newModule);
                        ImportChildren(newModule);
                    }
                }
            }
        }

        internal BuiltinModule GetBuiltinModule(IPythonModule attr) {
            if (attr == null) {
                return null;
            }

            if (!_builtinModuleTable.TryGetValue(attr, out var res)) {
                res = new BuiltinModule(attr, _analyzer);
                if (_builtinModuleType.TryGetValue(attr.Name, out var wrap) && wrap != null) {
                    res = wrap(res);
                }
                _builtinModuleTable[attr] = res;
            }

            return res;
        }

        internal void ImportChildren(IPythonModule interpreterModule) {
            BuiltinModule builtinModule = null;
            foreach (var child in interpreterModule.GetChildrenModules()) {
                builtinModule = builtinModule ?? GetBuiltinModule(interpreterModule);
                var fullname = builtinModule.Name + "." + child;

                if (!_modules.TryGetValue(fullname, out var modRef) || modRef?.Module == null) {
                    if (builtinModule.TryGetMember(child, out var value) && value is IModule module) {
                        SetModule(fullname, module);
                        _analyzer?.DoDelayedSpecialization(fullname);
                    }
                }
            }
        }

        #region IEnumerable<KeyValuePair<string, ModuleLoadState>> GetModuleStates

        public IEnumerable<KeyValuePair<string, ModuleLoadState>> GetModuleStates() {
            var unloadedNames = new HashSet<string>(_interpreter.GetModuleNames(), StringComparer.Ordinal);
            var unresolvedNames = _analyzer?.GetAllUnresolvedModuleNames();

            foreach (var keyValue in _modules) {
                unresolvedNames?.Remove(keyValue.Key);

                if (keyValue.Value.Module is Interpreter.Ast.AstNestedPythonModule anpm && !anpm.IsLoaded) {
                    continue;
                }

                unloadedNames.Remove(keyValue.Key);
                yield return new KeyValuePair<string, ModuleLoadState>(keyValue.Key, new InitializedModuleLoadState(keyValue.Value));
            }

            foreach (var name in unloadedNames) {
                yield return new KeyValuePair<string, ModuleLoadState>(name, new UninitializedModuleLoadState(this, name));
            }

            if (unresolvedNames != null) {
                foreach (var name in unresolvedNames) {
                    yield return new KeyValuePair<string, ModuleLoadState>(name, new UnresolvedModuleLoadState());
                }
            }
        }

        private class UnresolvedModuleLoadState : ModuleLoadState {
            public override AnalysisValue Module {
                get { return null; }
            }

            public override bool HasModule {
                get { return false; }
            }

            public override bool HasReferences {
                get { return false; }
            }

            public override bool IsValid {
                get { return true; }
            }

            public override string Name {
                get { return null; }
            }

            public override PythonMemberType MemberType {
                get { return PythonMemberType.Unknown; }
            }

            internal override bool ModuleContainsMember(IModuleContext context, string name) {
                return false;
            }
        }

        private class UninitializedModuleLoadState : ModuleLoadState {
            private readonly ModuleTable _moduleTable;
            private readonly string _name;
            private PythonMemberType? _type;

            public UninitializedModuleLoadState(
                ModuleTable moduleTable,
                string name
            ) {
                this._moduleTable = moduleTable;
                this._name = name;
            }

            public override AnalysisValue Module {
                get {
                    ModuleReference res;
                    if (_moduleTable.TryImport(_name, out res)) {
                        _type = res.AnalysisModule?.MemberType;
                        return res.AnalysisModule;
                    }
                    return null;
                }
            }

            public override bool IsValid {
                get {
                    return true;
                }
            }

            public override bool HasReferences {
                get {
                    return false;
                }
            }

            public override bool HasModule {
                get {
                    return true;
                }
            }

            public override string Name {
                get {
                    return _name;
                }
            }

            public override string MaybeSourceFile {
                get {
                    ModuleReference res;
                    if (_moduleTable.TryGetImportedModule(_name, out res)) {
                        return res.AnalysisModule?.DeclaringModule?.FilePath;
                    }
                    return null;
                }
            }

            public override PythonMemberType MemberType {
                get {
                    return _type ?? PythonMemberType.Module;
                }
            }

            internal override bool ModuleContainsMember(IModuleContext context, string name) {
                var mod = _moduleTable._interpreter.ImportModule(_name);
                if (mod != null) {
                    return BuiltinModuleContainsMember(context, name, mod);
                }
                return false;
            }

        }

        private class InitializedModuleLoadState : ModuleLoadState {
            private readonly ModuleReference _reference;

            public InitializedModuleLoadState(ModuleReference reference) {
                if (reference == null) {
                    throw new ArgumentNullException(nameof(reference));
                }
                _reference = reference;
            }

            public override AnalysisValue Module {
                get {
                    return _reference.AnalysisModule;
                }
            }

            public override bool HasReferences {
                get {
                    return _reference.HasReferences;
                }
            }

            public override bool IsValid {
                get {
                    return Module != null || HasReferences;
                }
            }

            public override bool HasModule {
                get {
                    return Module != null;
                }
            }

            public override string Name => _reference.Name;
            public override string MaybeSourceFile => Module?.DeclaringModule?.FilePath;

            public override PythonMemberType MemberType {
                get {
                    if (Module != null) {
                        return Module.MemberType;
                    }
                    return PythonMemberType.Module;
                }
            }

            internal override bool ModuleContainsMember(IModuleContext context, string name) {
                var builtin = Module as BuiltinModule;
                if (builtin != null) {
                    return BuiltinModuleContainsMember(context, name, builtin.InterpreterModule);
                }

                var modInfo = Module as ModuleInfo;
                if (modInfo != null) {
                    VariableDef varDef;
                    if (modInfo.Scope.TryGetVariable(name, out varDef) &&
                        varDef.VariableStillExists) {
                        var types = varDef.Types;
                        if (types.Count > 0) {
                            foreach (var type in types) {
                                if (type is ModuleInfo || type is BuiltinModule) {
                                    // we find modules via our modules list, dont duplicate these
                                    return false;
                                }

                                foreach (var location in type.Locations) {
                                    if (location.FilePath != modInfo.ProjectEntry.FilePath) {
                                        // declared in another module
                                        return false;
                                    }
                                }
                            }
                        }

                        return true;
                    }
                }
                return false;
            }
        }

        private static bool BuiltinModuleContainsMember(IModuleContext context, string name, IPythonModule module) {
            var member = module.GetMember(context, name);
            if (member == null) {
                return false;
            }

            if (IsExcludedBuiltin(module, member)) {
                // if a module imports a builtin and exposes it don't report it for purposes of adding imports
                return false;
            }

            // if something non-excludable aliased w/ something excludable we probably only care about the excludable
            // (for example a module and None - timeit.py does this in the std lib)
            if (member is IPythonMultipleMembers multipleMembers) {
                foreach (var innerMember in multipleMembers.Members) {
                    if (IsExcludedBuiltin(module, innerMember)) {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsExcludedBuiltin(IPythonModule builtin, IMember member) {
            switch (member) {
                case IPythonModule _: // modules are handled specially
                case IPythonFunction func when func.DeclaringModule != builtin: // function imported into another module
                case IPythonFunction type when type.DeclaringModule != builtin: // type imported into another module
                case IPythonConstant objConstant when objConstant.Type.TypeId == BuiltinTypeId.Object: // constant which we have no real type info for.
                case IPythonConstant constant when constant.Type.DeclaringModule.Name == "__future__" &&
                                                   constant.Type.Name == "_Feature" &&
                                                   builtin.Name != "__future__":
                    // someone has done a from __future__ import blah, don't include import in another
                    // module in the list of places where you can import this from.
                    return true;
                default:
                    return false;
            }

        }

        #endregion
    }

    abstract class ModuleLoadState {
        public abstract AnalysisValue Module {
            get;
        }

        public abstract bool HasModule {
            get;
        }

        public abstract bool HasReferences {
            get;
        }

        public abstract bool IsValid {
            get;
        }

        public abstract string Name {
            get;
        }

        public virtual string MaybeSourceFile {
            get { return null; }
        }

        public abstract PythonMemberType MemberType {
            get;
        }

        internal abstract bool ModuleContainsMember(IModuleContext context, string name);
    }
}
