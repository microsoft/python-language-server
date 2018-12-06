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
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AstModuleResolution: IModuleResolution {
        private static readonly IReadOnlyDictionary<string, string> _emptyModuleSet = new Dictionary<string, string>();
        private readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        private readonly InterpreterConfiguration _configuration;
        private readonly IPythonInterpreterFactory _factory;
        private readonly IPythonInterpreter _interpreter;
        private readonly ILogger _log;
        private readonly bool _requireInitPy;

        private IReadOnlyDictionary<string, string> _searchPathPackages;
        private IReadOnlyList<string> _searchPaths;
        private AstBuiltinsPythonModule _builtinModule;

        public AstModuleResolution(
            IPythonInterpreter interpreter,
            IPythonInterpreterFactory factory
            ) {
            _interpreter = interpreter;
            _configuration = factory.Configuration;
            _log = factory.Log;
            _requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_configuration.Version);
            ModuleCache = new AstModuleCache(factory);
            CurrentPathResolver = new PathResolverSnapshot(_configuration.Version.ToLanguageVersion());
        }

        public IModuleCache ModuleCache { get; }
        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(_configuration.Version.ToLanguageVersion());

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        public PathResolverSnapshot CurrentPathResolver { get; }

        /// <summary>
        /// Builtins module.
        /// </summary>
        public IBuiltinPythonModule BuiltinModule {
            get {
                _builtinModule = _builtinModule ?? ImportModule(BuiltinModuleName) as AstBuiltinsPythonModule;
                return _builtinModule;
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(CancellationToken cancellationToken) {
            if (_searchPathPackages != null) {
                return _searchPathPackages;
            }

            var sp = await GetSearchPathsAsync(cancellationToken).ConfigureAwait(false);
            if (sp == null) {
                return _emptyModuleSet;
            }

            var packageDict = await GetImportableModulesAsync(sp, cancellationToken).ConfigureAwait(false);
            if (!packageDict.Any()) {
                return _emptyModuleSet;
            }

            _searchPathPackages = packageDict;
            return packageDict;
        }

        public async Task<IReadOnlyList<string>> GetSearchPathsAsync(CancellationToken cancellationToken) {
            if (_searchPaths != null) {
                return _searchPaths;
            }

            _searchPaths = await GetCurrentSearchPathsAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(_searchPaths != null, "Should have search paths");
            _log?.Log(TraceEventType.Information, $"Search paths: {_searchPaths.Cast<object>().ToArray()}");
            return _searchPaths;
        }

        private async Task<IReadOnlyList<string>> GetCurrentSearchPathsAsync(CancellationToken cancellationToken) {
            if (_configuration.SearchPaths.Any()) {
                return _configuration.SearchPaths;
            }

            if (!File.Exists(_configuration.InterpreterPath)) {
                return Array.Empty<string>();
            }

            _log?.Log(TraceEventType.Verbose, "GetCurrentSearchPaths", _configuration.InterpreterPath, _factory.SearchPathCachePath);
            try {
                var paths = await PythonLibraryPath.GetDatabaseSearchPathsAsync(_configuration, _factory.SearchPathCachePath).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return paths.MaybeEnumerate().Select(p => p.Path).ToArray();
            } catch (InvalidOperationException) {
                return Array.Empty<string>();
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(IEnumerable<string> searchPaths, CancellationToken cancellationToken) {
            var packageDict = new Dictionary<string, string>();

            foreach (var searchPath in searchPaths.MaybeEnumerate()) {
                IReadOnlyCollection<string> packages = null;
                if (File.Exists(searchPath)) {
                    packages = GetPackagesFromZipFile(searchPath, cancellationToken);
                } else if (Directory.Exists(searchPath)) {
                    packages = await Task.Run(() => GetPackagesFromDirectory(searchPath, cancellationToken)).ConfigureAwait(false);
                }
                foreach (var package in packages.MaybeEnumerate()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    packageDict[package] = searchPath;
                }
            }

            return packageDict;
        }

        public async Task<TryImportModuleResult> TryImportModuleAsync(string name, IReadOnlyList<string> typeStubPaths, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(name)) {
                return TryImportModuleResult.ModuleNotFound;
            }

            Debug.Assert(!name.EndsWithOrdinal("."), $"{name} should not end with '.'");

            // Handle builtins explicitly
            if (name == BuiltinModuleName) {
                Debug.Fail($"Interpreters must handle import {name} explicitly");
                return TryImportModuleResult.NotSupported;
            }

            // Return any existing module
            if (_modules.TryGetValue(name, out var module) && module != null) {
                if (module is SentinelModule sentinelModule) {
                    // If we are importing this module on another thread, allow
                    // time for it to complete. This does not block if we are
                    // importing on the current thread or the module is not
                    // really being imported.
                    try {
                        module = await sentinelModule.WaitForImportAsync(cancellationToken);
                    } catch (OperationCanceledException) {
                        _log?.Log(TraceEventType.Warning, $"Import timeout: {name}");
                        return TryImportModuleResult.Timeout;
                    }

                    if (module is SentinelModule) {
                        _log?.Log(TraceEventType.Warning, $"Recursive import: {name}");
                    }
                }
                return new TryImportModuleResult(module);
            }

            // Set up a sentinel so we can detect recursive imports
            var sentinelValue = new SentinelModule(name, true);
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
            if (!string.IsNullOrEmpty(_configuration?.InterpreterPath)) {
                try {
                    module = ImportFromSearchPaths(name);
                } catch (OperationCanceledException) {
                    _log?.Log(TraceEventType.Error, $"Import timeout {name}");
                    return TryImportModuleResult.Timeout;
                }
            }

            if (module == null) {
                module = ModuleCache.ImportFromCache(name, _interpreter);
            }

            // Also search for type stub packages if enabled and we are not a blacklisted module
            if (module != null && typeStubPaths != null && module.Name != "typing") {
                var tsModule = ImportFromTypeStubs(module.Name, typeStubPaths);
                if (tsModule != null) {
                    module = AstPythonMultipleMembers.CombineAs<IPythonModule>(module, tsModule);
                }
            }

            // Replace our sentinel, or if we raced, get the current
            // value and abandon the one we just created.
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
            sentinelValue.Complete(module);

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

        public IPythonModule ImportModule(string name) {
            var token = new CancellationTokenSource(5000).Token;
#if DEBUG
            token = Debugger.IsAttached ? CancellationToken.None : token;
#endif
            var impTask = ImportModuleAsync(name, token);
            return impTask.Wait(10000) ? impTask.WaitAndUnwrapExceptions() : null;
        }

        public async Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token) {
            if (name == BuiltinModuleName) {
                if (_builtinModule == null) {
                    _modules[BuiltinModuleName] = _builtinModule = new AstBuiltinsPythonModule(_interpreter);
                }
                return _builtinModule;
            }

            var typeStubPaths = _analyzer.Limits.UseTypeStubPackages ? GetTypeStubPaths() : null;

            for (var retries = 5; retries > 0; --retries) {
                // The call should be cancelled by the cancellation token, but since we
                // are blocking here we wait for slightly longer. Timeouts are handled
                // gracefully by TryImportModuleAsync(), so we want those to trigger if
                // possible, but if all else fails then we'll abort and treat it as an
                // error.
                // (And if we've got a debugger attached, don't time out at all.)
                TryImportModuleResult result;
                try {
                    result = await TryImportModuleAsync(name, CurrentPathResolver, typeStubPaths, token);
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

        private ModulePath? FindModuleInSearchPath(IReadOnlyList<string> searchPaths, IReadOnlyDictionary<string, string> packages, string name) {
            if (searchPaths == null || searchPaths.Count == 0) {
                return null;
            }

            _log?.Log(TraceEventType.Verbose, "FindModule", name, "system", string.Join(", ", searchPaths));

            var i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);
            string searchPath;

            ModulePath mp;
            Func<string, bool> isPackage = IsPackage;
            if (firstBit.EndsWithOrdinal("-stubs", ignoreCase: true)) {
                isPackage = Directory.Exists;
            }

            var requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_configuration.Version);
            if (packages != null && packages.TryGetValue(firstBit, out searchPath) && !string.IsNullOrEmpty(searchPath)) {
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

        private static IReadOnlyCollection<string> GetPackagesFromZipFile(string searchPath, CancellationToken cancellationToken) {
            // TODO: Search zip files for packages
            return new string[0];
        }

        private IPythonModule ImportFromTypeStubs(string name, IReadOnlyList<string> typeStubPaths) {
            var mp = FindModuleInSearchPath(typeStubPaths, null, name);

            if (mp == null) {
                var i = name.IndexOf('.');
                if (i == 0) {
                    Debug.Fail("Invalid module name");
                    return null;
                }

                foreach (var stubPath in CurrentPathResolver.GetPossibleModuleStubPaths(name)) {
                    if (File.Exists(stubPath)) {
                        return PythonModuleLoader.FromTypeStub(_interpreter, stubPath, _configuration.Version.ToLanguageVersion(), name);
                    }
                }
            }

            if (mp == null && typeStubPaths != null && typeStubPaths.Count > 0) {
                mp = FindModuleInSearchPath(typeStubPaths.SelectMany(GetTypeShedPaths).ToArray(), null, name);
            }

            if (mp == null) {
                return null;
            }

            if (mp.Value.IsCompiled) {
                Debug.Fail("Unsupported native module in typeshed");
                return null;
            }

            _log?.Log(TraceEventType.Verbose, "Import type stub", mp?.FullName, mp?.SourceFile);
            return PythonModuleLoader.FromTypeStub(_interpreter, mp?.SourceFile, _configuration.Version.ToLanguageVersion(), mp?.FullName);
        }

        public IEnumerable<string> GetTypeShedPaths(string path) {
            var stdlib = Path.Combine(path, "stdlib");
            var thirdParty = Path.Combine(path, "third_party");

            var v = _configuration.Version;
            foreach (var subdir in new[] { v.ToString(), v.Major.ToString(), "2and3" }) {
                yield return Path.Combine(stdlib, subdir);
            }

            foreach (var subdir in new[] { v.ToString(), v.Major.ToString(), "2and3" }) {
                yield return Path.Combine(thirdParty, subdir);
            }
        }

        private IPythonModule ImportFromSearchPaths(string name) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                _log?.Log(TraceEventType.Verbose, "Import not found: ", name);
                return null;
            }

            if (moduleImport.IsBuiltin) {
                _log?.Log(TraceEventType.Verbose, "Import builtins: ", name, _configuration.InterpreterPath);
                return new AstBuiltinPythonModule(name, _interpreter);
            }

            if (moduleImport.IsCompiled) {
                _log?.Log(TraceEventType.Verbose, "Import scraped: ", moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath);
                return new AstScrapedPythonModule(moduleImport.FullName, moduleImport.ModulePath, _interpreter);
            }

            _log?.Log(TraceEventType.Verbose, "Import: ", moduleImport.FullName, moduleImport.ModulePath);
            return PythonModuleLoader.FromFile(_interpreter, moduleImport.ModulePath, _configuration.Version.ToLanguageVersion(), moduleImport.FullName);
        }

        /// <summary>
        /// Determines whether the specified directory is an importable package.
        /// </summary>
        public bool IsPackage(string directory) 
            => ModulePath.PythonVersionRequiresInitPyFiles(_configuration.Version) ?
                !string.IsNullOrEmpty(ModulePath.GetPackageInitPy(directory)) :
                Directory.Exists(directory);

        public async Task<ModulePath> FindModuleAsync(string filePath, CancellationToken cancellationToken) {
            var sp = await GetSearchPathsAsync(cancellationToken);
            var bestLibraryPath = "";

            foreach (var p in sp) {
                if (PathEqualityComparer.Instance.StartsWith(filePath, p)) {
                    if (p.Length > bestLibraryPath.Length) {
                        bestLibraryPath = p;
                    }
                }
            }

            var mp = ModulePath.FromFullPath(filePath, bestLibraryPath);
            return mp;
        }
    }
}
