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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.OS;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class AnalysisCache : IAnalysisCache {
        private const int _analysisCacheFormatVersion = 1;
        private const int _stubCacheFormatVersion = 1;

        private readonly string _stubsRootFolder;
        private readonly string _analysisRootFolder;
        private readonly ILogger _log;
        private readonly IFileSystem _fs;
        private readonly AnalysisReader _reader;

        public AnalysisCache(IServiceContainer services, string cacheRootFolder = null) {
            var platform = services.GetService<IOSPlatform>();

            cacheRootFolder = cacheRootFolder ?? CacheFolders.GetCacheFolder(platform);
            _analysisRootFolder = Path.Combine(cacheRootFolder, $"analysis.v{_analysisCacheFormatVersion}");
            _stubsRootFolder = Path.Combine(cacheRootFolder, $"stubs.v{_stubCacheFormatVersion}");

            _log = services.GetService<ILogger>();
            _fs = services.GetService<IFileSystem>();
            _reader = new AnalysisReader(_analysisRootFolder, _fs);
        }

        public Task WriteAnalysisAsync(IDocument document, CancellationToken cancellationToken = default) {
            if (document.Stub != null || document.ModuleType != ModuleType.Library
                || string.IsNullOrEmpty(document.Content) || document.GlobalScope == null) {
                return Task.CompletedTask;
            }

            var aw = new AnalysisWriter(document);
            var md = aw.SerializeModuleData();
            if (string.IsNullOrEmpty(md)) {
                return Task.CompletedTask;
            }

            var filePath = CacheFolders.GetAnalysisCacheFilePath(_analysisRootFolder, document.Name, document.Content, _fs);
            return Task.Run(() => _fs.WriteTextWithRetry(filePath, md), cancellationToken);
        }

        public string GetReturnType(IPythonFunctionType ft) {
            GetItemData(ft, out var name, out var md);
            return name != null ? (md.Functions.TryGetValue(name, out var v) ? v : null) : null;
        }

        public string GetReturnType(IPythonPropertyType pt) {
            GetItemData(pt, out var name, out var md);
            return name != null ? (md.Functions.TryGetValue(name, out var v) ? v : null) : null;
        }

        public IClassData GetClassData(IPythonClassType cls) {
            GetItemData(cls, out var name, out var md);
            return name != null ? (md.Classes.TryGetValue(name, out var v) ? v : null) : null;
        }

        public string GetStubCacheFilePath(string moduleName, string content)
            => CacheFolders.GetCacheFilePath(_stubsRootFolder, moduleName, content, _fs);

        private void GetItemData(IPythonType t, out string name, out ModuleData md) {
            name = null;
            md = null;
            if (t.DeclaringModule is IDocument doc) {
                md = _reader.GetModuleData(doc.Name, doc.Content);
                name = t.GetFullyQualifiedName();
            }
        }
    }
}
