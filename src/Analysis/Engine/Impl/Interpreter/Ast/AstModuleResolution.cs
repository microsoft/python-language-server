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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    internal sealed class AstModuleResolution {
        private static IReadOnlyDictionary<string, string> _emptyModuleSet = new Dictionary<string, string>();
        private readonly AstModuleCache _moduleCache;
        private readonly InterpreterConfiguration _configuration;
        private readonly AnalysisLogWriter _log;
        private readonly bool _requireInitPy;

        private IReadOnlyDictionary<string, string> _searchPathPackages;
        private IReadOnlyList<string> _searchPaths;

        public AstModuleResolution(AstModuleCache moduleCache, InterpreterConfiguration configuration, AnalysisLogWriter log) {
            _moduleCache = moduleCache;
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

            _log?.Log(TraceLevel.Info, "GetCurrentSearchPaths", _configuration.InterpreterPath, _moduleCache.SearchPathCachePath);
            try {
                var paths = await PythonLibraryPath.GetDatabaseSearchPathsAsync(_configuration, _moduleCache.SearchPathCachePath).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return paths.MaybeEnumerate().Select(p => p.Path).ToArray();
            } catch (InvalidOperationException) {
                return Array.Empty<string>();
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> GetPackagesFromSearchPathsAsync(IReadOnlyList<string> searchPaths, CancellationToken cancellationToken) {
            if (searchPaths == null || searchPaths.Count == 0) {
                return _emptyModuleSet;
            }

            _log?.Log(TraceLevel.Verbose, "GetImportableModulesAsync");
            return await GetImportableModulesAsync(searchPaths, cancellationToken);
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

        /// <summary>
        /// For test use only
        /// </summary>
        internal void SetCurrentSearchPaths(IEnumerable<string> paths) {
            _searchPaths = paths.ToArray();
            _searchPathPackages = null;
        }

        public async Task<TryImportModuleResult> TryImportModuleAsync(string name, TryImportModuleContext context, CancellationToken cancellationToken) {
            IPythonModule module = null;
            if (string.IsNullOrEmpty(name)) {
                return TryImportModuleResult.ModuleNotFound;
            }

            Debug.Assert(!name.EndsWithOrdinal("."), $"{name} should not end with '.'");

            // Handle builtins explicitly
            if (name == BuiltinTypeId.Unknown.GetModuleName(_configuration.Version.ToLanguageVersion())) {
                Debug.Fail($"Interpreters must handle import {name} explicitly");
                return TryImportModuleResult.NotSupported;
            }

            var modules = context?.ModuleCache;
            SentinelModule sentinelValue = null;

            if (modules != null) {
                // Return any existing module
                if (modules.TryGetValue(name, out module) && module != null) {
                    if (module is SentinelModule smod) {
                        // If we are importing this module on another thread, allow
                        // time for it to complete. This does not block if we are
                        // importing on the current thread or the module is not
                        // really being imported.
                        try {
                            module = await smod.WaitForImportAsync(cancellationToken);
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
                sentinelValue = new SentinelModule(name, true);
                if (!modules.TryAdd(name, sentinelValue)) {
                    // Try to get the new module, in case we raced with a .Clear()
                    if (modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                        return new TryImportModuleResult(module);
                    }
                    // If we reach here, the race is too complicated to recover
                    // from. Signal the caller to try importing again.
                    _log?.Log(TraceLevel.Warning, "RetryImport", name);
                    return TryImportModuleResult.NeedRetry;
                }
            }

            // Do normal searches
            if (!string.IsNullOrEmpty(_configuration?.InterpreterPath)) {
                try {
                    module = await ImportFromSearchPathsAsync(name, context, cancellationToken);
                } catch (OperationCanceledException) {
                    _log?.Log(TraceLevel.Error, "ImportTimeout", name, "ImportFromSearchPaths");
                    return TryImportModuleResult.Timeout;
                }

                if (module == null) {
                    module = ImportFromBuiltins(name, context.BuiltinModule as AstBuiltinsPythonModule);
                }
            }
            if (module == null) {
                module = _moduleCache.ImportFromCache(name, context);
            }

            // Also search for type stub packages if enabled and we are not a blacklisted module
            if (module != null && context?.TypeStubPaths != null && module.Name != "typing") {
                var tsModule = await ImportFromTypeStubsAsync(module.Name, context, cancellationToken);
                if (tsModule != null) {
                    if (context.MergeTypeStubPackages) {
                        module = AstPythonMultipleMembers.CombineAs<IPythonModule>(module, tsModule);
                    } else {
                        module = tsModule;
                    }
                }
            }

            if (modules != null) {
                // Replace our sentinel, or if we raced, get the current
                // value and abandon the one we just created.
                if (!modules.TryUpdate(name, module, sentinelValue)) {
                    // Try to get the new module, in case we raced
                    if (modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                        return new TryImportModuleResult(module);
                    }
                    // If we reach here, the race is too complicated to recover
                    // from. Signal the caller to try importing again.
                    _log?.Log(TraceLevel.Warning, "RetryImport", name);
                    return TryImportModuleResult.NeedRetry;
                }
                sentinelValue.Complete(module);
            }

            return new TryImportModuleResult(module);
        }

        private async Task<IPythonModule> ImportFromTypeStubsAsync(string name, TryImportModuleContext context, CancellationToken cancellationToken) {
            var mp = FindModuleInSearchPath(context.TypeStubPaths, null, name);

            if (mp == null) {
                int i = name.IndexOf('.');
                if (i == 0) {
                    Debug.Fail("Invalid module name");
                    return null;
                }
                var stubName = i < 0 ? (name + "-stubs") : (name.Remove(i)) + "-stubs" + name.Substring(i);
                ModulePath? stubMp = null;
                if (context.FindModuleInUserSearchPathAsync != null) {
                    try {
                        stubMp = await context.FindModuleInUserSearchPathAsync(stubName, cancellationToken);
                    } catch (Exception ex) {
                        _log?.Log(TraceLevel.Error, "Exception", ex.ToString());
                        _log?.Flush();
                        return null;
                    }
                }
                if (stubMp == null) {
                    stubMp = await FindModuleInSearchPathAsync(stubName, cancellationToken);
                }

                if (stubMp != null) {
                    mp = new ModulePath(name, stubMp?.SourceFile, stubMp?.LibraryPath);
                }
            }

            if (mp == null && context.TypeStubPaths != null && context.TypeStubPaths.Count > 0) {
                mp = FindModuleInSearchPath(context.TypeStubPaths.SelectMany(GetTypeShedPaths).ToArray(), null, name);
            }

            if (mp == null) {
                return null;
            }

            if (mp.Value.IsCompiled) {
                Debug.Fail("Unsupported native module in typeshed");
                return null;
            }

            _log?.Log(TraceLevel.Verbose, "ImportTypeStub", mp?.FullName, mp?.SourceFile);
            return PythonModuleLoader.FromTypeStub(context.Interpreter, mp?.SourceFile, _configuration.Version.ToLanguageVersion(), mp?.FullName);
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

        private IPythonModule ImportFromBuiltins(string name, AstBuiltinsPythonModule builtinModule) {
            if (builtinModule == null) {
                return null;
            }
            var bmn = builtinModule.GetAnyMember("__builtin_module_names__") as AstPythonStringLiteral;
            var names = bmn?.Value ?? string.Empty;
            // Quick substring check
            if (!names.Contains(name)) {
                return null;
            }
            // Proper split/trim check
            if (!names.Split(',').Select(n => n.Trim()).Contains(name)) {
                return null;
            }

            _log?.Log(TraceLevel.Info, "ImportBuiltins", name, _configuration.InterpreterPath);

            try {
                return new AstBuiltinPythonModule(name, _configuration.InterpreterPath);
            } catch (ArgumentNullException) {
                Debug.Fail("No factory means cannot import builtin modules");
                return null;
            }
        }

        public async Task<ModulePath?> FindModuleInSearchPathAsync(string name, CancellationToken cancellationToken) {
            var searchPaths = await GetSearchPathsAsync(cancellationToken).ConfigureAwait(false);
            var packages = await GetImportableModulesAsync(cancellationToken).ConfigureAwait(false);
            return FindModuleInSearchPath(searchPaths, packages, name);
        }

        public ModulePath? FindModuleInSearchPath(IReadOnlyList<string> searchPaths, IReadOnlyDictionary<string, string> packages, string name) {
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

        private async Task<IPythonModule> ImportFromSearchPathsAsync(string name, TryImportModuleContext context, CancellationToken cancellationToken) {
            ModulePath? mmp = null;
            if (context.FindModuleInUserSearchPathAsync != null) {
                try {
                    mmp = await context.FindModuleInUserSearchPathAsync(name, cancellationToken);
                } catch (Exception ex) {
                    _log?.Log(TraceLevel.Error, "Exception", ex.ToString());
                    _log?.Flush();
                    return null;
                }
            }

            if (!mmp.HasValue) {
                mmp = await FindModuleInSearchPathAsync(name, cancellationToken);
            }

            if (!mmp.HasValue) {
                _log?.Log(TraceLevel.Verbose, "ImportNotFound", name);
                return null;
            }

            var mp = mmp.Value;
            IPythonModule module;

            if (mp.IsCompiled) {
                _log?.Log(TraceLevel.Verbose, "ImportScraped", mp.FullName, mp.SourceFile);
                module = new AstScrapedPythonModule(mp.FullName, mp.SourceFile);
            } else {
                _log?.Log(TraceLevel.Verbose, "Import", mp.FullName, mp.SourceFile);
                module = PythonModuleLoader.FromFile(context.Interpreter, mp.SourceFile, _configuration.Version.ToLanguageVersion(), mp.FullName);
            }

            return module;
        }

        /// <summary>
        /// Determines whether the specified directory is an importable package.
        /// </summary>
        public bool IsPackage(string directory) {
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

        internal static async Task<ModulePath> FindModuleAsync(IPythonInterpreterFactory factory, string filePath, CancellationToken cancellationToken) {
            try {
                var apif = factory as AstPythonInterpreterFactory;
                if (apif != null) {
                    return await apif.ModuleResolution.FindModuleAsync(filePath, cancellationToken);
                }
                return ModulePath.FromFullPath(filePath);
            } catch (ArgumentException) {
                return default(ModulePath);
            }
        }
    }
}
