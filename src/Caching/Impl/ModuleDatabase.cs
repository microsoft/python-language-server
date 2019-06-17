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

        private readonly object _lock = new object();
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
        public ModuleStorageState TryGetModuleData(string moduleName, string filePath, out IPythonModule module) {
            module = null;
            lock (_lock) {
                // We don't cache results here. Module resolution service decides when to call in here
                // and it is responsible of overall management of the loaded Python modules.
                for (var retries = 50; retries > 0; --retries) {
                    try {
                        // TODO: make combined db rather than per module?
                        var dbPath = FindDatabaseFile(moduleName, filePath, out var qualifiedName);
                        if (string.IsNullOrEmpty(dbPath)) {
                            return ModuleStorageState.DoesNotExist;
                        }

                        using (var db = new LiteDatabase(dbPath)) {
                            if (!db.CollectionExists("modules")) {
                                return ModuleStorageState.Corrupted;
                            }

                            var modules = db.GetCollection<ModuleModel>("modules");
                            var model = modules.Find(m => m.Name == qualifiedName).FirstOrDefault();
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
        }

        public void StoreModuleAnalysis(IDocumentAnalysis analysis) {
            lock (_lock) {
                var model = ModuleModel.FromAnalysis(analysis);
                Exception ex = null;
                for (var retries = 50; retries > 0; --retries) {
                    try {
                        if (!_fs.DirectoryExists(_databaseFolder)) {
                            _fs.CreateDirectory(_databaseFolder);
                        }

                        using (var db = new LiteDatabase(Path.Combine(_databaseFolder, $"{model.Name}.db"))) {
                            var modules = db.GetCollection<ModuleModel>("modules");
                            modules.Upsert(model);
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
        }

        private string FindDatabaseFile(string moduleName, string filePath, out string qualifiedName) {
            var interpreter = _services.GetService<IPythonInterpreter>();
            qualifiedName = ModuleQualifiedName.CalculateQualifiedName(moduleName, filePath, interpreter, _fs);
            if(string.IsNullOrEmpty(qualifiedName)) {
                return null;
            }

            // Try module name as is.
            var dbPath = Path.Combine(_databaseFolder, $"{qualifiedName}.db");
            if(_fs.FileExists(dbPath)) {
                return dbPath;
            }
            
            // TODO: resolving to a different version can be an option
            // Try with the major.minor Python version.
            var pythonVersion = interpreter.Configuration.Version;

            dbPath = Path.Combine(_databaseFolder, $"{qualifiedName}({pythonVersion.Major}.{pythonVersion.Minor}).db");
            if (_fs.FileExists(dbPath)) {
                return dbPath;
            }

            // Try with just the major Python version.
            dbPath = Path.Combine(_databaseFolder, $"{qualifiedName}({pythonVersion.Major}).db");
            return _fs.FileExists(dbPath) ? dbPath : null;
        }
    }
}
