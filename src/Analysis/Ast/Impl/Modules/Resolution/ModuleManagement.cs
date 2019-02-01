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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal sealed class ModuleManagement : ModuleResolutionBase, IModuleManagement {
        private readonly ConcurrentDictionary<string, IPythonModule> _specialized = new ConcurrentDictionary<string, IPythonModule>();
        private IReadOnlyList<string> _searchPaths;

        public ModuleManagement(string root, IServiceContainer services)
            : base(root, services) { }

        internal async Task InitializeAsync(CancellationToken cancellationToken = default) {
            // Add names from search paths
            await ReloadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Initialize built-in
            var moduleName = BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);
            var modulePath = ModuleCache.GetCacheFilePath(_interpreter.Configuration.InterpreterPath ?? "python.exe");

            var b = new BuiltinsPythonModule(moduleName, modulePath, _services);
            _modules[BuiltinModuleName] = BuiltinsModule = b;
        }

        public async Task<IReadOnlyList<string>> GetSearchPathsAsync(CancellationToken cancellationToken = default) {
            if (_searchPaths != null) {
                return _searchPaths;
            }

            _searchPaths = await GetInterpreterSearchPathsAsync(cancellationToken);
            Debug.Assert(_searchPaths != null, "Should have search paths");
            _searchPaths = _searchPaths != null
                            ? _searchPaths.Concat(Configuration.SearchPaths ?? Array.Empty<string>()).ToArray()
                            : Array.Empty<string>();

            _log?.Log(TraceEventType.Information, "SearchPaths:");
            foreach (var s in _searchPaths) {
                _log?.Log(TraceEventType.Information, $"    {s}");
            }
            return _searchPaths;
        }

        protected override async Task<IPythonModule> DoImportAsync(string name, CancellationToken cancellationToken = default) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                _log?.Log(TraceEventType.Verbose, "Import not found: ", name);
                return null;
            }

            // If there is a stub, make sure it is loaded and attached
            // First check stub next to the module.
            IPythonModule stub = null;
            if(!string.IsNullOrEmpty(moduleImport.ModulePath)) {
                var pyiPath = Path.ChangeExtension(moduleImport.ModulePath, "pyi");
                if(_fs.FileExists(pyiPath)) {
                    stub = new StubPythonModule(name, pyiPath, _services);
                    await stub.LoadAndAnalyzeAsync(cancellationToken);
                }
            }

            stub = stub ?? await _interpreter.StubResolution.ImportModuleAsync(moduleImport.IsBuiltin ? name : moduleImport.FullName, cancellationToken);

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
                    Stub = stub
                };
                module = rdt.AddModule(mco);
            }

            await module.LoadAndAnalyzeAsync(cancellationToken);
            return module;
        }

        private async Task<IReadOnlyList<string>> GetInterpreterSearchPathsAsync(CancellationToken cancellationToken = default) {
            if (!_fs.FileExists(Configuration.InterpreterPath)) {
                return Array.Empty<string>();
            }

            _log?.Log(TraceEventType.Information, "GetCurrentSearchPaths", Configuration.InterpreterPath, ModuleCache.SearchPathCachePath);
            try {
                var paths = await PythonLibraryPath.GetDatabaseSearchPathsAsync(Configuration, ModuleCache.SearchPathCachePath);
                cancellationToken.ThrowIfCancellationRequested();
                return paths.MaybeEnumerate().Select(p => p.Path).ToArray();
            } catch (InvalidOperationException) {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Provides ability to specialize module by replacing module import by
        /// <see cref="IPythonModule"/> implementation in code. Real module
        /// content is loaded and analyzed only for class/functions definitions
        /// so the original documentation can be extracted.
        /// </summary>
        /// <param name="name">Module to specialize.</param>
        /// <param name="specializationConstructor">Specialized module constructor.</param>
        /// <returns>Original (library) module loaded as stub.</returns>
        public IPythonModule SpecializeModule(string name, Func<string, IPythonModule> specializationConstructor) {
            var import = CurrentPathResolver.GetModuleImportFromModuleName(name);
            var module = specializationConstructor(import?.ModulePath);
            _specialized[name] = module;
            return module;
        }

        /// <summary>
        /// Returns specialized module, if any.
        /// </summary>
        public IPythonModule GetSpecializedModule(string name)
        => _specialized.TryGetValue(name, out var module) ? module : null;

        internal async Task LoadBuiltinTypesAsync(CancellationToken cancellationToken = default) {
            await BuiltinsModule.LoadAndAnalyzeAsync(cancellationToken);

            // Add built-in module names
            var builtinModuleNamesMember = BuiltinsModule.GetAnyMember("__builtin_module_names__");
            if (builtinModuleNamesMember.TryGetConstant<string>(out var s)) {
                var builtinModuleNames = s.Split(',').Select(n => n.Trim());
                _pathResolver.SetBuiltins(builtinModuleNames);
            }
        }

        public override async Task ReloadAsync(CancellationToken cancellationToken = default) {
            ModuleCache = new ModuleCache(_interpreter, _services);

            _pathResolver = new PathResolver(_interpreter.LanguageVersion);

            var addedRoots = _pathResolver.SetRoot(_root);
            ReloadModulePaths(addedRoots);

            var interpreterPaths = await GetSearchPathsAsync(cancellationToken);
            addedRoots = _pathResolver.SetInterpreterSearchPaths(interpreterPaths);

            ReloadModulePaths(addedRoots);
            cancellationToken.ThrowIfCancellationRequested();

            addedRoots = _pathResolver.SetUserSearchPaths(_interpreter.Configuration.SearchPaths);
            ReloadModulePaths(addedRoots);
        }

        // For tests
        internal void AddUnimportableModule(string moduleName)
            => _modules[moduleName] = new SentinelModule(moduleName, _services);
    }
}
