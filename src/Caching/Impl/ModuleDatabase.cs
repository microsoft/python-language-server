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
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching.IO;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class ModuleDatabase : IModuleDatabaseService {
        private readonly object _modulesLock = new object();
        private readonly Dictionary<string, PythonDbModule> _modulesCache
            = new Dictionary<string, PythonDbModule>();

        private readonly ConcurrentDictionary<string, ModuleModel> _modelsCache
            = new ConcurrentDictionary<string, ModuleModel>();

        private readonly ConcurrentDictionary<AnalysisModuleKey, bool> _searchResults
            = new ConcurrentDictionary<AnalysisModuleKey, bool>();

        private readonly IServiceContainer _services;
        private readonly ILogger _log;
        private readonly IFileSystem _fs;
        private readonly AnalysisCachingLevel _defaultCachingLevel;
        private readonly CacheWriter _cacheWriter;
        private AnalysisCachingLevel? _cachingLevel;

        public ModuleDatabase(IServiceManager sm, string cacheFolder = null) {
            _services = sm;
            _log = _services.GetService<ILogger>();
            _fs = _services.GetService<IFileSystem>();
            _defaultCachingLevel = AnalysisCachingLevel.Library;
            var cfs = _services.GetService<ICacheFolderService>();
            CacheFolder = cacheFolder ?? Path.Combine(cfs.CacheFolder, $"{CacheFolderBaseName}{DatabaseFormatVersion}");
            _cacheWriter = new CacheWriter(_fs, _log, CacheFolder);
            sm.AddService(this);
        }

        public string CacheFolderBaseName => "analysis.v";
        public int DatabaseFormatVersion => 5;
        public string CacheFolder { get; }

        /// <summary>
        /// Creates global scope from module persistent state.
        /// Global scope is then can be used to construct module analysis.
        /// </summary>
        public IPythonModule RestoreModule(string moduleName, string modulePath, ModuleType moduleType) {
            if (GetCachingLevel() == AnalysisCachingLevel.None) {
                return null;
            }
            return FindModuleModelByPath(moduleName, modulePath, moduleType, out var model)
                ? RestoreModule(model) : null;
        }

        /// <summary>
        /// Determines if module analysis exists in the storage.
        /// </summary>
        public bool ModuleExistsInStorage(string name, string filePath, ModuleType moduleType) {
            if (GetCachingLevel() == AnalysisCachingLevel.None) {
                return false;
            }

            var key = new AnalysisModuleKey(name, filePath);
            if (_searchResults.TryGetValue(key, out var result)) {
                return result;
            }

            for (var retries = 50; retries > 0; --retries) {
                try {
                    var dbPath = FindDatabaseFile(name, filePath, moduleType);
                    _searchResults[key] = result = !string.IsNullOrEmpty(dbPath);
                    return result;
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    Thread.Sleep(10);
                }
            }
            return false;
        }

        public async Task StoreModuleAnalysisAsync(IDocumentAnalysis analysis, CancellationToken cancellationToken = default) {
            var cachingLevel = GetCachingLevel();
            if (cachingLevel == AnalysisCachingLevel.None) {
                return;
            }

            var model = await Task.Run(() => ModuleModel.FromAnalysis(analysis, _services, cachingLevel), cancellationToken);
            if (model != null && !cancellationToken.IsCancellationRequested) {
                await _cacheWriter.EnqueueModel(model, cancellationToken);
            }
        }

        internal IPythonModule RestoreModule(string moduleName, string uniqueId) {
            lock (_modulesLock) {
                if (_modulesCache.TryGetValue(uniqueId, out var m)) {
                    return m;
                }
            }
            return FindModuleModelById(moduleName, uniqueId, out var model) ? RestoreModule(model) : null;
        }

        private IPythonModule RestoreModule(ModuleModel model) {
            PythonDbModule dbModule;
            lock (_modulesLock) {
                if (_modulesCache.TryGetValue(model.UniqueId, out var m)) {
                    return m;
                }
                dbModule = _modulesCache[model.UniqueId] = new PythonDbModule(model, model.FilePath, _services);
            }
            dbModule.Construct(model);
            return dbModule;
        }

        /// <summary>
        /// Locates database file based on module information. Module is identified
        /// by name, version, current Python interpreter version and/or hash of the
        /// module content (typically file sizes).
        /// </summary>
        private string FindDatabaseFile(string moduleName, string filePath, ModuleType moduleType) {
            var uniqueId = ModuleUniqueId.GetUniqueId(moduleName, filePath, moduleType, _services, GetCachingLevel());
            return string.IsNullOrEmpty(uniqueId) ? null : FindDatabaseFile(uniqueId);
        }

        private string FindDatabaseFile(string uniqueId) {
            // Try module name as is.
            var dbPath = Path.Combine(CacheFolder, $"{uniqueId}.db");
            if (_fs.FileExists(dbPath)) {
                return dbPath;
            }

            // TODO: resolving to a different version can be an option
            // Try with the major.minor Python version.
            var interpreter = _services.GetService<IPythonInterpreter>();
            var pythonVersion = interpreter.Configuration.Version;

            dbPath = Path.Combine(CacheFolder, $"{uniqueId}({pythonVersion.Major}.{pythonVersion.Minor}).db");
            if (_fs.FileExists(dbPath)) {
                return dbPath;
            }

            // Try with just the major Python version.
            dbPath = Path.Combine(CacheFolder, $"{uniqueId}({pythonVersion.Major}).db");
            return _fs.FileExists(dbPath) ? dbPath : null;
        }

        private bool FindModuleModelByPath(string moduleName, string modulePath, ModuleType moduleType, out ModuleModel model)
            => TryGetModuleModel(moduleName, FindDatabaseFile(moduleName, modulePath, moduleType), out model);

        private bool FindModuleModelById(string moduleName, string uniqueId, out ModuleModel model)
            => TryGetModuleModel(moduleName, FindDatabaseFile(uniqueId), out model);

        private bool TryGetModuleModel(string moduleName, string dbPath, out ModuleModel model) {
            model = null;

            if (string.IsNullOrEmpty(dbPath)) {
                return false;
            }

            if (_modelsCache.TryGetValue(moduleName, out model)) {
                return true;
            }

            model = WithRetries.Execute(() => {
                using (var db = new LiteDatabase(dbPath)) {
                    var modules = db.GetCollection<ModuleModel>("modules");
                    var storedModel = modules.FindOne(m => m.Name == moduleName);
                    _modelsCache[moduleName] = storedModel;
                    return storedModel;
                }
            }, $"Unable to locate database for module {moduleName}.", _log);

            return model != null;
        }

        private AnalysisCachingLevel GetCachingLevel()
            => _cachingLevel
               ?? (_cachingLevel = _services.GetService<IAnalysisOptionsProvider>()?.Options.AnalysisCachingLevel)
               ?? _defaultCachingLevel;
    }
}
