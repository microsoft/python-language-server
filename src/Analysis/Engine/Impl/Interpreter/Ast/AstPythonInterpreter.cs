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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    internal class AstPythonInterpreter : IPythonInterpreter2, IModuleContext, ICanFindModuleMembers {
        private readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>() {
            { BuiltinTypeId.NoneType, new AstPythonBuiltinType("NoneType", BuiltinTypeId.NoneType) },
            { BuiltinTypeId.Unknown, new AstPythonBuiltinType("Unknown", BuiltinTypeId.Unknown) }
        };
        private readonly string _workspaceRoot;
        private readonly AstPythonInterpreterFactory _factory;
        private readonly object _userSearchPathsLock = new object();

        private PythonAnalyzer _analyzer;
        private AstScrapedPythonModule _builtinModule;
        private IReadOnlyList<string> _builtinModuleNames;

        private IReadOnlyList<string> _userSearchPaths;
        private IReadOnlyDictionary<string, string> _userSearchPathPackages;
        private HashSet<string> _userSearchPathImported = new HashSet<string>();

        public AstPythonInterpreter(AstPythonInterpreterFactory factory, string workspaceRoot, AnalysisLogWriter log = null) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _factory.ImportableModulesChanged += Factory_ImportableModulesChanged;

            _workspaceRoot = workspaceRoot;
            Log = log;
        }

        public void Dispose() {
            _factory.ImportableModulesChanged -= Factory_ImportableModulesChanged;
        }


        public event EventHandler ModuleNamesChanged;
        public IModuleContext CreateModuleContext() => this;
        public IPythonInterpreterFactory Factory => _factory;
        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(_factory.Configuration.Version.ToLanguageVersion());
        public AnalysisLogWriter Log { get; }

        private void Factory_ImportableModulesChanged(object sender, EventArgs e) {
            _modules.Clear();
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

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
                                _builtinTypes[BuiltinTypeId.Unknown] = res = new AstPythonType("<unknown>");
                            }
                        } else {
                            res = new AstPythonType(name);
                        }
                    }
                    _builtinTypes[id] = res;
                }
            }
            return res;
        }

        private async Task<IReadOnlyDictionary<string, string>> GetUserSearchPathPackagesAsync(CancellationToken cancellationToken) {
            Log?.Log(TraceLevel.Verbose, "GetUserSearchPathPackagesAsync");
            var ussp = _userSearchPathPackages;
            if (ussp == null) {
                IReadOnlyList<string> usp;
                lock (_userSearchPathsLock) {
                    usp = _userSearchPaths;
                    ussp = _userSearchPathPackages;
                }
                if (ussp != null || usp == null || !usp.Any()) {
                    return ussp;
                }

                ussp = await _factory.ModuleResolution.GetPackagesFromSearchPathsAsync(_analyzer.GetSearchPaths(), cancellationToken);
                lock (_userSearchPathsLock) {
                    if (_userSearchPathPackages == null) {
                        _userSearchPathPackages = ussp;
                    } else {
                        ussp = _userSearchPathPackages;
                    }
                }
            }
            Log?.Log(TraceLevel.Verbose, "GetPackagesFromSearchPathsAsync", _userSearchPathPackages.Keys.Cast<object>().ToArray());
            return ussp;
        }

        private async Task<ModulePath?> FindModuleInUserSearchPathAsync(string name, CancellationToken cancellationToken) {
            var searchPaths = _userSearchPaths;
            if (searchPaths == null || searchPaths.Count == 0) {
                return null;
            }

            var packages = await GetUserSearchPathPackagesAsync(cancellationToken);

            var i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);
            string searchPath;

            ModulePath mp;
            Func<string, bool> isPackage = _factory.ModuleResolution.IsPackage;
            if (firstBit.EndsWithOrdinal("-stubs", ignoreCase: true)) {
                isPackage = Directory.Exists;
            }

            var requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_factory.Configuration.Version);
            if (packages != null && packages.TryGetValue(firstBit, out searchPath) && !string.IsNullOrEmpty(searchPath)) {
                if (ModulePath.FromBasePathAndName_NoThrow(searchPath, name, isPackage, null, requireInitPy, out mp, out _, out _, out _)) {
                    ImportedFromUserSearchPath(name);
                    return mp;
                }
            }

            foreach (var sp in searchPaths.MaybeEnumerate()) {
                if (ModulePath.FromBasePathAndName_NoThrow(sp, name, isPackage, null, requireInitPy, out mp, out _, out _, out _)) {
                    ImportedFromUserSearchPath(name);
                    return mp;
                }
            }

            return null;
        }

        private void ImportedFromUserSearchPath(string name) {
            lock (_userSearchPathsLock) {
                if (_userSearchPathImported == null) {
                    _userSearchPathImported = new HashSet<string>();
                }
                _userSearchPathImported.Add(name);
            }
        }

        public IList<string> GetModuleNames() {
            var ussp = GetUserSearchPathPackagesAsync(CancellationToken.None).WaitAndUnwrapExceptions();
            var ssp = _factory.ModuleResolution.GetImportableModulesAsync(CancellationToken.None).WaitAndUnwrapExceptions();
            var bmn = _builtinModuleNames;
            if (bmn == null && _builtinModule != null) {
                var builtinModules = (_builtinModule as IBuiltinPythonModule)?.GetAnyMember("__builtin_module_names__");
                bmn = _builtinModuleNames = (builtinModules as AstPythonStringLiteral)?.Value?.Split(',') ?? Array.Empty<string>();
            }

            IEnumerable<string> names = null;
            if (ussp != null) {
                names = ussp.Keys;
            }
            if (ssp != null) {
                names = names?.Union(ssp.Keys) ?? ssp.Keys;
            }
            if (bmn != null) {
                names = names?.Union(bmn) ?? bmn;
            }

            return names.MaybeEnumerate().ToArray();
        }

        public async Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token) {
            if (name == BuiltinModuleName) {
                if (_builtinModule == null) {
                    _modules[BuiltinModuleName] = _builtinModule = new AstBuiltinsPythonModule(_factory.LanguageVersion);
                    _builtinModuleNames = null;
                }
                return _builtinModule;
            }

            Debug.Assert(_analyzer != null);

            var ctxt = new TryImportModuleContext {
                Interpreter = this,
                ModuleCache = _modules,
                BuiltinModule = _builtinModule,
                FindModuleInUserSearchPathAsync = FindModuleInUserSearchPathAsync,
                TypeStubPaths = _analyzer.Limits.UseTypeStubPackages ? _analyzer.GetTypeStubPaths() : null,
                MergeTypeStubPackages = !_analyzer.Limits.UseTypeStubPackagesExclusively
            };

            for (var retries = 5; retries > 0; --retries) {
                // The call should be cancelled by the cancellation token, but since we
                // are blocking here we wait for slightly longer. Timeouts are handled
                // gracefully by TryImportModuleAsync(), so we want those to trigger if
                // possible, but if all else fails then we'll abort and treat it as an
                // error.
                // (And if we've got a debugger attached, don't time out at all.)
                TryImportModuleResult result;
                try {
                    result = await _factory.ModuleResolution.TryImportModuleAsync(name, ctxt, token);
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
            try {
                var impTask = ImportModuleAsync(name, token);
                if (impTask.Wait(10000)) {
                    return impTask.Result;
                }
            } catch (AggregateException ex) {
                throw ex.InnerException ?? ex;
            }
            return null;
        }

        public void Initialize(PythonAnalyzer analyzer) {
            if (_analyzer != null) {
                return;
            }

            _analyzer = analyzer;
            if (analyzer != null) {
                lock (_userSearchPathsLock) {
                    _userSearchPaths = analyzer.GetSearchPaths();
                }
                analyzer.SearchPathsChanged += Analyzer_SearchPathsChanged;
                var bm = analyzer.BuiltinModule;
                if (!string.IsNullOrEmpty(bm?.Name)) {
                    _modules[analyzer.BuiltinModule.Name] = analyzer.BuiltinModule.InterpreterModule;
                }
            }
        }

        private void Analyzer_SearchPathsChanged(object sender, EventArgs e) {
            lock (_userSearchPathsLock) {
                // Remove imported modules from search paths so we will import them again.
                foreach (var name in _userSearchPathImported.MaybeEnumerate().ToArray()) {
                    _modules.TryRemove(name, out var mod);
                }
                _userSearchPathImported = null;
                _userSearchPathPackages = null;
                _userSearchPaths = _analyzer.GetSearchPaths();
            }
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<string> GetModulesNamed(string name) {
            var usp = GetUserSearchPathPackagesAsync(CancellationToken.None).WaitAndUnwrapExceptions();
            var ssp = _factory.ModuleResolution.GetImportableModulesAsync(CancellationToken.None).WaitAndUnwrapExceptions();

            var dotName = "." + name;

            IEnumerable<string> res;
            if (usp == null) {
                if (ssp == null) {
                    res = Enumerable.Empty<string>();
                } else {
                    res = ssp.Keys;
                }
            } else if (ssp == null) {
                res = usp.Keys;
            } else {
                res = usp.Keys.Union(ssp.Keys);
            }

            return res.Where(m => m == name || m.EndsWithOrdinal(dotName));
        }

        public IEnumerable<string> GetModulesContainingName(string name) {
            // TODO: Some efficient way of searching every module

            yield break;
        }
    }
}
