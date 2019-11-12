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
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Threading;

namespace Microsoft.Python.Analysis.Caching.IO {
    internal sealed class CacheWriter : IDisposable {
        private readonly IFileSystem _fs;
        private readonly ILogger _log;
        private readonly string _cacheFolder;
        private readonly TaskQueue _taskQueue;
        private readonly IPythonAnalyzer _analyzer;

        public CacheWriter(IPythonAnalyzer analyzer, IFileSystem fs, ILogger log, string cacheFolder) {
            _fs = fs;
            _log = log;
            _cacheFolder = cacheFolder;
            _taskQueue = new TaskQueue(Math.Max(1, Environment.ProcessorCount / 4));

            _analyzer = analyzer;
            _analyzer.AnalysisComplete += OnAnalysisComplete;
        }

        private void OnAnalysisComplete(object sender, AnalysisCompleteEventArgs e) => _taskQueue.ProcessQueue();

        public void Dispose() {
            _analyzer.AnalysisComplete -= OnAnalysisComplete;
            _taskQueue.Dispose();
        }

        public Task EnqueueModel(ModuleModel model, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<bool>();
            _taskQueue.Enqueue(() => {
                try {
                    Write(model, cancellationToken);
                    tcs.SetResult(true);
                } catch (OperationCanceledException) {
                    tcs.TrySetCanceled();
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    tcs.TrySetException(ex);
                }
            }, immediate: false);
            return tcs.Task;
        }

        private void Write(ModuleModel model, CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            WithRetries.Execute(() => {
                if (!_fs.DirectoryExists(_cacheFolder)) {
                    _fs.CreateDirectory(_cacheFolder);
                }
                return true;
            }, $"Unable to create directory {_cacheFolder} for modules cache.", _log);

            WithRetries.Execute(() => {
                if (cancellationToken.IsCancellationRequested) {
                    return false;
                }
                using (var db = new LiteDatabase(Path.Combine(_cacheFolder, $"{model.UniqueId}.db"))) {
                    var modules = db.GetCollection<ModuleModel>("modules");
                    modules.Upsert(model);
                    modules.EnsureIndex(x => x.Name);
                }
                return true;
            }, $"Unable to write analysis of {model.Name} to database.", _log);
        }
    }
}
