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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class ModuleDatabase : IModuleDatabaseService {
        private readonly ConcurrentDictionary<string, IDependencyProvider> _dependencies = new ConcurrentDictionary<string, IDependencyProvider>();

        private readonly IServiceContainer _services;
        private readonly ILogger _log;
        private readonly IFileSystem _fs;

        public ModuleDatabase(IServiceContainer services) {
            _services = services;
            _log = services.GetService<ILogger>();
            _fs = services.GetService<IFileSystem>();

            var cfs = services.GetService<ICacheFolderService>();
            CacheFolder = Path.Combine(cfs.CacheFolder, $"{CacheFolderBaseName}{DatabaseFormatVersion}");
        }

        public string CacheFolderBaseName => "analysis.v";
        public int DatabaseFormatVersion => 2;
        public string CacheFolder { get; }

        /// <summary>
        /// Retrieves dependencies from the module persistent state.
        /// </summary>
        /// <param name="module">Python module to restore analysis for.</param>
        /// <param name="dp">Python module dependency provider.</param>
        public bool TryRestoreDependencies(IPythonModule module, out IDependencyProvider dp) {
            dp = null;

            if (GetCachingLevel() == AnalysisCachingLevel.None || !module.ModuleType.CanBeCached()) {
                return false;
            }

            if (_dependencies.TryGetValue(module.Name, out dp)) {
                return true;
            }

            if (FindModuleModel(module.Name, module.FilePath, module.ModuleType, out var model)) {
                dp = new DependencyProvider(module, model);
                _dependencies[module.Name] = dp;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates global scope from module persistent state.
        /// Global scope is then can be used to construct module analysis.
        /// </summary>
        /// <param name="module">Python module to restore analysis for.</param>
        /// <param name="gs">Python module global scope.</param>
        public bool TryRestoreGlobalScope(IPythonModule module, out IRestoredGlobalScope gs) {
            gs = null;

            if (GetCachingLevel() == AnalysisCachingLevel.None || !module.ModuleType.CanBeCached()) {
                return false;
            }

            if (FindModuleModel(module.Name, module.FilePath, module.ModuleType, out var model)) {
                gs = new RestoredGlobalScope(model, module);
            }

            return gs != null;
        }

        /// <summary>
        /// Writes module data to the database.
        /// </summary>
        public Task StoreModuleAnalysisAsync(IDocumentAnalysis analysis, CancellationToken cancellationToken = default)
            => Task.Run(() => StoreModuleAnalysis(analysis, cancellationToken), cancellationToken);

        /// <summary>
        /// Determines if module analysis exists in the storage.
        /// </summary>
        public bool ModuleExistsInStorage(string moduleName, string filePath, ModuleType moduleType) {
            if (GetCachingLevel() == AnalysisCachingLevel.None) {
                return false;
            }

            for (var retries = 50; retries > 0; --retries) {
                try {
                    var dbPath = FindDatabaseFile(moduleName, filePath, moduleType);
                    return !string.IsNullOrEmpty(dbPath);
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    Thread.Sleep(10);
                }
            }
            return false;
        }

        private void StoreModuleAnalysis(IDocumentAnalysis analysis, CancellationToken cancellationToken = default) {
            var cachingLevel = GetCachingLevel();
            if (cachingLevel == AnalysisCachingLevel.None) {
                return;
            }

            var model = ModuleModel.FromAnalysis(analysis, _services, cachingLevel);
            if (model == null) {
                // Caching level setting does not permit this module to be persisted.
                return;
            }

            Exception ex = null;
            for (var retries = 50; retries > 0; --retries) {
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    if (!_fs.DirectoryExists(CacheFolder)) {
                        _fs.CreateDirectory(CacheFolder);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    using (var db = new LiteDatabase(Path.Combine(CacheFolder, $"{model.UniqueId}.db"))) {
                        var modules = db.GetCollection<ModuleModel>("modules");
                        modules.Upsert(model);
                        return;
                    }
                } catch (Exception ex1) when (ex1 is IOException || ex1 is UnauthorizedAccessException) {
                    ex = ex1;
                    Thread.Sleep(10);
                } catch (Exception ex2) {
                    ex = ex2;
                    break;
                }
            }

            if (ex != null) {
                _log?.Log(System.Diagnostics.TraceEventType.Warning, $"Unable to write analysis of {model.Name} to database. Exception {ex.Message}");
                if (ex.IsCriticalException()) {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Locates database file based on module information. Module is identified
        /// by name, version, current Python interpreter version and/or hash of the
        /// module content (typically file sizes).
        /// </summary>
        private string FindDatabaseFile(string moduleName, string filePath, ModuleType moduleType) {
            var interpreter = _services.GetService<IPythonInterpreter>();
            var uniqueId = ModuleUniqueId.GetUniqueId(moduleName, filePath, moduleType, _services, GetCachingLevel());
            if (string.IsNullOrEmpty(uniqueId)) {
                return null;
            }

            // Try module name as is.
            var dbPath = Path.Combine(CacheFolder, $"{uniqueId}.db");
            if (_fs.FileExists(dbPath)) {
                return dbPath;
            }

            // TODO: resolving to a different version can be an option
            // Try with the major.minor Python version.
            var pythonVersion = interpreter.Configuration.Version;

            dbPath = Path.Combine(CacheFolder, $"{uniqueId}({pythonVersion.Major}.{pythonVersion.Minor}).db");
            if (_fs.FileExists(dbPath)) {
                return dbPath;
            }

            // Try with just the major Python version.
            dbPath = Path.Combine(CacheFolder, $"{uniqueId}({pythonVersion.Major}).db");
            return _fs.FileExists(dbPath) ? dbPath : null;
        }

        private bool FindModuleModel(string moduleName, string filePath, ModuleType moduleType, out ModuleModel model) {
            model = null;

            // We don't cache results here. Module resolution service decides when to call in here
            // and it is responsible of overall management of the loaded Python modules.
            for (var retries = 50; retries > 0; --retries) {
                try {
                    // TODO: make combined db rather than per module?
                    var dbPath = FindDatabaseFile(moduleName, filePath, moduleType);
                    if (string.IsNullOrEmpty(dbPath)) {
                        return false;
                    }

                    using (var db = new LiteDatabase(dbPath)) {
                        if (!db.CollectionExists("modules")) {
                            return false;
                        }

                        var modules = db.GetCollection<ModuleModel>("modules");
                        model = modules.Find(m => m.Name == moduleName).FirstOrDefault();
                        return model != null;
                    }
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    Thread.Sleep(10);
                }
            }
            return false;
        }
        private AnalysisCachingLevel GetCachingLevel()
            => _services.GetService<IAnalysisOptionsProvider>()?.Options.AnalysisCachingLevel ?? AnalysisCachingLevel.None;

        private sealed class DependencyProvider : IDependencyProvider {
            private readonly ISet<AnalysisModuleKey> _dependencies;

            public DependencyProvider(IPythonModule module, ModuleModel model) {
                var dc = new DependencyCollector(module);
                dc.AddImports(model.Imports);
                dc.AddFromImports(model.FromImports);
                _dependencies = dc.Dependencies;
            }

            public ISet<AnalysisModuleKey> GetDependencies(PythonAst ast) => _dependencies;
        }
    }
}
