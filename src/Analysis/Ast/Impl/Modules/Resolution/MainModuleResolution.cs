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
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal sealed class MainModuleResolution : ModuleResolutionBase, IModuleManagement {
        private readonly ConcurrentDictionary<string, IPythonModule> _specialized = new ConcurrentDictionary<string, IPythonModule>();
        private IRunningDocumentTable _rdt;

        private IEnumerable<string> _userPaths = Enumerable.Empty<string>();

        public MainModuleResolution(string root, IServiceContainer services)
            : base(root, services) { }

        internal IBuiltinsPythonModule CreateBuiltinsModule() {
            if (BuiltinsModule == null) {
                // Initialize built-in
                var moduleName = BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);

                StubCache = _services.GetService<IStubCache>();
                var modulePath = StubCache.GetCacheFilePath(_interpreter.Configuration.InterpreterPath);

                var b = new BuiltinsPythonModule(moduleName, modulePath, _services);
                BuiltinsModule = b;
                Modules[BuiltinModuleName] = new ModuleRef(b);
            }
            return BuiltinsModule;
        }

        internal async Task InitializeAsync(CancellationToken cancellationToken = default) {
            await ReloadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        protected override IPythonModule CreateModule(string name) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                _log?.Log(TraceEventType.Verbose, "Import not found: ", name);
                return null;
            }

            if (moduleImport.ModulePath != null) {
                var module = GetRdt().GetDocument(new Uri(moduleImport.ModulePath));
                if (module != null) {
                    GetRdt().LockDocument(module.Uri);
                    return module;
                }
            }

            // If there is a stub, make sure it is loaded and attached
            // First check stub next to the module.
            if (!TryCreateModuleStub(name, moduleImport.ModulePath, out var stub)) {
                // If nothing found, try Typeshed.
                stub = _interpreter.TypeshedResolution.GetOrLoadModule(moduleImport.IsBuiltin ? name : moduleImport.FullName);
            }

            // If stub is created and its path equals to module, return that stub as module
            if (stub != null && stub.FilePath.PathEquals(moduleImport.ModulePath)) {
                return stub;
            }

            if (moduleImport.IsBuiltin) {
                _log?.Log(TraceEventType.Verbose, "Create built-in compiled (scraped) module: ", name, Configuration.InterpreterPath);
                return new CompiledBuiltinPythonModule(name, stub, _services);
            }

            if (moduleImport.IsCompiled) {
                _log?.Log(TraceEventType.Verbose, "Create compiled (scraped): ", moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath);
                return new CompiledPythonModule(moduleImport.FullName, ModuleType.Compiled, moduleImport.ModulePath, stub, _services);
            }

            _log?.Log(TraceEventType.Verbose, "Import: ", moduleImport.FullName, moduleImport.ModulePath);
            // Module inside workspace == user code.

            var mco = new ModuleCreationOptions {
                ModuleName = moduleImport.FullName,
                ModuleType = moduleImport.IsLibrary ? ModuleType.Library : ModuleType.User,
                FilePath = moduleImport.ModulePath,
                Stub = stub
            };

            return GetRdt().AddModule(mco);
        }

        private async Task<IReadOnlyList<PythonLibraryPath>> GetInterpreterSearchPathsAsync(CancellationToken cancellationToken = default) {
            if (!_fs.FileExists(Configuration.InterpreterPath)) {
                _log?.Log(TraceEventType.Warning, "Interpreter does not exist:", Configuration.InterpreterPath);
                _ui?.ShowMessageAsync(Resources.InterpreterNotFound, TraceEventType.Error);
                return Array.Empty<PythonLibraryPath>();
            }

            _log?.Log(TraceEventType.Information, "GetCurrentSearchPaths", Configuration.InterpreterPath);
            try {
                var fs = _services.GetService<IFileSystem>();
                var ps = _services.GetService<IProcessServices>();
                var paths = await PythonLibraryPath.GetSearchPathsAsync(Configuration, fs, ps, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return paths.ToArray();
            } catch (InvalidOperationException ex) {
                _log?.Log(TraceEventType.Warning, "Exception getting search paths", ex);
                _ui?.ShowMessageAsync(Resources.ExceptionGettingSearchPaths, TraceEventType.Error);
                return Array.Empty<PythonLibraryPath>();
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
            var analyzer = _services.GetService<IPythonAnalyzer>();
            await analyzer.GetAnalysisAsync(BuiltinsModule, -1, cancellationToken);

            Check.InvalidOperation(!(BuiltinsModule.Analysis is EmptyAnalysis), "After await");

            // Add built-in module names
            var builtinModuleNamesMember = BuiltinsModule.GetAnyMember("__builtin_module_names__");
            var value = (builtinModuleNamesMember as IVariable)?.Value ?? builtinModuleNamesMember;
            if (value.TryGetConstant<string>(out var s)) {
                var builtinModuleNames = s.Split(',').Select(n => n.Trim());
                PathResolver.SetBuiltins(builtinModuleNames);
            }
        }

        internal async Task ReloadSearchPaths(CancellationToken cancellationToken = default) {
            var ps = _services.GetService<IProcessServices>();

            var paths = await GetInterpreterSearchPathsAsync(cancellationToken);
            var (interpreterPaths, userPaths) = PythonLibraryPath.ClassifyPaths(Root, _fs, paths, Configuration.SearchPaths);

            InterpreterPaths = interpreterPaths.Select(p => p.Path);
            _userPaths = userPaths.Select(p => p.Path);

            _log?.Log(TraceEventType.Information, "Interpreter search paths:");
            foreach (var s in InterpreterPaths) {
                _log?.Log(TraceEventType.Information, $"    {s}");
            }

            _log?.Log(TraceEventType.Information, "User search paths:");
            foreach (var s in _userPaths) {
                _log?.Log(TraceEventType.Information, $"    {s}");
            }
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default) {
            foreach (var uri in Modules
                .Where(m => m.Value.Value?.Name != BuiltinModuleName)
                .Select(m => m.Value.Value?.Uri)
                .ExcludeDefault()) {
                GetRdt()?.UnlockDocument(uri);
            }

            // Preserve builtins, they don't need to be reloaded since interpreter does not change.
            var builtins = Modules[BuiltinModuleName];
            Modules.Clear();
            Modules[BuiltinModuleName] = builtins;

            PathResolver = new PathResolver(_interpreter.LanguageVersion);

            var addedRoots = new HashSet<string>();
            addedRoots.UnionWith(PathResolver.SetRoot(Root));

            await ReloadSearchPaths(cancellationToken);

            addedRoots.UnionWith(PathResolver.SetInterpreterSearchPaths(InterpreterPaths));
            addedRoots.UnionWith(PathResolver.SetUserSearchPaths(_userPaths));
            ReloadModulePaths(addedRoots);
        }

        // For tests
        internal void AddUnimportableModule(string moduleName)
            => Modules[moduleName] = new ModuleRef(new SentinelModule(moduleName, _services));

        private bool TryCreateModuleStub(string name, string modulePath, out IPythonModule module) {
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

        private IRunningDocumentTable GetRdt()
            => _rdt ?? (_rdt = _services.GetService<IRunningDocumentTable>());
    }
}
