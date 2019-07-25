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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Caching {
    public sealed class ModuleDatabase : IModuleDatabaseService {
        private const int _databaseFormatVersion = 1;

        private readonly IServiceContainer _services;
        private readonly ILogger _log;
        private readonly IFileSystem _fs;
        private readonly string _databaseFolder;

        public ModuleDatabase(IServiceContainer services) {
            _services = services;
            _log = services.GetService<ILogger>();
            _fs = services.GetService<IFileSystem>();
            var cfs = services.GetService<ICacheFolderService>();
            _databaseFolder = Path.Combine(cfs.CacheFolder, $"analysis.v{_databaseFormatVersion}");
        }

        /// <summary>
        /// Retrieves module representation from module index database
        /// or null if module does not exist. 
        /// </summary>
        /// <param name="moduleName">Module name. If the name is not qualified
        /// the module will ge resolved against active Python version.</param>
        /// <param name="filePath">Module file path.</param>
        /// <param name="module">Python module.</param>
        /// <returns>Module storage state</returns>
        public ModuleStorageState TryCreateModule(string moduleName, string filePath, out IPythonModule module) {
            module = null;
            // We don't cache results here. Module resolution service decides when to call in here
            // and it is responsible of overall management of the loaded Python modules.
            for (var retries = 50; retries > 0; --retries) {
                try {
                    // TODO: make combined db rather than per module?
                    var dbPath = FindDatabaseFile(moduleName, filePath);
                    if (string.IsNullOrEmpty(dbPath)) {
                        return ModuleStorageState.DoesNotExist;
                    }

                    using (var db = new LiteDatabase(dbPath)) {
                        if (!db.CollectionExists("modules")) {
                            return ModuleStorageState.Corrupted;
                        }

                        var modules = db.GetCollection<ModuleModel>("modules");
                        var model = modules.Find(m => m.Name == moduleName).FirstOrDefault();
                        if (model == null) {
                            return ModuleStorageState.DoesNotExist;
                        }

                        module = new PythonDbModule(model, filePath, _services);
                        return ModuleStorageState.Complete;
                    }
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    Thread.Sleep(10);
                }
            }
            return ModuleStorageState.DoesNotExist;
        }

        /// <summary>
        /// Writes module data to the database.
        /// </summary>
        public Task StoreModuleAnalysisAsync(IDocumentAnalysis analysis, CancellationToken cancellationToken = default)
            => Task.Run(() => StoreModuleAnalysis(analysis, cancellationToken));

        /// <summary>
        /// Determines if module analysis exists in the storage.
        /// </summary>
        public bool ModuleExistsInStorage(string moduleName, string filePath) {
            for (var retries = 50; retries > 0; --retries) {
                try {
                    var dbPath = FindDatabaseFile(moduleName, filePath);
                    return !string.IsNullOrEmpty(dbPath);
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    Thread.Sleep(10);
                }
            }
            return false;
        }

        private void StoreModuleAnalysis(IDocumentAnalysis analysis, CancellationToken cancellationToken = default) {
            var model = ModuleModel.FromAnalysis(analysis, _services);
            Exception ex = null;
            for (var retries = 50; retries > 0; --retries) {
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    if (!_fs.DirectoryExists(_databaseFolder)) {
                        _fs.CreateDirectory(_databaseFolder);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    using (var db = new LiteDatabase(Path.Combine(_databaseFolder, $"{model.UniqueId}.db"))) {
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
        private string FindDatabaseFile(string moduleName, string filePath) {
            var interpreter = _services.GetService<IPythonInterpreter>();
            var uniqueId = ModuleUniqueId.GetUniqieId(moduleName, filePath, ModuleType.Specialized, _services);
            if (string.IsNullOrEmpty(uniqueId)) {
                return null;
            }

            // Try module name as is.
            var dbPath = Path.Combine(_databaseFolder, $"{uniqueId}.db");
            if (_fs.FileExists(dbPath)) {
                return dbPath;
            }

            // TODO: resolving to a different version can be an option
            // Try with the major.minor Python version.
            var pythonVersion = interpreter.Configuration.Version;

            dbPath = Path.Combine(_databaseFolder, $"{uniqueId}({pythonVersion.Major}.{pythonVersion.Minor}).db");
            if (_fs.FileExists(dbPath)) {
                return dbPath;
            }

            // Try with just the major Python version.
            dbPath = Path.Combine(_databaseFolder, $"{uniqueId}({pythonVersion.Major}).db");
            return _fs.FileExists(dbPath) ? dbPath : null;
        }
    }
}
