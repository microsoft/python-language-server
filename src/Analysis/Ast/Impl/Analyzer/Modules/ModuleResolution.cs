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
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Shell;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal sealed class ModuleResolution : IModuleResolution {
        private static readonly IReadOnlyDictionary<string, string> _emptyModuleSet = EmptyDictionary<string, string>.Instance;
        private readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        private readonly IServiceContainer _services;
        private readonly IPythonInterpreter _interpreter;
        private readonly PathResolver _pathResolver;
        private readonly IFileSystem _fs;
        private readonly ILogger _log;
        private readonly bool _requireInitPy;

        private IReadOnlyDictionary<string, string> _searchPathPackages;

        private ILogger Log { get; }
        private InterpreterConfiguration Configuration => _interpreter.Configuration;

        public ModuleResolution(IServiceContainer services) {
            _services = services;
            _interpreter = services.GetService<IPythonInterpreter>();
            _fs = services.GetService<IFileSystem>();
            _log = services.GetService<ILogger>();

            _requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_interpreter.Configuration.Version);
            ModuleCache = new ModuleCache(services);

            _pathResolver = new PathResolver(_interpreter.LanguageVersion);
            _pathResolver.SetInterpreterSearchPaths(new[] {
                _interpreter.Configuration.LibraryPath,
                _interpreter.Configuration.SitePackagesPath,
            });
            _pathResolver.SetUserSearchPaths(_interpreter.Configuration.SearchPaths);
            _modules[BuiltinModuleName] = BuiltinModule = new BuiltinsPythonModule(_interpreter, services);

            BuildModuleList();
        }

        private void BuildModuleList() {
            // Initialize built-in
            ((IAnalyzable)BuiltinModule).AnalyzeAsync(new CancellationTokenSource(12000).Token).Wait(10000);

            // Add built-in module names
            var builtinModuleNamesMember = BuiltinModule.GetAnyMember("__builtin_module_names__");
            if (builtinModuleNamesMember is AstPythonStringLiteral builtinModuleNamesLiteral && builtinModuleNamesLiteral.Value != null) {
                var builtinModuleNames = builtinModuleNamesLiteral.Value.Split(',').Select(n => n.Trim());
                _pathResolver.SetBuiltins(builtinModuleNames);
            }

            // Add names from search paths
            var paths = _interpreter.Configuration.SearchPaths
                        .Concat(Enumerable.Repeat(_interpreter.Configuration.LibraryPath, 1)
                        .Concat(Enumerable.Repeat(_interpreter.Configuration.SitePackagesPath, 1)));
            // TODO: how to remove?
            foreach (var modulePath in paths.Where(_fs.DirectoryExists).SelectMany(p => _fs.GetFiles(p))) {
                _pathResolver.TryAddModulePath(modulePath, out _);
            }
        }

        public IModuleCache ModuleCache { get; }
        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        public PathResolverSnapshot CurrentPathResolver => _pathResolver.CurrentSnapshot;

        /// <summary>
        /// Builtins module.
        /// </summary>
        public IBuiltinPythonModule BuiltinModule { get; }

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

        public IReadOnlyList<string> SearchPaths => Configuration.SearchPaths;

        public async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(IEnumerable<string> searchPaths, CancellationToken cancellationToken) {
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

        public async Task<TryImportModuleResult> TryImportModuleAsync(string name, CancellationToken cancellationToken) {
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
            if (!string.IsNullOrEmpty(Configuration?.InterpreterPath)) {
                try {
                    module = await ImportFromSearchPathsAsync(name, cancellationToken);
                } catch (OperationCanceledException) {
                    _log?.Log(TraceEventType.Error, $"Import timeout {name}");
                    return TryImportModuleResult.Timeout;
                }
            }

            if (module == null) {
                var document = ModuleCache.ImportFromCache(name);
                await document.GetAnalysisAsync(cancellationToken);
                module = document;
            }

            var typeStubPaths = GetTypeShedPaths(Configuration?.TypeshedPath).ToArray();
            // Also search for type stub packages if enabled and we are not a blacklisted module
            if (module != null && typeStubPaths.Length > 0 && module.Name != "typing") {
                var tsModule = ImportFromTypeStubs(module.Name, typeStubPaths);
                if (tsModule != null) {
                    module = PythonMultipleTypes.CombineAs<IPythonModule>(module, tsModule);
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
                return BuiltinModule;
            }

            for (var retries = 5; retries > 0; --retries) {
                // The call should be cancelled by the cancellation token, but since we
                // are blocking here we wait for slightly longer. Timeouts are handled
                // gracefully by TryImportModuleAsync(), so we want those to trigger if
                // possible, but if all else fails then we'll abort and treat it as an
                // error.
                // (And if we've got a debugger attached, don't time out at all.)
                TryImportModuleResult result;
                try {
                    result = await TryImportModuleAsync(name, token);
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

        private static IReadOnlyCollection<string> GetPackagesFromZipFile(string searchPath, CancellationToken cancellationToken) {
            // TODO: Search zip files for packages
            return new string[0];
        }

        private IPythonModule ImportFromTypeStubs(string name, IReadOnlyList<string> typeStubPaths) {
            var mp = FindModuleInSearchPath(typeStubPaths, null, name);

            var rdt = _services.GetService<IRunningDocumentTable>();
            if (mp == null) {
                var i = name.IndexOf('.');
                if (i == 0) {
                    Debug.Fail("Invalid module name");
                    return null;
                }

                foreach (var stubPath in CurrentPathResolver.GetPossibleModuleStubPaths(name)) {
                    if (_fs.FileExists(stubPath)) {
                        return rdt.AddModule(name, ModuleType.Stub, stubPath, null, DocumentCreationOptions.Analyze);
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

            _log?.Log(TraceEventType.Verbose, "Import type stub", mp.Value.FullName, mp.Value.SourceFile);
            return rdt.AddModule(mp.Value.FullName, ModuleType.Stub, mp.Value.SourceFile, null, DocumentCreationOptions.Analyze);
        }

        public IEnumerable<string> GetTypeShedPaths(string typeshedRootPath) {
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

        private async Task<IPythonModule> ImportFromSearchPathsAsync(string name, CancellationToken cancellationToken) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                _log?.Log(TraceEventType.Verbose, "Import not found: ", name);
                return null;
            }

            IDocument document;
            if (moduleImport.IsBuiltin) {
                _log?.Log(TraceEventType.Verbose, "Import builtins: ", name, Configuration.InterpreterPath);
                document = new CompiledBuiltinPythonModule(name, _services);
            } else if (moduleImport.IsCompiled) {
                _log?.Log(TraceEventType.Verbose, "Import scraped: ", moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath);
                document = new CompiledPythonModule(moduleImport.FullName, ModuleType.Compiled, moduleImport.ModulePath, _services);
            } else {
                _log?.Log(TraceEventType.Verbose, "Import: ", moduleImport.FullName, moduleImport.ModulePath);
                var rdt = _services.GetService<IRunningDocumentTable>();
                // TODO: handle user code and library module separately.
                document = rdt.AddModule(moduleImport.FullName, ModuleType.Library, moduleImport.ModulePath, null, DocumentCreationOptions.Analyze);
            }

            var analyzer = _services.GetService<IPythonAnalyzer>();
            if (document.ModuleType == ModuleType.Library || document.ModuleType == ModuleType.User) {
                await analyzer.AnalyzeDocumentDependencyChainAsync(document, cancellationToken);
            } else {
                await analyzer.AnalyzeDocumentAsync(document, cancellationToken);
            }
            return document;
        }

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
    }
}
