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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Specializations;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Modules {
    internal sealed class ModuleResolution : IModuleResolution {
        private static readonly IReadOnlyDictionary<string, string> _emptyModuleSet = EmptyDictionary<string, string>.Instance;
        private readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        private readonly IReadOnlyList<string> _typeStubPaths;
        private readonly IServiceContainer _services;
        private readonly IPythonInterpreter _interpreter;
        private readonly IFileSystem _fs;
        private readonly ILogger _log;
        private readonly bool _requireInitPy;
        private readonly string _root;

        private PathResolver _pathResolver;
        private IReadOnlyDictionary<string, string> _searchPathPackages;
        private IReadOnlyList<string> _searchPaths;

        private InterpreterConfiguration Configuration => _interpreter.Configuration;

        public ModuleResolution(string root, IServiceContainer services) {
            _root = root;
            _services = services;
            _interpreter = services.GetService<IPythonInterpreter>();
            _fs = services.GetService<IFileSystem>();
            _log = services.GetService<ILogger>();

            _requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_interpreter.Configuration.Version);
            // TODO: merge with user-provided stub paths
            _typeStubPaths = GetTypeShedPaths(_interpreter.Configuration?.TypeshedPath).ToArray();
        }

        internal async Task LoadBuiltinTypesAsync(CancellationToken cancellationToken = default) {
            // Add names from search paths
            await ReloadAsync(cancellationToken);

            // Initialize built-in
            var moduleName = BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);
            var modulePath = ModuleCache.GetCacheFilePath(_interpreter.Configuration.InterpreterPath ?? "python.exe");

            var b = new BuiltinsPythonModule(moduleName, modulePath, _services);
            _modules[BuiltinModuleName] = BuiltinsModule = b;
            await b.LoadAndAnalyzeAsync(cancellationToken);

            // Add built-in module names
            var builtinModuleNamesMember = BuiltinsModule.GetAnyMember("__builtin_module_names__");
            if (builtinModuleNamesMember.TryGetConstant<string>(out var s)) {
                var builtinModuleNames = s.Split(',').Select(n => n.Trim());
                _pathResolver.SetBuiltins(builtinModuleNames);
            }
        }

        public IModuleCache ModuleCache { get; private set; }
        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        public PathResolverSnapshot CurrentPathResolver => _pathResolver.CurrentSnapshot;

        /// <summary>
        /// Builtins module.
        /// </summary>
        public IBuiltinsPythonModule BuiltinsModule { get; private set; }

        public async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(CancellationToken cancellationToken) {
            if (_searchPathPackages != null) {
                return _searchPathPackages;
            }

            var packageDict = await GetImportableModulesAsync(Configuration.SearchPaths, cancellationToken).ConfigureAwait(false);
            if (!packageDict.Any()) {
                return _emptyModuleSet;
            }

            _searchPathPackages = packageDict;
            return packageDict;
        }

        public async Task<IReadOnlyList<string>> GetSearchPathsAsync(CancellationToken cancellationToken = default) {
            if (_searchPaths != null) {
                return _searchPaths;
            }

            _searchPaths = await GetInterpreterSearchPathsAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(_searchPaths != null, "Should have search paths");
            _searchPaths = _searchPaths.Concat(Configuration.SearchPaths ?? Array.Empty<string>()).ToArray();
            _log?.Log(TraceEventType.Information, "SearchPaths", _searchPaths.Cast<object>().ToArray());
            return _searchPaths;
        }

        public async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(IEnumerable<string> searchPaths, CancellationToken cancellationToken = default) {
            var packageDict = new Dictionary<string, string>();

            foreach (var searchPath in searchPaths.MaybeEnumerate()) {
                IReadOnlyCollection<string> packages = null;
                if (_fs.FileExists(searchPath)) {
                    packages = GetPackagesFromZipFile(searchPath, cancellationToken);
                } else if (_fs.DirectoryExists(searchPath)) {
                    packages = await Task.Run(()
                        => GetPackagesFromDirectory(searchPath, cancellationToken), cancellationToken).ConfigureAwait(false);
                }
                foreach (var package in packages.MaybeEnumerate()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    packageDict[package] = searchPath;
                }
            }

            return packageDict;
        }

        private async Task<IReadOnlyList<string>> GetInterpreterSearchPathsAsync(CancellationToken cancellationToken = default) {
            if (!_fs.FileExists(Configuration.InterpreterPath)) {
                return Array.Empty<string>();
            }

            _log?.Log(TraceEventType.Information, "GetCurrentSearchPaths", Configuration.InterpreterPath, ModuleCache.SearchPathCachePath);
            try {
                var paths = await PythonLibraryPath.GetDatabaseSearchPathsAsync(Configuration, ModuleCache.SearchPathCachePath).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return paths.MaybeEnumerate().Select(p => p.Path).ToArray();
            } catch (InvalidOperationException) {
                return Array.Empty<string>();
            }
        }

        private async Task<TryImportModuleResult> TryImportModuleAsync(string name, CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(name)) {
                return TryImportModuleResult.ModuleNotFound;
            }
            if (name == BuiltinModuleName) {
                return new TryImportModuleResult(BuiltinsModule);
            }

            Debug.Assert(!name.EndsWithOrdinal("."), $"{name} should not end with '.'");
            // Return any existing module
            if (_modules.TryGetValue(name, out var module) && module != null) {
                if (module is SentinelModule) {
                    // TODO: we can't just wait here or we hang. There are two cases:
                    //   a. Recursion on the same analysis chain (A -> B -> A)
                    //   b. Call from another chain (A -> B -> C and D -> B -> E).
                    // TODO: Both should be resolved at the dependency chain level.
                    _log?.Log(TraceEventType.Warning, $"Recursive import: {name}");
                }
                return new TryImportModuleResult(module);
            }

            // Set up a sentinel so we can detect recursive imports
            var sentinelValue = new SentinelModule(name);
            if (!_modules.TryAdd(name, sentinelValue)) {
                // Try to get the new module, in case we raced with a .Clear()
                if (_modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                    return new TryImportModuleResult(module);
                }
                // If we reach here, the race is too complicated to recover
                // from. Signal the caller to try importing again.
                _log?.Log(TraceEventType.Warning, $"Retry import: {name}");
                return TryImportModuleResult.NeedRetry;
            }

            // Do normal searches
            try {
                module = await ImportFromSearchPathsAsync(name, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                _log?.Log(TraceEventType.Error, $"Import timeout {name}");
                return TryImportModuleResult.Timeout;
            }

            module = module ?? await ModuleCache.ImportFromCacheAsync(name, cancellationToken);

            // Replace our sentinel
            if (!_modules.TryUpdate(name, module, sentinelValue)) {
                // Try to get the new module, in case we raced
                if (_modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                    return new TryImportModuleResult(module);
                }
                // If we reach here, the race is too complicated to recover
                // from. Signal the caller to try importing again.
                _log?.Log(TraceEventType.Warning, $"Retry import: {name}");
                return TryImportModuleResult.NeedRetry;
            }

            return new TryImportModuleResult(module);
        }

        public IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, CancellationToken cancellationToken) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true,
                requireInitPy: _requireInitPy
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).TakeWhile(_ => !cancellationToken.IsCancellationRequested).ToList();
        }

        public async Task<IPythonModule> ImportModuleAsync(string name, CancellationToken cancellationToken = default) {
            if (name == BuiltinModuleName) {
                return BuiltinsModule;
            }

            for (var retries = 5; retries > 0; --retries) {
                cancellationToken.ThrowIfCancellationRequested();

                // The call should be cancelled by the cancellation token, but since we
                // are blocking here we wait for slightly longer. Timeouts are handled
                // gracefully by TryImportModuleAsync(), so we want those to trigger if
                // possible, but if all else fails then we'll abort and treat it as an
                // error.
                // (And if we've got a debugger attached, don't time out at all.)
                TryImportModuleResult result;
                try {
                    result = await TryImportModuleAsync(name, cancellationToken);
                } catch (OperationCanceledException) {
                    _log?.Log(TraceEventType.Error, $"Import timeout: {name}");
                    Debug.Fail("Import timeout");
                    return null;
                }

                switch (result.Status) {
                    case TryImportModuleResultCode.Success:
                        return result.Module;
                    case TryImportModuleResultCode.ModuleNotFound:
                        _log?.Log(TraceEventType.Information, $"Import not found: {name}");
                        return null;
                    case TryImportModuleResultCode.NeedRetry:
                    case TryImportModuleResultCode.Timeout:
                        break;
                    case TryImportModuleResultCode.NotSupported:
                        _log?.Log(TraceEventType.Error, $"Import not supported: {name}");
                        return null;
                }
            }
            // Never succeeded, so just log the error and fail
            _log?.Log(TraceEventType.Error, $"Retry import failed: {name}");
            return null;
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default) {
            ModuleCache = new ModuleCache(_interpreter, _services);

            _pathResolver = new PathResolver(_interpreter.LanguageVersion);

            var addedRoots = _pathResolver.SetRoot(_root);
            ReloadModulePaths(addedRoots);

            var interpreterPaths = await GetSearchPathsAsync(cancellationToken);
            addedRoots = _pathResolver.SetInterpreterSearchPaths(interpreterPaths);
            ReloadModulePaths(addedRoots);

            addedRoots = _pathResolver.SetUserSearchPaths(_interpreter.Configuration.SearchPaths);
            ReloadModulePaths(addedRoots);
        }

        public void AddModulePath(string path) => _pathResolver.TryAddModulePath(path, out var _);

        /// <summary>
        /// Determines whether the specified directory is an importable package.
        /// </summary>
        public bool IsPackage(string directory)
            => ModulePath.PythonVersionRequiresInitPyFiles(Configuration.Version) ?
                !string.IsNullOrEmpty(ModulePath.GetPackageInitPy(directory)) :
                _fs.DirectoryExists(directory);

        public ModulePath FindModule(string filePath) {
            var bestLibraryPath = string.Empty;

            foreach (var p in Configuration.SearchPaths) {
                if (PathEqualityComparer.Instance.StartsWith(filePath, p)) {
                    if (p.Length > bestLibraryPath.Length) {
                        bestLibraryPath = p;
                    }
                }
            }

            var mp = ModulePath.FromFullPath(filePath, bestLibraryPath);
            return mp;
        }

        /// <summary>
        /// Provides ability to specialize module by replacing module import by
        /// <see cref="IPythonModule"/> implementation in code. Real module
        /// content is loaded and analyzed only for class/functions definitions
        /// so the original documentation can be extracted.
        /// </summary>
        /// <param name="name">Module to specialize.</param>
        /// <param name="specializationConstructor">Specialized module constructor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Original (library) module loaded as stub.</returns>
        public async Task<IPythonModule> SpecializeModuleAsync(string name, Func<string, IPythonModule> specializationConstructor, CancellationToken cancellationToken = default) {
            var import = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (!string.IsNullOrEmpty(import?.ModulePath)) {
                var module = specializationConstructor(import.ModulePath);
                _modules[name] = module;
                await module.LoadAndAnalyzeAsync(cancellationToken);
                return module;
            }
            return null;
        }

        /// <summary>
        /// Returns specialized module, if any.
        /// </summary>
        public IPythonModule GetSpecializedModule(string name)
            => _modules.TryGetValue(name, out var m) && m is SpecializedModule ? m : null;

        private async Task<IPythonModule> ImportFromSearchPathsAsync(string name, CancellationToken cancellationToken) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                _log?.Log(TraceEventType.Verbose, "Import not found: ", name);
                return null;
            }
            // If there is a stub, make sure it is loaded and attached
            var stub = await ImportFromTypeStubsAsync(moduleImport.IsBuiltin ? name : moduleImport.FullName, cancellationToken);
            IPythonModule module;

            if (moduleImport.IsBuiltin) {
                _log?.Log(TraceEventType.Verbose, "Import built-in compiled (scraped) module: ", name, Configuration.InterpreterPath);
                module = new CompiledBuiltinPythonModule(name, stub, _services);
            } else if (moduleImport.IsCompiled) {
                _log?.Log(TraceEventType.Verbose, "Import compiled (scraped): ", moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath);
                module = new CompiledPythonModule(moduleImport.FullName, ModuleType.Compiled, moduleImport.ModulePath, stub, _services);
            } else {
                _log?.Log(TraceEventType.Verbose, "Import: ", moduleImport.FullName, moduleImport.ModulePath);
                var rdt = _services.GetService<IRunningDocumentTable>();
                // TODO: handle user code and library module separately.
                var mco = new ModuleCreationOptions {
                    ModuleName = moduleImport.FullName,
                    ModuleType = ModuleType.Library,
                    FilePath = moduleImport.ModulePath,
                    Stub = stub,
                    LoadOptions = ModuleLoadOptions.Analyze
                };
                module = rdt.AddModule(mco);
            }

            await module.LoadAndAnalyzeAsync(cancellationToken).ConfigureAwait(false);
            return module;
        }

        private async Task<IPythonModule> ImportFromTypeStubsAsync(string name, CancellationToken cancellationToken = default) {
            var mp = FindModuleInSearchPath(_typeStubPaths, null, name);
            if (mp != null) {
                if (mp.Value.IsCompiled) {
                    _log?.Log(TraceEventType.Warning, "Unsupported native module in stubs", mp.Value.FullName, mp.Value.SourceFile);
                    return null;
                }
                return await CreateStubModuleAsync(mp.Value.FullName, mp.Value.SourceFile, cancellationToken);
            }

            var i = name.IndexOf('.');
            if (i == 0) {
                Debug.Fail("Invalid module name");
                return null;
            }

            var stubPath = CurrentPathResolver.GetPossibleModuleStubPaths(name).FirstOrDefault(p => _fs.FileExists(p));
            return stubPath != null ? await CreateStubModuleAsync(name, stubPath, cancellationToken) : null;
        }

        private async Task<IPythonModule> CreateStubModuleAsync(string moduleName, string filePath, CancellationToken cancellationToken = default) {
            _log?.Log(TraceEventType.Verbose, "Import type stub", moduleName, filePath);
            var module = new StubPythonModule(moduleName, filePath, _services);
            await module.LoadAndAnalyzeAsync(cancellationToken);
            return module;
        }

        private ModulePath? FindModuleInSearchPath(IReadOnlyList<string> searchPaths, IReadOnlyDictionary<string, string> packages, string name) {
            if (searchPaths == null || searchPaths.Count == 0) {
                return null;
            }

            _log?.Log(TraceEventType.Verbose, "FindModule", name, "system", string.Join(", ", searchPaths));

            var i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);

            ModulePath mp;
            Func<string, bool> isPackage = IsPackage;
            if (firstBit.EndsWithOrdinal("-stubs", ignoreCase: true)) {
                isPackage = _fs.DirectoryExists;
            }

            var requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(Configuration.Version);
            if (packages != null && packages.TryGetValue(firstBit, out var searchPath) && !string.IsNullOrEmpty(searchPath)) {
                if (ModulePath.FromBasePathAndName_NoThrow(searchPath, name, isPackage, null, requireInitPy, out mp, out _, out _, out _)) {
                    return mp;
                }
            }

            foreach (var sp in searchPaths.MaybeEnumerate()) {
                if (ModulePath.FromBasePathAndName_NoThrow(sp, name, isPackage, null, requireInitPy, out mp, out _, out _, out _)) {
                    return mp;
                }
            }

            return null;
        }

        private async Task<IReadOnlyList<IPythonModule>> GetModuleStubsAsync(string moduleName, string modulePath, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            // Also search for type stub packages if enabled and we are not a blacklisted module
            if (_typeStubPaths.Count > 0 && moduleName != "typing") {
                var tsModule = await ImportFromTypeStubsAsync(moduleName, cancellationToken);
                if (tsModule != null) {
                    // TODO: What about custom stub files?
                    return new[] { tsModule };
                }
            }
            return Array.Empty<IPythonModule>();
        }

        private IEnumerable<string> GetTypeShedPaths(string typeshedRootPath) {
            if (string.IsNullOrEmpty(typeshedRootPath)) {
                yield break;
            }

            var stdlib = Path.Combine(typeshedRootPath, "stdlib");
            var thirdParty = Path.Combine(typeshedRootPath, "third_party");

            var v = Configuration.Version;
            foreach (var subdir in new[] { v.ToString(), v.Major.ToString(), "2and3" }) {
                yield return Path.Combine(stdlib, subdir);
            }

            foreach (var subdir in new[] { v.ToString(), v.Major.ToString(), "2and3" }) {
                yield return Path.Combine(thirdParty, subdir);
            }
        }

        private static IReadOnlyCollection<string> GetPackagesFromZipFile(string searchPath, CancellationToken cancellationToken) {
            // TODO: Search zip files for packages
            return new string[0];
        }

        private void ReloadModulePaths(in IEnumerable<string> rootPaths) {
            foreach (var modulePath in rootPaths.Where(Directory.Exists).SelectMany(p => PathUtils.EnumerateFiles(p))) {
                _pathResolver.TryAddModulePath(modulePath, out _);
            }
        }

        // For tests
        internal void AddUnimportableModule(string moduleName) => _modules[moduleName] = new SentinelModule(moduleName);
    }
}
