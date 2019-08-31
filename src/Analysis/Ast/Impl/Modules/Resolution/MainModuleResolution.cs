﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal sealed class MainModuleResolution : ModuleResolutionBase, IModuleManagement {
        private readonly ConcurrentDictionary<string, IPythonModule> _specialized = new ConcurrentDictionary<string, IPythonModule>();
        private readonly IUIService _ui;
        private IModuleDatabaseService _dbService;
        private IRunningDocumentTable _rdt;

        private ImmutableArray<string> _userConfiguredPaths;

        public MainModuleResolution(string root, IServiceContainer services, ImmutableArray<string> userConfiguredPaths)
            : base(root, services) {
            _ui = services.GetService<IUIService>();
            _userConfiguredPaths = userConfiguredPaths;
        }

        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(Interpreter.LanguageVersion);
        public ImmutableArray<PythonLibraryPath> LibraryPaths { get; private set; } = ImmutableArray<PythonLibraryPath>.Empty;

        public IBuiltinsPythonModule BuiltinsModule { get; private set; }

        protected override IPythonModule CreateModule(string name) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport == null) {
                Log?.Log(TraceEventType.Verbose, "Import not found: ", name);
                return null;
            }

            if (moduleImport.ModulePath != null) {
                var module = GetRdt().GetDocument(new Uri(moduleImport.ModulePath));
                if (module != null) {
                    GetRdt().LockDocument(module.Uri);
                    return module;
                }
            }

            var dbs = GetDbService();
            moduleImport.IsPersistent = dbs != null && dbs.ModuleExistsInStorage(name, moduleImport.ModulePath);

            IPythonModule stub = null;
            if (!moduleImport.IsPersistent) {
                // If there is a stub, make sure it is loaded and attached
                // First check stub next to the module.
                if (!TryCreateModuleStub(name, moduleImport.ModulePath, out stub)) {
                    // If nothing found, try Typeshed.
                    stub = Interpreter.TypeshedResolution.GetOrLoadModule(moduleImport.IsBuiltin ? name : moduleImport.FullName);
                }

                // If stub is created and its path equals to module, return that stub as module
                if (stub != null && stub.FilePath.PathEquals(moduleImport.ModulePath)) {
                    return stub;
                }
            }

            if (moduleImport.IsBuiltin) {
                Log?.Log(TraceEventType.Verbose, "Create built-in compiled (scraped) module: ", name, Configuration.InterpreterPath);
                return new CompiledBuiltinPythonModule(name, stub, moduleImport.IsPersistent, Services);
            }

            if (moduleImport.IsCompiled) {
                Log?.Log(TraceEventType.Verbose, "Create compiled (scraped): ", moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath);
                return new CompiledPythonModule(moduleImport.FullName, ModuleType.Compiled, moduleImport.ModulePath, stub, moduleImport.IsPersistent, Services);
            }

            Log?.Log(TraceEventType.Verbose, "Import: ", moduleImport.FullName, moduleImport.ModulePath);
            // Module inside workspace == user code.

            var mco = new ModuleCreationOptions {
                ModuleName = moduleImport.FullName,
                ModuleType = moduleImport.IsLibrary ? ModuleType.Library : ModuleType.User,
                FilePath = moduleImport.ModulePath,
                Stub = stub,
                IsPersistent = moduleImport.IsPersistent
            };

            return GetRdt().AddModule(mco);
        }

        private async Task<ImmutableArray<PythonLibraryPath>> GetInterpreterSearchPathsAsync(CancellationToken cancellationToken = default) {
            if (!FileSystem.FileExists(Configuration.InterpreterPath)) {
                Log?.Log(TraceEventType.Warning, "Interpreter does not exist:", Configuration.InterpreterPath);
                _ui?.ShowMessageAsync(Resources.InterpreterNotFound, TraceEventType.Error);
                return ImmutableArray<PythonLibraryPath>.Empty;
            }

            Log?.Log(TraceEventType.Information, "GetCurrentSearchPaths", Configuration.InterpreterPath);
            try {
                var fs = Services.GetService<IFileSystem>();
                var ps = Services.GetService<IProcessServices>();
                return await PythonLibraryPath.GetSearchPathsAsync(Configuration, fs, ps, cancellationToken);
            } catch (InvalidOperationException ex) {
                Log?.Log(TraceEventType.Warning, "Exception getting search paths", ex);
                _ui?.ShowMessageAsync(Resources.ExceptionGettingSearchPaths, TraceEventType.Error);
                return ImmutableArray<PythonLibraryPath>.Empty;
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
        /// <param name="replaceExisting">Replace existing loaded module, if any.</param>
        /// <returns>Specialized module.</returns>
        public IPythonModule SpecializeModule(string name, Func<string, IPythonModule> specializationConstructor, bool replaceExisting = false) {
            var import = CurrentPathResolver.GetModuleImportFromModuleName(name);
            var module = specializationConstructor(import?.ModulePath);
            _specialized[name] = module;

            if (replaceExisting) {
                Modules.TryRemove(name, out _);
            }
            return module;
        }

        /// <summary>
        /// Returns specialized module, if any.
        /// </summary>
        public IPythonModule GetSpecializedModule(string fullName, bool allowCreation = false, string modulePath = null)
            => _specialized.TryGetValue(fullName, out var module) ? module : null;

        /// <summary>
        /// Determines of module is specialized or exists in the database.
        /// </summary>
        public bool IsSpecializedModule(string fullName, string modulePath = null)
            => _specialized.ContainsKey(fullName);

        internal async Task AddBuiltinTypesToPathResolverAsync(CancellationToken cancellationToken = default) {
            var analyzer = Services.GetService<IPythonAnalyzer>();
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

        public async Task ReloadAsync(CancellationToken cancellationToken = default) {
            foreach (var uri in Modules
                .Where(m => m.Value.Value?.Name != BuiltinModuleName)
                .Select(m => m.Value.Value?.Uri)
                .ExcludeDefault()) {
                GetRdt()?.UnlockDocument(uri);
            }

            // Preserve builtins, they don't need to be reloaded since interpreter does not change.
            var builtinsIsCreated = Modules.TryGetValue(BuiltinModuleName, out var builtinsRef);
            Modules.Clear();

            await ReloadSearchPaths(cancellationToken);

            PathResolver = new PathResolver(Interpreter.LanguageVersion, Root, InterpreterPaths, UserPaths);

            var addedRoots = new HashSet<string> { Root };
            addedRoots.UnionWith(InterpreterPaths);
            addedRoots.UnionWith(UserPaths);
            ReloadModulePaths(addedRoots);

            if (!builtinsIsCreated) {
                var builtinsModule = CreateBuiltinsModule(Services, Interpreter, StubCache);
                BuiltinsModule = builtinsModule;
                builtinsRef = new ModuleRef(builtinsModule);
            }

            Modules[BuiltinModuleName] = builtinsRef;
            await AddBuiltinTypesToPathResolverAsync(cancellationToken);
        }

        private static IBuiltinsPythonModule CreateBuiltinsModule(IServiceContainer services, IPythonInterpreter interpreter, IStubCache stubCache) {
            var moduleName = BuiltinTypeId.Unknown.GetModuleName(interpreter.LanguageVersion);
            var modulePath = stubCache.GetCacheFilePath(interpreter.Configuration.InterpreterPath);
            return new BuiltinsPythonModule(moduleName, modulePath, services);
        }

        private async Task ReloadSearchPaths(CancellationToken cancellationToken = default) {
            LibraryPaths = await GetInterpreterSearchPathsAsync(cancellationToken);
            var (interpreterPaths, userPaths) = PythonLibraryPath.ClassifyPaths(Root, FileSystem, LibraryPaths, _userConfiguredPaths);

            InterpreterPaths = interpreterPaths.Select(p => p.Path);
            UserPaths = userPaths.Select(p => p.Path);

            if (Log != null) {
                Log.Log(TraceEventType.Information, "Interpreter search paths:");
                foreach (var s in InterpreterPaths) {
                    Log.Log(TraceEventType.Information, $"    {s}");
                }

                Log.Log(TraceEventType.Information, "User search paths:");
                foreach (var s in UserPaths) {
                    Log.Log(TraceEventType.Information, $"    {s}");
                }
            }
        }

        public bool SetUserConfiguredPaths(ImmutableArray<string> paths) {
            if (paths.SequentiallyEquals(_userConfiguredPaths)) {
                return false;
            }

            _userConfiguredPaths = paths;
            return true;
        }

        public bool TryAddModulePath(in string path, in long fileSize, in bool allowNonRooted, out string fullModuleName)
            => PathResolver.TryAddModulePath(path, fileSize, allowNonRooted, out fullModuleName);

        // For tests
        internal void AddUnimportableModule(string moduleName)
            => Modules[moduleName] = new ModuleRef(new SentinelModule(moduleName, Services));

        private bool TryCreateModuleStub(string name, string modulePath, out IPythonModule module) {
            // First check stub next to the module.
            if (!string.IsNullOrEmpty(modulePath)) {
                var pyiPath = Path.ChangeExtension(modulePath, "pyi");
                if (FileSystem.FileExists(pyiPath)) {
                    module = new StubPythonModule(name, pyiPath, false, Services);
                    return true;
                }
            }

            // Try location of stubs that are in a separate folder next to the package.
            var stubPath = CurrentPathResolver.GetPossibleModuleStubPaths(name).FirstOrDefault(p => FileSystem.FileExists(p));
            module = !string.IsNullOrEmpty(stubPath) ? new StubPythonModule(name, stubPath, false, Services) : null;
            return module != null;
        }

        private IRunningDocumentTable GetRdt()
            => _rdt ?? (_rdt = Services.GetService<IRunningDocumentTable>());

        private IModuleDatabaseService GetDbService()
            => _dbService ?? (_dbService = Services.GetService<IModuleDatabaseService>());
    }
}
