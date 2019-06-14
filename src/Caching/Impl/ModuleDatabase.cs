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
using LiteDB;
using Microsoft.Python.Analysis.Caching.Models;
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
        private readonly string _databaseFolder;

        public ModuleDatabase(IServiceContainer services) {
            _services = services;
            _log = services.GetService<ILogger>();
            var cfs = services.GetService<ICacheFolderService>();
            _databaseFolder = Path.Combine(cfs.CacheFolder, $"analysis.v{_databaseFormatVersion}");
        }

        public ModuleStorageState TryGetModuleData(string qualifiedName, out IPythonModule module) {
            module = null;
            lock (_lock) {
                // We don't cache results here. Module resolution service decides when to call in here
                // and it is responsible of overall management of the loaded Python modules.
                try {
                    // TODO: make combined db rather than per module.
                    using (var db = new LiteDatabase(Path.Combine(_databaseFolder, $"{qualifiedName}.db"))) {
                        if (!db.CollectionExists("modules")) {
                            return ModuleStorageState.Corrupted;
                        }

                        var modules = db.GetCollection<ModuleModel>("modules");
                        var model = modules.Find(m => m.Name == qualifiedName).FirstOrDefault();
                        if (model == null) {
                            return ModuleStorageState.DoesNotExist;
                        }

                        module = new PythonDbModule(model, _services);
                        return ModuleStorageState.Complete;
                    }
                } catch (IOException) { } catch (UnauthorizedAccessException) { }

                return ModuleStorageState.DoesNotExist;
            }
        }

        public void StoreModuleAnalysis(IDocumentAnalysis analysis) {
            lock (_lock) {
                var model = ModuleModel.FromAnalysis(analysis);
                try {
                    if(!Directory.Exists(_databaseFolder)) {
                        Directory.CreateDirectory(_databaseFolder);
                    }
                    using (var db = new LiteDatabase(Path.Combine(_databaseFolder, $"{model.Name}.db"))) {
                        var modules = db.GetCollection<ModuleModel>("modules");
                        modules.Upsert(model);
                    }
                } catch (Exception ex) {
                    _log?.Log(System.Diagnostics.TraceEventType.Warning, $"Unable to write analysis of {model.Name} to database. Exception {ex.Message}");
                    if(ex.IsCriticalException()) {
                        throw;
                    }
                } 
            }
        }
    }
}
