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
using Microsoft.Python.Parsing;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    internal sealed class AstModuleResolution {
        private static IReadOnlyDictionary<string, string> _emptyModuleSet = new Dictionary<string, string>();
        private readonly IPythonInterpreter _interpreter;
        private readonly ConcurrentDictionary<string, IPythonModule> _modules;
        private readonly AstModuleCache _astModuleCache;
        private readonly InterpreterConfiguration _configuration;
        private readonly AnalysisLogWriter _log;
        private readonly bool _requireInitPy;

        private IReadOnlyDictionary<string, string> _searchPathPackages;
        private IReadOnlyList<string> _searchPaths;
        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(_configuration.Version.ToLanguageVersion());

        public AstModuleResolution(IPythonInterpreter interpreter, ConcurrentDictionary<string, IPythonModule> modules, AstModuleCache astModuleCache, InterpreterConfiguration configuration, AnalysisLogWriter log) {
            _interpreter = interpreter;
            _modules = modules;
            _astModuleCache = astModuleCache;
            _configuration = configuration;
            _log = log;
            _requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_configuration.Version);
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
            _log?.Log(TraceLevel.Info, "SearchPaths", _searchPaths.Cast<object>().ToArray());
            return _searchPaths;
        }

        private async Task<IReadOnlyList<string>> GetCurrentSearchPathsAsync(CancellationToken cancellationToken) {
            if (_configuration.SearchPaths.Any()) {
                return _configuration.SearchPaths;
            }

            if (!File.Exists(_configuration.InterpreterPath)) {
                return Array.Empty<string>();
            }

            _log?.Log(TraceLevel.Info, "GetCurrentSearchPaths", _configuration.InterpreterPath, _astModuleCache.SearchPathCachePath);
            try {
                var paths = await PythonLibraryPath.GetDatabaseSearchPathsAsync(_configuration, _astModuleCache.SearchPathCachePath).ConfigureAwait(false);
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

        private ModulePath? FindModuleInSearchPath(IReadOnlyList<string> searchPaths, IReadOnlyDictionary<string, string> packages, string name) {
            if (searchPaths == null || searchPaths.Count == 0) {
                return null;
            }

            _log?.Log(TraceLevel.Verbose, "FindModule", name, "system", string.Join(", ", searchPaths));

            int i = name.IndexOf('.');
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
        
        public async Task<TryImportModuleResult> TryImportModuleAsync(string name, PathResolverSnapshot pathResolver, IReadOnlyList<string> typeStubPaths, bool mergeTypeStubPackages, CancellationToken cancellationToken) {
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
                        _log?.Log(TraceLevel.Warning, "ImportTimeout", name);
                        return TryImportModuleResult.Timeout;
                    }

                    if (module is SentinelModule) {
                        _log?.Log(TraceLevel.Warning, "RecursiveImport", name);
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
                _log?.Log(TraceLevel.Warning, "RetryImport", name);
                return TryImportModuleResult.NeedRetry;
            }

            // Do normal searches
            if (!string.IsNullOrEmpty(_configuration?.InterpreterPath)) {
                try {
                    module = ImportFromSearchPaths(name, pathResolver);
                } catch (OperationCanceledException) {
                    _log?.Log(TraceLevel.Error, "ImportTimeout", name, "ImportFromSearchPaths");
                    return TryImportModuleResult.Timeout;
                }
            }

            if (module == null) {
                module = _astModuleCache.ImportFromCache(name, _interpreter);
            }

            // Also search for type stub packages if enabled and we are not a blacklisted module
            if (module != null && typeStubPaths != null && module.Name != "typing") {
                var tsModule = ImportFromTypeStubs(module.Name, typeStubPaths, pathResolver);
                if (tsModule != null) {
                    module = mergeTypeStubPackages ? AstPythonMultipleMembers.CombineAs<IPythonModule>(module, tsModule) : tsModule;
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
                _log?.Log(TraceLevel.Warning, "RetryImport", name);
                return TryImportModuleResult.NeedRetry;
            }
            sentinelValue.Complete(module);

            return new TryImportModuleResult(module);
        }

        private IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, CancellationToken cancellationToken) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true,
                requireInitPy: _requireInitPy
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).TakeWhile(_ => !cancellationToken.IsCancellationRequested).ToList();
        }

        private static IReadOnlyCollection<string> GetPackagesFromZipFile(string searchPath, CancellationToken cancellationToken) {
            // TODO: Search zip files for packages
            return new string[0];
        }

        private IPythonModule ImportFromTypeStubs(string name, IReadOnlyList<string> typeStubPaths, PathResolverSnapshot pathResolver) {
            var mp = FindModuleInSearchPath(typeStubPaths, null, name);

            if (mp == null) {
                var i = name.IndexOf('.');
                if (i == 0) {
                    Debug.Fail("Invalid module name");
                    return null;
                }

                foreach (var stubPath in pathResolver.GetPossibleModuleStubPaths(name)) {
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

            _log?.Log(TraceLevel.Verbose, "ImportTypeStub", mp?.FullName, mp?.SourceFile);
            return PythonModuleLoader.FromTypeStub(_interpreter, mp?.SourceFile, _configuration.Version.ToLanguageVersion(), mp?.FullName);
        }

        private IEnumerable<string> GetTypeShedPaths(string path) {
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

        private IPythonModule ImportFromSearchPaths(string name, PathResolverSnapshot pathResolver) {
            var moduleImport = pathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                _log?.Log(TraceLevel.Verbose, "ImportNotFound", name);
                return null;
            }

            if (moduleImport.IsBuiltin) {
                _log?.Log(TraceLevel.Info, "ImportBuiltins", name, _configuration.InterpreterPath);
                return new AstBuiltinPythonModule(name, _configuration.InterpreterPath);
            }

            if (moduleImport.IsCompiled) {
                _log?.Log(TraceLevel.Verbose, "ImportScraped", moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath);
                return new AstScrapedPythonModule(moduleImport.FullName, moduleImport.ModulePath);
            }

            _log?.Log(TraceLevel.Verbose, "Import", moduleImport.FullName, moduleImport.ModulePath);
            return PythonModuleLoader.FromFile(_interpreter, moduleImport.ModulePath, _configuration.Version.ToLanguageVersion(), moduleImport.FullName);
        }

        /// <summary>
        /// Determines whether the specified directory is an importable package.
        /// </summary>
        private bool IsPackage(string directory) {
            return ModulePath.PythonVersionRequiresInitPyFiles(_configuration.Version) ?
                !string.IsNullOrEmpty(ModulePath.GetPackageInitPy(directory)) :
                Directory.Exists(directory);
        }

        private async Task<ModulePath> FindModuleAsync(string filePath, CancellationToken cancellationToken) {
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

        internal static async Task<ModulePath> FindModuleAsync(AstPythonInterpreter interpreter, string filePath, CancellationToken cancellationToken) {
            try {
                return await interpreter.ModuleResolution.FindModuleAsync(filePath, cancellationToken);
            } catch (ArgumentException) {
                return default;
            }
        }
    }
}
