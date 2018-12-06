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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AstPythonInterpreter : IPythonInterpreter {
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>() {
            { BuiltinTypeId.NoneType, new AstPythonType("NoneType", BuiltinTypeId.NoneType) },
            { BuiltinTypeId.Unknown, new AstPythonType("Unknown", BuiltinTypeId.Unknown) }
        };
        
        private readonly AstPythonInterpreterFactory _factory;
        private readonly object _userSearchPathsLock = new object();

        private IPythonAnalyzer _analyzer;
        private AstBuiltinsPythonModule _builtinModule;
        
        public AstPythonInterpreter(AstPythonInterpreterFactory factory) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _factory.ImportableModulesChanged += Factory_ImportableModulesChanged;

            ModuleResolution = new AstModuleResolution(this, factory);
        }

        public void Dispose() {
            _factory.ImportableModulesChanged -= Factory_ImportableModulesChanged;
            if (_analyzer != null) {
                _analyzer.SearchPathsChanged -= Analyzer_SearchPathsChanged;
            }
        }
        /// <summary>
        /// Interpreter configuration.
        /// </summary>
        public InterpreterConfiguration Configuration { get; }

        public event EventHandler ModuleNamesChanged;
        public IPythonInterpreterFactory Factory => _factory;
        public ILogger Log => _factory.Log;
        public PythonLanguageVersion LanguageVersion => _factory.LanguageVersion;
        public string InterpreterPath => _factory.Configuration.InterpreterPath;
        public string LibraryPath => _factory.Configuration.LibraryPath;
        /// <summary>
        /// Module resolution service.
        /// </summary>
        public IModuleResolution ModuleResolution { get; }

        /// <summary>
        /// Gets a well known built-in type such as int, list, dict, etc...
        /// </summary>
        /// <param name="id">The built-in type to get</param>
        /// <returns>An IPythonType representing the type.</returns>
        /// <exception cref="KeyNotFoundException">
        /// The requested type cannot be resolved by this interpreter.
        /// </exception>
        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            if (id < 0 || id > BuiltinTypeIdExtensions.LastTypeId) {
                throw new KeyNotFoundException("(BuiltinTypeId)({0})".FormatInvariant((int)id));
            }

            IPythonType res;
            lock (_builtinTypes) {
                if (!_builtinTypes.TryGetValue(id, out res)) {
                    var bm = ModuleResolution.BuiltinModule;
                    res = bm.GetAnyMember("__{0}__".FormatInvariant(id)) as IPythonType;
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

        public void Initialize(IPythonAnalyzer analyzer) {
            if (_analyzer != null) {
                return;
            }

            _analyzer = analyzer;
            if (analyzer != null) {
                var searchPaths = ModuleResolution.GetSearchPathsAsync(CancellationToken.None).WaitAndUnwrapExceptions();
                _analyzer.SetSearchPaths(searchPaths);

                analyzer.SearchPathsChanged += Analyzer_SearchPathsChanged;
                var bm = ModuleResolution.BuiltinModule;
                if (!string.IsNullOrEmpty(bm?.Name)) {
                    _modules[bm.Name] = bm.InterpreterModule;
                }
            }
        }

        private void Factory_ImportableModulesChanged(object sender, EventArgs e) {
            _modules.Clear();
            if (_builtinModule != null) {
                _modules[ModuleResolution.BuiltinModuleName] = _builtinModule;
            }
            ModuleCache.Clear();
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Analyzer_SearchPathsChanged(object sender, EventArgs e) {
            var moduleNames = ModuleResolution.CurrentPathResolver.GetInterpreterModuleNames().Append(BuiltinModuleName);
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
