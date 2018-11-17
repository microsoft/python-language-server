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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.DependencyResolution;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Performs analysis of multiple Python code files and enables interrogation of the resulting analysis.
    /// </summary>
    public partial class PythonAnalyzer : IPythonAnalyzer, IDisposable {
        public const string PythonAnalysisSource = "Python";
        private static object _nullKey = new object();

        private readonly PathResolver _pathResolver;
        private readonly HashSet<ModuleInfo> _modulesWithUnresolvedImports = new HashSet<ModuleInfo>();
        private readonly object _modulesWithUnresolvedImportsLock = new object();
        private readonly Dictionary<object, AnalysisValue> _itemCache = new Dictionary<object, AnalysisValue>();
        private readonly SemaphoreSlim _reloadLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<IProjectEntry, Dictionary<Node, Diagnostic>> _diagnostics = new Dictionary<IProjectEntry, Dictionary<Node, Diagnostic>>();
        private readonly Dictionary<string, List<SpecializationInfo>> _specializationInfo = new Dictionary<string, List<SpecializationInfo>>();  // delayed specialization information, for modules not yet loaded...
        private IReadOnlyList<string> _searchPaths = new List<string>();
        private IReadOnlyList<string> _typeStubPaths = new List<string>();

        internal readonly string _builtinName;
        internal BuiltinModule _builtinModule;
        internal ConstantInfo _noneInst;
        internal readonly IModuleContext _defaultContext;
        internal readonly AnalysisUnit _evalUnit;   // a unit used for evaluating when we don't otherwise have a unit available

        private IKnownPythonTypes _knownTypes;
        private Action<int> _reportQueueSize;
        private int _reportQueueInterval;
        private AnalysisLimits _limits = AnalysisLimits.GetDefaultLimits();
        private Dictionary<IProjectEntry[], AggregateProjectEntry> _aggregates = new Dictionary<IProjectEntry[], AggregateProjectEntry>(AggregateComparer.Instance);

        /// <summary>
        /// Creates a new analyzer that is ready for use.
        /// </summary>
        public static async Task<PythonAnalyzer> CreateAsync(IPythonInterpreterFactory factory, CancellationToken token = default) {
            var analyzer = new PythonAnalyzer(factory);
            try {
                await analyzer.ReloadModulesAsync(token).ConfigureAwait(false);
            } catch(Exception) {
                analyzer.Dispose();
                throw;
            }

            return analyzer;
        }
        
        internal PythonAnalyzer(IPythonInterpreterFactory factory) {
            InterpreterFactory = factory;
            LanguageVersion = factory.GetLanguageVersion();
            Interpreter = factory.CreateInterpreter();
            _pathResolver = new PathResolver(LanguageVersion);

            _builtinName = BuiltinTypeId.Unknown.GetModuleName(LanguageVersion);
            Modules = new ModuleTable(this, Interpreter);
            ModulesByFilename = new ConcurrentDictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);

            Limits = AnalysisLimits.GetDefaultLimits();
            Queue = new Deque<AnalysisUnit>();

            _defaultContext = Interpreter.CreateModuleContext();

            _evalUnit = new AnalysisUnit(null, null, new ModuleInfo("$global", new ProjectEntry(this, "$global", String.Empty, null, null), _defaultContext).Scope, true);
            AnalysisLog.NewUnit(_evalUnit);
        }

        private async Task LoadKnownTypesAsync(CancellationToken token) {
            _itemCache.Clear();

            var fallback = new FallbackBuiltinModule(LanguageVersion);

            var moduleRef = await Modules.TryImportAsync(_builtinName, token).ConfigureAwait(false);
            if (moduleRef != null) {
                _builtinModule = (BuiltinModule)moduleRef.Module;
            } else {
                _builtinModule = new BuiltinModule(fallback, this);
                Modules.SetModule(_builtinName, BuiltinModule);
            }
            _builtinModule.InterpreterModule.Imported(_defaultContext);

            var builtinModuleNamesMember = ((IBuiltinPythonModule)_builtinModule.InterpreterModule).GetAnyMember("__builtin_module_names__");
            if (builtinModuleNamesMember is Interpreter.Ast.AstPythonStringLiteral builtinModuleNamesLiteral && builtinModuleNamesLiteral.Value != null) {
                var builtinModuleNames = builtinModuleNamesLiteral.Value.Split(',').Select(n => n.Trim());
                _pathResolver.SetBuiltins(builtinModuleNames);
            }

            Modules.AddBuiltinModuleWrapper("sys", SysModuleInfo.Wrap);
            Modules.AddBuiltinModuleWrapper("typing", TypingModuleInfo.Wrap);

            _knownTypes = KnownTypes.Create(this, fallback);

            _noneInst = (ConstantInfo)GetCached(
                _nullKey,
                () => new ConstantInfo(ClassInfos[BuiltinTypeId.NoneType], null, PythonMemberType.Constant)
            );

            AddBuiltInSpecializations();
        }

        private void ReloadModulePaths() {
            var rootPaths = CurrentPathResolver.GetRootPaths();
            foreach (var rootPath in rootPaths.Where(Directory.Exists)) {
                foreach (var modulePath in ModulePath.GetModulesInPath(rootPath)) {
                    _pathResolver.TryAddModulePath(modulePath.SourceFile, out _);
                }
            }
        }

        /// <summary>
        /// Reloads the modules from the interpreter.
        /// 
        /// This method should be called on the analysis thread and is usually invoked
        /// when the interpreter signals that it's modules have changed.
        /// </summary>
        public async Task ReloadModulesAsync(CancellationToken token = default) {
            if (!_reloadLock.Wait(0)) {
                // If we don't lock immediately, wait for the current reload to
                // complete and then return.
                await _reloadLock.WaitAsync(token).ConfigureAwait(false);
                _reloadLock.Release();
                return;
            }

            try {
                // Cancel outstanding analysis
                Queue.Clear(); 
                // Tell factory to clear cached modules. This also clears the interpreter data.
                InterpreterFactory.NotifyImportNamesChanged();
                // Now initialize the interpreter
                Interpreter.Initialize(this);
                // Reload importable modules
                Modules.Reload();
                // Load known types from the selected interpreter
                await LoadKnownTypesAsync(token);
                // Re-initialize module variables
                foreach (var mod in ModulesByFilename.Values) {
                    mod.Clear();
                    mod.EnsureModuleVariables(this);
                }
            } finally {
                _reloadLock.Release();
            }
        }

        #region Public API

        public PythonLanguageVersion LanguageVersion { get; }

        /// <summary>
        /// Adds a new module of code to the list of available modules and returns a ProjectEntry object.
        /// 
        /// This method is thread safe.
        /// </summary>
        /// <param name="moduleName">The name of the module; used to associate with imports</param>
        /// <param name="filePath">The path to the file on disk</param>
        /// <param name="cookie">An application-specific identifier for the module</param>
        /// <returns>The project entry for the new module.</returns>
        public IPythonProjectEntry AddModule(string moduleName, string filePath, Uri documentUri = null, IAnalysisCookie cookie = null) {
            if (filePath == null || documentUri == null || documentUri.Scheme != "python") {
                if (_pathResolver.TryAddModulePath(filePath, out var fullModuleName)) {
                    moduleName = fullModuleName;
                }
            }

            var entry = new ProjectEntry(this, moduleName, filePath, documentUri, cookie);

            if (moduleName != null) {
                Modules.SetModule(moduleName, entry.MyScope);
                DoDelayedSpecialization(moduleName);
            }
            if (filePath != null) {
                ModulesByFilename[filePath] = entry.MyScope;
            }
            return entry;
        }

        public void RemoveModule(IProjectEntry entry) => RemoveModule(entry, null);

        /// <summary>
        /// Removes the specified project entry from the current analysis.
        /// 
        /// This method is thread safe.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="onImporter">Action to perform on each module that
        /// had imported the one being removed.</param>
        public void RemoveModule(IProjectEntry entry, Action<IPythonProjectEntry> onImporter) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            Contract.EndContractBlock();

            var pyEntry = entry as IPythonProjectEntry;
            IPythonProjectEntry[] importers = null;
            if (!string.IsNullOrEmpty(pyEntry?.ModuleName)) {
                importers = GetEntriesThatImportModule(pyEntry.ModuleName, false).ToArray();
            }

            if (!string.IsNullOrEmpty(entry.FilePath) && ModulesByFilename.TryRemove(entry.FilePath, out var moduleInfo)) {
                lock (_modulesWithUnresolvedImportsLock) {
                    _modulesWithUnresolvedImports.Remove(moduleInfo);
                }
            }

            if (pyEntry?.DocumentUri.Scheme != "python" && !string.IsNullOrEmpty(entry.FilePath)) {
                _pathResolver.RemoveModulePath(entry.FilePath);
            }

            entry.Dispose();
            ClearDiagnostics(entry);

            if (onImporter == null) {
                onImporter = e => e.Analyze(CancellationToken.None, enqueueOnly: true);
            }

            if (!string.IsNullOrEmpty(pyEntry?.ModuleName)) {
                Modules.TryRemove(pyEntry.ModuleName, out _);
                foreach (var e in importers.MaybeEnumerate()) {
                    onImporter(e);
                }
            }
        }

        /// <summary>
        /// Returns a sequence of project entries that import the specified
        /// module. The sequence will be empty if the module is unknown.
        /// </summary>
        /// <param name="moduleName">
        /// The absolute name of the module. This should never end with
        /// '__init__'.
        /// </param>
        public IEnumerable<IPythonProjectEntry> GetEntriesThatImportModule(string moduleName, bool includeUnresolved) {
            var entries = new List<IPythonProjectEntry>();
            if (Modules.TryImport(moduleName, out var modRef) && modRef.HasReferences) {
                entries.AddRange(modRef.References.Select(m => m.ProjectEntry).OfType<IPythonProjectEntry>());
            }

            if (includeUnresolved) {
                // Have to iterate over modules with unresolved imports to find
                // ephemeral references.
                lock (_modulesWithUnresolvedImportsLock) {
                    foreach (var module in _modulesWithUnresolvedImports) {
                        if (module.GetAllUnresolvedModules().Contains(moduleName)) {
                            entries.Add(module.ProjectEntry);
                        }
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Returns a sequence of absolute module names that, if available,
        /// would resolve one or more unresolved references.
        /// </summary>
        internal ISet<string> GetAllUnresolvedModuleNames() {
            var set = new HashSet<string>(StringComparer.Ordinal);
            lock (_modulesWithUnresolvedImportsLock) {
                foreach (var module in _modulesWithUnresolvedImports) {
                    set.UnionWith(module.GetAllUnresolvedModules());
                }
            }
            return set;
        }

        internal void ModuleHasUnresolvedImports(ModuleInfo module, bool hasUnresolvedImports) {
            lock (_modulesWithUnresolvedImportsLock) {
                if (hasUnresolvedImports) {
                    _modulesWithUnresolvedImports.Add(module);
                } else {
                    _modulesWithUnresolvedImports.Remove(module);
                }
            }
        }

        /// <summary>
        /// Gets a top-level list of all the available modules as a list of MemberResults.
        /// </summary>
        /// <returns></returns>
        public IMemberResult[] GetModules() {
            var d = new Dictionary<string, List<ModuleLoadState>>();
            foreach (var keyValue in Modules.GetModuleStates()) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (string.IsNullOrWhiteSpace(modName) || modName.Contains(".")) {
                    continue;
                }

                if (moduleRef.IsValid) {
                    if (!d.TryGetValue(modName, out var l)) {
                        d[modName] = l = new List<ModuleLoadState>();
                    }
                    if (moduleRef.HasModule) {
                        // The REPL shows up here with value=None
                        l.Add(moduleRef);
                    }
                }
            }

            return ModuleDictToMemberResult(d);
        }

        private static IMemberResult[] ModuleDictToMemberResult(Dictionary<string, List<ModuleLoadState>> d) {
            var result = new IMemberResult[d.Count];
            var pos = 0;
            foreach (var kvp in d) {
                var lazyEnumerator = new LazyModuleEnumerator(kvp.Value);
                result[pos++] = new MemberResult(
                    kvp.Key,
                    lazyEnumerator.GetLazyModules,
                    lazyEnumerator.GetModuleType
                );
            }
            return result;
        }

        class LazyModuleEnumerator {
            private readonly List<ModuleLoadState> _loaded;

            public LazyModuleEnumerator(List<ModuleLoadState> loaded) {
                _loaded = loaded;
            }

            public IEnumerable<AnalysisValue> GetLazyModules() {
                foreach (var value in _loaded) {
                    yield return new SyntheticDefinitionInfo(
                        value.Name,
                        null,
                        string.IsNullOrEmpty(value.MaybeSourceFile) ?
                            Enumerable.Empty<LocationInfo>() :
                            new[] { new LocationInfo(value.MaybeSourceFile, null, 0, 0) }
                    );
                }
            }

            public PythonMemberType GetModuleType() {
                PythonMemberType? type = null;
                foreach (var value in _loaded) {
                    if (type == null) {
                        type = value.MemberType;
                    } else if (type != value.MemberType) {
                        type = PythonMemberType.Multiple;
                        break;
                    }
                }
                return type ?? PythonMemberType.Unknown;
            }
        }

        /// <summary>
        /// Searches all modules which match the given name and searches in the modules
        /// for top-level items which match the given name.  Returns a list of all the
        /// available names fully qualified to their name.  
        /// </summary>
        /// <param name="name"></param>
        public IEnumerable<ExportedMemberInfo> FindNameInAllModules(string name) {
            string pkgName;

            if (Interpreter is ICanFindModuleMembers finder) {
                foreach (var modName in finder.GetModulesNamed(name)) {
                    var dot = modName.LastIndexOf('.');
                    if (dot < 0) {
                        yield return new ExportedMemberInfo(null, modName);
                    } else {
                        yield return new ExportedMemberInfo(modName.Remove(dot), modName.Substring(dot + 1));
                    }
                }

                foreach (var modName in finder.GetModulesContainingName(name)) {
                    yield return new ExportedMemberInfo(modName, name);
                }

                // Scan added modules directly
                foreach (var mod in ModulesByFilename.Values) {
                    if (mod.Name == name) {
                        yield return new ExportedMemberInfo(null, mod.Name);
                    } else if (GetPackageNameIfMatch(name, mod.Name, out pkgName)) {
                        yield return new ExportedMemberInfo(pkgName, name);
                    }

                    if (mod.IsMemberDefined(_defaultContext, name)) {
                        yield return new ExportedMemberInfo(mod.Name, name);
                    }
                }

                yield break;
            }

            // provide module names first
            foreach (var keyValue in Modules.GetModuleStates()) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (moduleRef.IsValid) {
                    // include modules which can be imported
                    if (modName == name) {
                        yield return new ExportedMemberInfo(null, modName);
                    } else if (GetPackageNameIfMatch(name, modName, out pkgName)) {
                        yield return new ExportedMemberInfo(pkgName, name);
                    }
                }
            }

            foreach (var modName in Interpreter.GetModuleNames()) {
                if (modName == name) {
                    yield return new ExportedMemberInfo(null, modName);
                } else if (GetPackageNameIfMatch(name, modName, out pkgName)) {
                    yield return new ExportedMemberInfo(pkgName, name);
                }
            }

            // then include imported module members
            foreach (var keyValue in Modules.GetModuleStates()) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (moduleRef.IsValid && moduleRef.ModuleContainsMember(_defaultContext, name)) {
                    yield return new ExportedMemberInfo(modName, name);
                }
            }
        }

        private static bool GetPackageNameIfMatch(string name, string fullName, out string packageName) {
            var lastDot = fullName.LastIndexOf('.');
            if (lastDot < 0) {
                packageName = null;
                return false;
            }

            packageName = fullName.Remove(lastDot);
            return String.Compare(fullName, lastDot + 1, name, 0, name.Length, StringComparison.Ordinal) == 0;
        }

        /// <summary>
        /// Returns the interpreter that the analyzer is using.
        /// 
        /// This property is thread safe.
        /// </summary>
        public IPythonInterpreter Interpreter { get; }

        /// <summary>
        /// Returns the interpreter factory that the analyzer is using.
        /// </summary>
        public IPythonInterpreterFactory InterpreterFactory { get; }

        /// <summary>
        /// returns the MemberResults associated with modules in the specified
        /// list of names.  The list of names is the path through the module, for example
        /// ['System', 'Runtime']
        /// </summary>
        /// <returns></returns>
        public IMemberResult[] GetModuleMembers(IModuleContext moduleContext, string[] names, bool includeMembers = false) {
            if (Modules.TryImport(names[0], out var moduleRef)) {
                if (moduleRef.Module is IModule module) {
                    return GetModuleMembers(moduleContext, names, includeMembers, module);
                }

            }

            return new IMemberResult[0];
        }

        internal static IMemberResult[] GetModuleMembers(IModuleContext moduleContext, string[] names, bool includeMembers, IModule module) {
            for (var i = 1; i < names.Length && module != null; i++) {
                module = module.GetChildPackage(moduleContext, names[i]);
            }

            if (module == null) {
                return new IMemberResult[0];
            }

            var result = new Dictionary<string, List<IAnalysisSet>>();
            if (includeMembers) {
                foreach (var keyValue in module.GetAllMembers(moduleContext)) {
                    if (!result.TryGetValue(keyValue.Key, out var results)) {
                        result[keyValue.Key] = results = new List<IAnalysisSet>();
                    }
                    results.Add(keyValue.Value);
                }
                return MemberDictToMemberResult(result);
            }

            foreach (var child in module.GetChildrenPackages(moduleContext)) {
                if (!result.TryGetValue(child.Key, out var results)) {
                    result[child.Key] = results = new List<IAnalysisSet>();
                }
                results.Add(child.Value);
            }
            foreach (var keyValue in module.GetAllMembers(moduleContext)) {
                var anyModules = false;
                foreach (var ns in keyValue.Value.OfType<MultipleMemberInfo>()) {
                    if (ns.Members.OfType<IModule>().Any(mod => !(mod is MultipleMemberInfo))) {
                        anyModules = true;
                        break;
                    }
                }
                if (anyModules) {
                    if (!result.TryGetValue(keyValue.Key, out var results)) {
                        result[keyValue.Key] = results = new List<IAnalysisSet>();
                    }
                    results.Add(keyValue.Value);
                }
            }
            return MemberDictToMemberResult(result);
        }

        private static IMemberResult[] MemberDictToMemberResult(Dictionary<string, List<IAnalysisSet>> results) 
            => results.Select(r => new MemberResult(r.Key, r.Value.SelectMany()) as IMemberResult).ToArray();

        /// <summary>
        /// Gets the list of directories which should be analyzed.
        /// This property is thread safe.
        /// </summary>
        public IEnumerable<string> AnalysisDirectories => _searchPaths;

        /// <summary>
        /// Gets the list of directories which should be searched for type stubs.
        /// This property is thread safe.
        /// </summary>
        public IEnumerable<string> TypeStubDirectories => _typeStubPaths;

        public AnalysisLimits Limits {
            get => _limits;
            set {
                value = value ?? AnalysisLimits.GetDefaultLimits();
                var limits = _limits;
                _limits = value;

                if (limits.UseTypeStubPackages ^ _limits.UseTypeStubPackages
                    || limits.UseTypeStubPackagesExclusively ^ _limits.UseTypeStubPackagesExclusively) {
                    SearchPathsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool EnableDiagnostics { get; set; }

        public void AddDiagnostic(Node node, AnalysisUnit unit, string message, DiagnosticSeverity severity, string code = null) {
            if (!EnableDiagnostics) {
                return;
            }

            lock (_diagnostics) {
                if (!_diagnostics.TryGetValue(unit.ProjectEntry, out var diags)) {
                    _diagnostics[unit.ProjectEntry] = diags = new Dictionary<Node, Diagnostic>();
                }
                diags[node] = new Diagnostic {
                    message = message,
                    range = node.GetSpan(unit.ProjectEntry.Tree),
                    severity = severity,
                    code = code,
                    source = PythonAnalysisSource
                };
            }
        }

        public IReadOnlyList<Diagnostic> GetDiagnostics(IProjectEntry entry) {
            lock (_diagnostics) {
                if (_diagnostics.TryGetValue(entry, out var diags)) {
                    return diags.OrderBy(kv => kv.Key.StartIndex).Select(kv => kv.Value).ToArray();
                }
            }
            return Array.Empty<Diagnostic>();
        }

        public IReadOnlyDictionary<IProjectEntry, IReadOnlyList<Diagnostic>> GetAllDiagnostics() {
            var res = new Dictionary<IProjectEntry, IReadOnlyList<Diagnostic>>();
            lock (_diagnostics) {
                foreach (var kv in _diagnostics) {
                    res[kv.Key] = kv.Value.OrderBy(d => d.Key.StartIndex).Select(d => d.Value).ToArray();
                }
            }
            return res;
        }

        public void ClearDiagnostic(Node node, AnalysisUnit unit, string code = null) {
            if (!EnableDiagnostics) {
                return;
            }

            lock (_diagnostics) {
                if (_diagnostics.TryGetValue(unit.ProjectEntry, out var diags) && diags.TryGetValue(node, out var d)) {
                    if (code == null || d.code == code) {
                        diags.Remove(node);
                    }
                }
            }
        }

        public void ClearDiagnostics(IProjectEntry entry) {
            lock (_diagnostics) {
                _diagnostics.Remove(entry);
            }
        }
        #endregion

        #region Internal Implementation

        internal IKnownPythonTypes Types {
            get {
                if (_knownTypes != null) {
                    return _knownTypes;
                }
                throw new InvalidOperationException("Analyzer has not been initialized. Call ReloadModulesAsync() first.");
            }
        }

        internal IKnownClasses ClassInfos {
            get {
                if (_knownTypes != null) {
                    return (IKnownClasses)_knownTypes;
                }
                throw new InvalidOperationException("Analyzer has not been initialized. Call ReloadModulesAsync() first.");
            }
        }

        internal Deque<AnalysisUnit> Queue { get; }

        /// <summary>
        /// Returns the cached value for the provided key, creating it with
        /// <paramref name="maker"/> if necessary. If <paramref name="maker"/>
        /// attempts to get the same value, returns <c>null</c>.
        /// </summary>
        /// <param name="key">The identifier for the cached value.</param>
        /// <param name="maker">Function to create the value.</param>
        /// <returns>The cached value or <c>null</c>.</returns>
        internal AnalysisValue GetCached(object key, Func<AnalysisValue> maker) {
            if (!_itemCache.TryGetValue(key, out var result)) {
                // Set the key to prevent recursion
                _itemCache[key] = null;
                _itemCache[key] = result = maker();
            }
            return result;
        }

        internal BuiltinModule BuiltinModule => _builtinModule;

        internal PathResolverSnapshot CurrentPathResolver => _pathResolver.CurrentSnapshot;

        internal BuiltinInstanceInfo GetInstance(IPythonType type) => GetBuiltinType(type).Instance;

        internal BuiltinClassInfo GetBuiltinType(IPythonType type) => 
            (BuiltinClassInfo)GetCached(type,
                () => MakeBuiltinType(type)
            ) ?? ClassInfos[BuiltinTypeId.Object];

        private BuiltinClassInfo MakeBuiltinType(IPythonType type) {
            switch (type.TypeId) {
                case BuiltinTypeId.List: return new ListBuiltinClassInfo(type, this);
                case BuiltinTypeId.Tuple: return new TupleBuiltinClassInfo(type, this);
                case BuiltinTypeId.Object: return new ObjectBuiltinClassInfo(type, this);
                case BuiltinTypeId.Dict: return new DictBuiltinClassInfo(type, this);
                default: return new BuiltinClassInfo(type, this);
            }
        }

        internal IAnalysisSet GetAnalysisSetFromObjects(object objects) {
            if (!(objects is IEnumerable<object> typeList)) {
                return AnalysisSet.Empty;
            }
            return AnalysisSet.UnionAll(typeList.Select(GetAnalysisValueFromObjects));
        }

        internal IAnalysisSet GetAnalysisSetFromObjects(IEnumerable<IPythonType> typeList) {
            if (typeList == null) {
                return AnalysisSet.Empty;
            }
            return AnalysisSet.UnionAll(typeList.Select(GetAnalysisValueFromObjects));
        }

        internal AnalysisValue GetAnalysisValueFromObjectsThrowOnNull(object attr) {
            if (attr == null) {
                throw new ArgumentNullException(nameof(attr));
            }
            return GetAnalysisValueFromObjects(attr);
        }

        public AnalysisValue GetAnalysisValueFromObjects(object attr) {
            if (attr == null) {
                return _noneInst;
            }

            var attrType = attr.GetType();
            if (attr is IPythonType pt) {
                return GetBuiltinType(pt);
            } else if (attr is IPythonFunction pf) {
                return GetCached(attr, () => new BuiltinFunctionInfo(pf, this)) ?? _noneInst;
            } else if (attr is IPythonMethodDescriptor md) {
                return GetCached(attr, () => {
                    if (md.IsBound) {
                        return new BuiltinFunctionInfo(md.Function, this);
                    } else {
                        return new BuiltinMethodInfo(md, this);
                    }
                }) ?? _noneInst;
            } else if (attr is IPythonBoundFunction pbf) {
                return GetCached(attr, () => new BoundBuiltinMethodInfo(pbf, this)) ?? _noneInst;
            } else if (attr is IBuiltinProperty bp) {
                return GetCached(attr, () => new BuiltinPropertyInfo(bp, this)) ?? _noneInst;
            } else if (attr is IPythonModule pm) {
                return Modules.GetBuiltinModule(pm);
            } else if (attr is IPythonEvent pe) {
                return GetCached(attr, () => new BuiltinEventInfo(pe, this)) ?? _noneInst;
            } else if (attr is IPythonConstant ||
                       attrType == typeof(bool) || attrType == typeof(int) || attrType == typeof(Complex) ||
                       attrType == typeof(string) || attrType == typeof(long) || attrType == typeof(double)) {
                return GetConstant(attr).First();
            } else if (attr is IMemberContainer mc) {
                return GetCached(attr, () => new ReflectedNamespace(mc, this));
            } else if (attr is IPythonMultipleMembers mm) {
                var members = mm.Members;
                return GetCached(attr, () =>
                    MultipleMemberInfo.Create(members.Select(GetAnalysisValueFromObjects)).FirstOrDefault() ??
                        ClassInfos[BuiltinTypeId.NoneType].Instance
                );
            } else {
                var pyAttrType = GetTypeFromObject(attr);
                Debug.Assert(pyAttrType != null);
                return GetBuiltinType(pyAttrType).Instance;
            }
        }

        internal IDictionary<string, IAnalysisSet> GetAllMembers(IMemberContainer container, IModuleContext moduleContext) {
            var names = container.GetMemberNames(moduleContext);
            var result = new Dictionary<string, IAnalysisSet>();
            foreach (var name in names) {
                result[name] = GetAnalysisValueFromObjects(container.GetMember(moduleContext, name));
            }

            return result;
        }

        internal ModuleTable Modules { get; }

        internal ConcurrentDictionary<string, ModuleInfo> ModulesByFilename { get; }

        public bool TryGetProjectEntryByPath(string path, out IProjectEntry projEntry) {
            if (ModulesByFilename.TryGetValue(path, out var modInfo)) {
                projEntry = modInfo.ProjectEntry;
                return true;
            }

            projEntry = null;
            return false;
        }

        internal IAnalysisSet GetConstant(object value) {
            var key = value ?? _nullKey;
            return GetCached(key, () => {
                var constant = value as IPythonConstant;
                var constantType = constant?.Type;
                var av = GetAnalysisValueFromObjectsThrowOnNull(constantType ?? GetTypeFromObject(value));

                if (av is ConstantInfo ci) {
                    return ci;
                }

                if (av is BuiltinClassInfo bci) {
                    if (constant == null) {
                        return new ConstantInfo(bci, value, PythonMemberType.Constant);
                    }
                    return bci.Instance;
                }
                return _noneInst;
            }) ?? _noneInst;
        }

        private static void Update<K, V>(IDictionary<K, V> dict, IDictionary<K, V> newValues) {
            foreach (var kvp in newValues) {
                dict[kvp.Key] = kvp.Value;
            }
        }

        internal IPythonType GetTypeFromObject(object value) {
            if (value == null) {
                return Types[BuiltinTypeId.NoneType];
            }

            var astConst = value as IPythonConstant;
            if (astConst != null) {
                return Types[astConst.Type?.TypeId ?? BuiltinTypeId.Object] ?? Types[BuiltinTypeId.Object];
            }

            switch (Type.GetTypeCode(value.GetType())) {
                case TypeCode.Boolean: return Types[BuiltinTypeId.Bool];
                case TypeCode.Double: return Types[BuiltinTypeId.Float];
                case TypeCode.Int32: return Types[BuiltinTypeId.Int];
                case TypeCode.String: return Types[BuiltinTypeId.Unicode];
                case TypeCode.Object:
                    if (value.GetType() == typeof(Complex)) {
                        return Types[BuiltinTypeId.Complex];
                    } else if (value.GetType() == typeof(AsciiString)) {
                        return Types[BuiltinTypeId.Bytes];
                    } else if (value.GetType() == typeof(BigInteger)) {
                        return Types[BuiltinTypeId.Long];
                    } else if (value.GetType() == typeof(Ellipsis)) {
                        return Types[BuiltinTypeId.Ellipsis];
                    }
                    break;
            }

            Debug.Fail("unsupported constant type <{0}> value '{1}'".FormatInvariant(value.GetType().FullName, value));
            return Types[BuiltinTypeId.Object];
        }

        internal BuiltinClassInfo MakeGenericType(IAdvancedPythonType clrType, params IPythonType[] clrIndexType) {
            var res = clrType.MakeGenericType(clrIndexType);

            return (BuiltinClassInfo)GetAnalysisValueFromObjects(res);
        }

        #endregion

        #region IGroupableAnalysisProject Members

        public void AnalyzeQueuedEntries(CancellationToken cancel) {
            if (cancel.IsCancellationRequested) {
                return;
            }

            if (_builtinModule == null) {
                Debug.Fail("Used analyzer without reloading modules");
                ReloadModulesAsync(cancel).WaitAndUnwrapExceptions();
            }

            var ddg = new DDG();
            ddg.Analyze(Queue, cancel, _reportQueueSize, _reportQueueInterval);
            foreach (ProjectEntry entry in ddg.AnalyzedEntries) {
                entry.SetCompleteAnalysis();
            }
        }

        #endregion

        /// <summary>
        /// Specifies a callback to invoke to provide feedback on the number of
        /// items being processed.
        /// </summary>
        public void SetQueueReporting(Action<int> reportFunction, int interval = 1) {
            _reportQueueSize = reportFunction;
            _reportQueueInterval = interval;
        }

        public IReadOnlyList<string> GetSearchPaths() => _searchPaths.ToArray();

        /// <summary>
        /// Sets the search paths for this analyzer, invoking callbacks for any
        /// path added or removed.
        /// </summary>
        public void SetSearchPaths(IEnumerable<string> paths) {
            Interlocked.Exchange(ref _searchPaths, new List<string>(paths).AsReadOnly());
            _pathResolver.SetUserSearchPaths(_searchPaths);
            ReloadModulePaths();
            SearchPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void SetRoot(string rootDir) => _pathResolver.SetRoot(rootDir);

        internal void SetInterpreterPaths(IEnumerable<string> paths) 
            => _pathResolver.SetInterpreterSearchPaths(paths);

        public IReadOnlyList<string> GetTypeStubPaths() => _typeStubPaths;

        /// <summary>
        /// Sets the type stub search paths for this analyzer, invoking callbacks for any
        /// path added or removed.
        /// </summary>
        public void SetTypeStubPaths(IEnumerable<string> paths) {
            Interlocked.Exchange(ref _typeStubPaths, new List<string>(paths).AsReadOnly());
            SearchPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event fired when the analysis directories have changed.  
        /// 
        /// This event can be fired on any thread.
        /// 
        /// New in 1.1.
        /// </summary>
        public event EventHandler SearchPathsChanged;

        #region IDisposable Members

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                Queue.Clear();
                var interpreter = Interpreter as IDisposable;
                interpreter?.Dispose();
                // Try and acquire the lock before disposing. This helps avoid
                // some (non-fatal) exceptions.
                try {
                    _reloadLock.Wait(TimeSpan.FromSeconds(10));
                    _reloadLock.Dispose();
                } catch (ObjectDisposedException) {
                }
            }
        }

        ~PythonAnalyzer() {
            Dispose(false);
        }
        #endregion

        internal AggregateProjectEntry GetAggregate(params IProjectEntry[] aggregating) {
            Debug.Assert(new HashSet<IProjectEntry>(aggregating).Count == aggregating.Length);

            SortAggregates(aggregating);

            return GetAggregateWorker(aggregating);
        }

        private static void SortAggregates(IProjectEntry[] aggregating) 
            => Array.Sort(aggregating, (x, y) => x.GetHashCode() - y.GetHashCode());

        internal AggregateProjectEntry GetAggregate(HashSet<IProjectEntry> from, IProjectEntry with) {
            Debug.Assert(!from.Contains(with));

            var all = new IProjectEntry[from.Count + 1];
            from.CopyTo(all);
            all[from.Count] = with;

            SortAggregates(all);

            return GetAggregateWorker(all);
        }

        internal void ClearAggregate(AggregateProjectEntry entry) {
            var aggregating = entry._aggregating.ToArray();
            SortAggregates(aggregating);

            _aggregates.Remove(aggregating);
        }

        private AggregateProjectEntry GetAggregateWorker(IProjectEntry[] all) {
            if (!_aggregates.TryGetValue(all, out var agg)) {
                _aggregates[all] = agg = new AggregateProjectEntry(new HashSet<IProjectEntry>(all));

                foreach (var proj in all) {
                    if (proj is IAggregateableProjectEntry aggretable) {
                        aggretable.AggregatedInto(agg);
                    }
                }
            }

            return agg;
        }

        class AggregateComparer : IEqualityComparer<IProjectEntry[]> {
            public static AggregateComparer Instance = new AggregateComparer();

            public bool Equals(IProjectEntry[] x, IProjectEntry[] y) {
                if (x.Length != y.Length) {
                    return false;
                }
                for (var i = 0; i < x.Length; i++) {
                    if (x[i] != y[i]) {
                        return false;
                    }
                }
                return true;
            }

            public int GetHashCode(IProjectEntry[] obj) {
                var res = 0;
                for (var i = 0; i < obj.Length; i++) {
                    res ^= obj[i].GetHashCode();
                }
                return res;
            }
        }

    }
}
