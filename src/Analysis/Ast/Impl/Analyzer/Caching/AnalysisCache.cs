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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Threading;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class AnalysisCache : IAnalysisCache, IDisposable {
        private const int _analysisCacheFormatVersion = 1;

        private readonly string _analysisRootFolder;
        private readonly IFileSystem _fs;
        private readonly AnalysisReader _reader;

        public AnalysisCache(IServiceContainer services, string cacheRootFolder = null) {
            var platform = services.GetService<IOSPlatform>();

            cacheRootFolder = cacheRootFolder ?? CacheFolders.GetCacheFolder(platform);
            _analysisRootFolder = Path.Combine(cacheRootFolder, $"analysis.v{_analysisCacheFormatVersion}");

            _fs = services.GetService<IFileSystem>();
            _reader = new AnalysisReader(_analysisRootFolder, _fs);
            Task.Run(() => WriterTask()).DoNotWait();
        }

        public Task WriteAnalysisAsync(IDocument document, IScope globalScope) {
            if (globalScope == null
                || document.Stub != null
                || string.IsNullOrEmpty(document.Content)) {
                return Task.CompletedTask;
            }

            var newData = ModuleData.FromModule(document, globalScope);
            if (newData.Classes.Count == 0 && newData.Functions.Count == 0) {
                return Task.CompletedTask;
            }

            var newDataString = newData.Serialize();
            if (!string.IsNullOrEmpty(newDataString)) {
                var filePath = CacheFolders.GetAnalysisCacheFilePath(_analysisRootFolder, document.Name, document.Content, _fs);
                // Write if file does not exist or something has changed in the module data.
                var write = !_fs.FileExists(filePath);

                if (!write) {
                    var existingDataString = _reader.GetModuleData(document.Name, document.Content)?.Serialize();
                    write = newDataString != existingDataString;
                }

                if (write) {
                    _reader.SetModuleData(document.Name, newData);
                    var tcs = new TaskCompletionSource<bool>();
                    Enqueue(() => {
                        _fs.WriteAllTextEx(filePath, newDataString);
                        tcs.SetResult(true);
                    });
                    return tcs.Task;
                }
            }
            return Task.CompletedTask;
        }

        public CacheSearchResult GetReturnType(IPythonType ft, out string returnType) {
            returnType = null;
            GetItemData(ft, out var name, out var md);
            if (md == null) {
                return CacheSearchResult.NoCache;
            }
            return name != null && md.Functions.TryGetValue(name, out returnType)
                ? CacheSearchResult.Found : CacheSearchResult.NotFound;
        }

        public void Dispose() => _cts.Cancel();

        private void GetItemData(IPythonType t, out string name, out ModuleData md) {
            name = null;
            md = null;
            if (t.DeclaringModule is IDocument doc) {
                md = _reader.GetModuleData(doc.Name, doc.Content);
                if (md != null) {
                    name = t.GetFullyQualifiedName();
                }
            }
        }

        private readonly PriorityProducerConsumer<Action> _ppc = new PriorityProducerConsumer<Action>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private void Enqueue(Action a) => _ppc.Produce(a);

        private void WriterTask() {
            while (!_cts.Token.IsCancellationRequested) {
                try {
                    var t = _ppc.ConsumeAsync();
                    if (!t.Wait(Timeout.Infinite, _cts.Token)) {
                        break;
                    }
                    t.Result();
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }
    }
}
