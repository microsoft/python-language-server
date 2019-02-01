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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal abstract class ModuleResolutionBase {
        protected readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        protected readonly IServiceContainer _services;
        protected readonly IPythonInterpreter _interpreter;
        protected readonly IFileSystem _fs;
        protected readonly ILogger _log;
        protected readonly bool _requireInitPy;
        protected string _root;

        protected PathResolver _pathResolver;

        protected InterpreterConfiguration Configuration => _interpreter.Configuration;

        protected ModuleResolutionBase(string root, IServiceContainer services) {
            _root = root;
            _services = services;
            _interpreter = services.GetService<IPythonInterpreter>();
            _fs = services.GetService<IFileSystem>();
            _log = services.GetService<ILogger>();

            _requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_interpreter.Configuration.Version);
        }

        public IModuleCache ModuleCache { get; protected set; }
        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        public PathResolverSnapshot CurrentPathResolver => _pathResolver.CurrentSnapshot;

        /// <summary>
        /// Builtins module.
        /// </summary>
        public IBuiltinsPythonModule BuiltinsModule { get; protected set; }

        public abstract Task ReloadAsync(CancellationToken cancellationToken = default);
        protected abstract Task<IPythonModule> DoImportAsync(string name, CancellationToken cancellationToken = default);

 public IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, CancellationToken cancellationToken) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true,
                requireInitPy: _requireInitPy
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).TakeWhile(_ => !cancellationToken.IsCancellationRequested).ToList();
        }

        public IPythonModule GetImportedModule(string name)
            => _modules.TryGetValue(name, out var module) ? module : null;

        public void AddModulePath(string path) => _pathResolver.TryAddModulePath(path, out var _);

        public ModulePath FindModule(string filePath) {
            var bestLibraryPath = string.Empty;

            foreach (var p in Configuration.SearchPaths) {
                if (PathEqualityComparer.Instance.StartsWith(filePath, p)) {
                    if (p.Length > bestLibraryPath.Length) {
                        bestLibraryPath = p;
                    }
                }
            }
            return ModulePath.FromFullPath(filePath, bestLibraryPath);
        }

        public async Task<IPythonModule> ImportModuleAsync(string name, CancellationToken cancellationToken = default) {
            if (name == BuiltinModuleName) {
                return BuiltinsModule;
            }
            var module = _interpreter.ModuleResolution.GetSpecializedModule(name);
            if (module != null) {
                return module;
            }
            return await DoImportModuleAsync(name, cancellationToken);
        }

        private async Task<IPythonModule> DoImportModuleAsync(string name, CancellationToken cancellationToken = default) {
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
                    //_log?.Log(TraceEventType.Warning, $"Recursive import: {name}");
                }
                return new TryImportModuleResult(module);
            }

            // Set up a sentinel so we can detect recursive imports
            var sentinelValue = new SentinelModule(name, _services);
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
                module = await DoImportAsync(name, cancellationToken);
            } catch (OperationCanceledException) {
                _log?.Log(TraceEventType.Error, $"Import timeout {name}");
                return TryImportModuleResult.Timeout;
            }

            if (ModuleCache != null) {
                module = module ?? await ModuleCache.ImportFromCacheAsync(name, cancellationToken);
            }

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

        protected void ReloadModulePaths(in IEnumerable<string> rootPaths) {
            foreach (var modulePath in rootPaths.Where(Directory.Exists).SelectMany(p => PathUtils.EnumerateFiles(p))) {
                _pathResolver.TryAddModulePath(modulePath, out _);
            }
        }

        protected async Task<IPythonModule> CreateStubModuleAsync(string moduleName, string filePath, CancellationToken cancellationToken = default) {
            _log?.Log(TraceEventType.Verbose, "Import type stub", moduleName, filePath);
            var module = new StubPythonModule(moduleName, filePath, _services);
            await module.LoadAndAnalyzeAsync(cancellationToken);
            return module;
        }
    }
}
