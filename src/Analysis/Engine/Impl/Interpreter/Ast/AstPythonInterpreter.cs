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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.DependencyResolution;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    internal class AstPythonInterpreter : IPythonInterpreter2, IModuleContext {
        private readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>() {
            { BuiltinTypeId.NoneType, new AstPythonType("NoneType", BuiltinTypeId.NoneType) },
            { BuiltinTypeId.Unknown, new AstPythonType("Unknown", BuiltinTypeId.Unknown) }
        };
        
        private readonly AstPythonInterpreterFactory _factory;
        private readonly object _userSearchPathsLock = new object();

        private PythonAnalyzer _analyzer;
        private AstBuiltinsPythonModule _builtinModule;
        
        public AstPythonInterpreter(AstPythonInterpreterFactory factory, bool useDefaultDatabase, AnalysisLogWriter log = null) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _factory.ImportableModulesChanged += Factory_ImportableModulesChanged;

            Log = log;

            ModuleCache = new AstModuleCache(factory.Configuration, factory.CreationOptions.DatabasePath, useDefaultDatabase, factory.CreationOptions.UseExistingCache, log);
            ModuleResolution = new AstModuleResolution(this, _modules, ModuleCache, factory.Configuration, log);
        }

        public void Dispose() {
            _factory.ImportableModulesChanged -= Factory_ImportableModulesChanged;
            if (_analyzer != null) {
                _analyzer.SearchPathsChanged -= Analyzer_SearchPathsChanged;
            }
        }

        public event EventHandler ModuleNamesChanged;
        public IModuleContext CreateModuleContext() => this;
        public IPythonInterpreterFactory Factory => _factory;
        public string BuiltinModuleName => ModuleResolution.BuiltinModuleName;
        public AnalysisLogWriter Log { get; }

        internal AstModuleResolution ModuleResolution { get; }
        internal AstModuleCache ModuleCache { get; }
        internal string InterpreterPath => _factory.Configuration.InterpreterPath;
        internal PathResolverSnapshot CurrentPathResolver => _analyzer?.CurrentPathResolver ?? new PathResolverSnapshot(_factory.LanguageVersion);

        public void AddUnimportableModule(string moduleName) {
            _modules[moduleName] = new SentinelModule(moduleName, false);
        }

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            if (id < 0 || id > BuiltinTypeIdExtensions.LastTypeId) {
                throw new KeyNotFoundException("(BuiltinTypeId)({0})".FormatInvariant((int)id));
            }

            IPythonType res;
            lock (_builtinTypes) {
                if (!_builtinTypes.TryGetValue(id, out res)) {
                    var bm = ImportModule(BuiltinModuleName) as AstBuiltinsPythonModule;
                    res = bm?.GetAnyMember("__{0}__".FormatInvariant(id)) as IPythonType;
                    if (res == null) {
                        var name = id.GetTypeName(_factory.Configuration.Version);
                        if (string.IsNullOrEmpty(name)) {
                            Debug.Assert(id == BuiltinTypeId.Unknown, $"no name for {id}");
                            if (!_builtinTypes.TryGetValue(BuiltinTypeId.Unknown, out res)) {
                                _builtinTypes[BuiltinTypeId.Unknown] = res = new AstPythonType("<unknown>", bm, null, null, BuiltinTypeId.Unknown);
                            }
                        } else {
                            res = new AstPythonType(name, bm, null, null, id);
                        }
                    }
                    _builtinTypes[id] = res;
                }
            }
            return res;
        }
        
        private Task<ModulePath?> FindModulePath(string name, CancellationToken cancellationToken) {
            var moduleImport = _analyzer.CurrentPathResolver.GetModuleImportFromModuleName(name);
            return moduleImport != null
                ? Task.FromResult<ModulePath?>(new ModulePath(moduleImport.FullName, moduleImport.ModulePath, moduleImport.RootPath))
                : Task.FromResult<ModulePath?>(null);
        }

        public IList<string> GetModuleNames() => _analyzer.CurrentPathResolver.GetAllModuleNames().ToArray();

        public async Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token) {
            if (name == BuiltinModuleName) {
                if (_builtinModule == null) {
                    _modules[BuiltinModuleName] = _builtinModule = new AstBuiltinsPythonModule(_factory.LanguageVersion);
                }
                return _builtinModule;
            }

            Debug.Assert(_analyzer != null);

            var pathResolver = _analyzer.CurrentPathResolver;
            var typeStubPaths = _analyzer.Limits.UseTypeStubPackages ? _analyzer.GetTypeStubPaths() : null;
            var mergeTypeStubPackages = !_analyzer.Limits.UseTypeStubPackagesExclusively;

            for (var retries = 5; retries > 0; --retries) {
                // The call should be cancelled by the cancellation token, but since we
                // are blocking here we wait for slightly longer. Timeouts are handled
                // gracefully by TryImportModuleAsync(), so we want those to trigger if
                // possible, but if all else fails then we'll abort and treat it as an
                // error.
                // (And if we've got a debugger attached, don't time out at all.)
                TryImportModuleResult result;
                try {
                    result = await ModuleResolution.TryImportModuleAsync(name, pathResolver, typeStubPaths, mergeTypeStubPackages, token);
                } catch (OperationCanceledException) {
                    Log.Log(TraceLevel.Error, "ImportTimeout", name);
                    Debug.Fail("Import timeout");
                    return null;
                }

                switch (result.Status) {
                    case TryImportModuleResultCode.Success:
                        return result.Module;
                    case TryImportModuleResultCode.ModuleNotFound:
                        Log?.Log(TraceLevel.Info, "ImportNotFound", name);
                        return null;
                    case TryImportModuleResultCode.NeedRetry:
                    case TryImportModuleResultCode.Timeout:
                        break;
                    case TryImportModuleResultCode.NotSupported:
                        Log?.Log(TraceLevel.Error, "ImportNotSupported", name);
                        return null;
                }
            }
            // Never succeeded, so just log the error and fail
            Log?.Log(TraceLevel.Error, "RetryImport", name);
            return null;
        }

        public IPythonModule ImportModule(string name) {
            var token = new CancellationTokenSource(5000).Token;
#if DEBUG
            token = Debugger.IsAttached ? CancellationToken.None : token;
#endif
            var impTask = ImportModuleAsync(name, token);
            return impTask.Wait(10000) ? impTask.WaitAndUnwrapExceptions() : null;
        }

        public void Initialize(PythonAnalyzer analyzer) {
            if (_analyzer != null) {
                return;
            }

            _analyzer = analyzer;
            if (analyzer != null) {
                var interpreterPaths = ModuleResolution.GetSearchPathsAsync(CancellationToken.None).WaitAndUnwrapExceptions();
                _analyzer.SetInterpreterPaths(interpreterPaths);

                analyzer.SearchPathsChanged += Analyzer_SearchPathsChanged;
                var bm = analyzer.BuiltinModule;
                if (!string.IsNullOrEmpty(bm?.Name)) {
                    _modules[analyzer.BuiltinModule.Name] = analyzer.BuiltinModule.InterpreterModule;
                }
            }
        }

        private void Factory_ImportableModulesChanged(object sender, EventArgs e) {
            _modules.Clear();
            if (_builtinModule != null) {
                _modules[BuiltinModuleName] = _builtinModule;
            }
            ModuleCache.Clear();
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Analyzer_SearchPathsChanged(object sender, EventArgs e) {
            var moduleNames = _analyzer.CurrentPathResolver.GetInterpreterModuleNames().Append(BuiltinModuleName);
            lock (_userSearchPathsLock) {
                // Remove imported modules from search paths so we will import them again.
                var modulesNamesToRemove = _modules.Keys.Except(moduleNames).ToList();
                foreach (var moduleName in modulesNamesToRemove) {
                    _modules.TryRemove(moduleName, out _);
                }
            }
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
