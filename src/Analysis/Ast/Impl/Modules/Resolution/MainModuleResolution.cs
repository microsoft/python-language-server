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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal sealed class MainModuleResolution : ModuleResolutionBase, IModuleManagement {
        private readonly ConcurrentDictionary<string, IPythonModule> _specialized = new ConcurrentDictionary<string, IPythonModule>();
        private IReadOnlyList<string> _searchPaths;

        public MainModuleResolution(string root, IServiceContainer services)
            : base(root, services) { }

        internal async Task InitializeAsync(CancellationToken cancellationToken = default) {
            // Add names from search paths
            await ReloadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Initialize built-in
            var moduleName = BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);
            var modulePath = ModuleCache.GetCacheFilePath(_interpreter.Configuration.InterpreterPath);

            var b = new BuiltinsPythonModule(moduleName, modulePath, _services);
            BuiltinsModule = b;
            _modules[BuiltinModuleName] = new ModuleRef(b);
        }

        public async Task<IReadOnlyList<string>> GetSearchPathsAsync(CancellationToken cancellationToken = default) {
            if (_searchPaths != null) {
                return _searchPaths;
            }

            _searchPaths = await GetInterpreterSearchPathsAsync(cancellationToken);
            Debug.Assert(_searchPaths != null, "Should have search paths");
            _searchPaths = _searchPaths != null 
                ? Configuration.SearchPaths != null 
                    ? _searchPaths.Concat(Configuration.SearchPaths).ToArray()
                    : _searchPaths
                : Array.Empty<string>();

            _log?.Log(TraceEventType.Information, "SearchPaths:");
            foreach (var s in _searchPaths) {
                _log?.Log(TraceEventType.Information, $"    {s}");
            }
            return _searchPaths;
        }

        protected override IPythonModule CreateModule(string name) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                _log?.Log(TraceEventType.Verbose, "Import not found: ", name);
                return null;
            }

            var rdt = _services.GetService<IRunningDocumentTable>();
            IPythonModule module;
            if (!string.IsNullOrEmpty(moduleImport.ModulePath) && Uri.TryCreate(moduleImport.ModulePath, UriKind.Absolute, out var uri)) {
                module = rdt.GetDocument(uri);
                if (module != null) {
                    return module;
                }
            }

            // If there is a stub, make sure it is loaded and attached
            // First check stub next to the module.
            if (!TryCreateModuleStub(name, moduleImport.ModulePath, out var stub)) {
                // If nothing found, try Typeshed.
                stub = _interpreter.TypeshedResolution.GetOrLoadModule(moduleImport.IsBuiltin ? name : moduleImport.FullName) as IPythonStubModule;
            }

            // If stub is created and its path equals to module, return that stub as module
            if (stub != null && stub.FilePath.PathEquals(moduleImport.ModulePath)) {
                return stub;
            }
            
            if (moduleImport.IsBuiltin) {
                _log?.Log(TraceEventType.Verbose, "Create built-in compiled (scraped) module: ", name, Configuration.InterpreterPath);
                module = new CompiledBuiltinPythonModule(name, stub, _services);
            } else if (moduleImport.IsCompiled) {
                _log?.Log(TraceEventType.Verbose, "Create compiled (scraped): ", moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath);
                module = new CompiledPythonModule(moduleImport.FullName, ModuleType.Compiled, moduleImport.ModulePath, stub, _services);
            } else {
                _log?.Log(TraceEventType.Verbose, "Import: ", moduleImport.FullName, moduleImport.ModulePath);
                // Module inside workspace == user code.

                var mco = new ModuleCreationOptions {
                    ModuleName = moduleImport.FullName,
                    ModuleType = moduleImport.IsLibrary ? ModuleType.Library : ModuleType.User,
                    FilePath = moduleImport.ModulePath,
                    Stub = stub
                };
                module = rdt.AddModule(mco);
            }

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

            Check.InvalidOperation(!(BuiltinsModule.Analysis is EmptyAnalysis), "After await");

            // Add built-in module names
            var builtinModuleNamesMember = BuiltinsModule.GetAnyMember("__builtin_module_names__");
            if (builtinModuleNamesMember.TryGetConstant<string>(out var s)) {
                var builtinModuleNames = s.Split(',').Select(n => n.Trim());
                PathResolver.SetBuiltins(builtinModuleNames);
            }
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default) {
            ModuleCache = new ModuleCache(_interpreter, _services);
            PathResolver = new PathResolver(_interpreter.LanguageVersion);
            
            var addedRoots = new HashSet<string>();
            addedRoots.UnionWith(PathResolver.SetRoot(_root));
            
            var interpreterPaths = await GetSearchPathsAsync(cancellationToken);
            addedRoots.UnionWith(PathResolver.SetInterpreterSearchPaths(interpreterPaths));
            
            addedRoots.UnionWith(SetUserSearchPaths(_interpreter.Configuration.SearchPaths));
            ReloadModulePaths(addedRoots);
        }

        public IEnumerable<string> SetUserSearchPaths(in IEnumerable<string> searchPaths)
            => PathResolver.SetUserSearchPaths(searchPaths);

        // For tests
        internal void AddUnimportableModule(string moduleName) 
            => _modules[moduleName] = new ModuleRef(new SentinelModule(moduleName, _services));

        private bool TryCreateModuleStub(string name, string modulePath, out IPythonStubModule module) {
            // First check stub next to the module.
            if (!string.IsNullOrEmpty(modulePath)) {
                var pyiPath = Path.ChangeExtension(modulePath, "pyi");
                if (_fs.FileExists(pyiPath)) {
                    module = new StubPythonModule(name, pyiPath, false, _services);
                    return true;
                }
            }

            // Try location of stubs that are in a separate folder next to the package.
            var stubPath = CurrentPathResolver.GetPossibleModuleStubPaths(name).FirstOrDefault(p => _fs.FileExists(p));
            module = !string.IsNullOrEmpty(stubPath) ? new StubPythonModule(name, stubPath, false, _services) : null;
            return module != null;
        }
    }
}
