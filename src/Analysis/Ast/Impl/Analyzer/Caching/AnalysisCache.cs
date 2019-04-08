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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class AnalysisCache: IAnalysisCache {
        private const int _analysisCacheFormatVersion = 1;
        private const int _stubCacheFormatVersion = 1;

        private readonly string _cacheRootFolder;
        private readonly StubsCache _stubsCache;
        private readonly string _analysisRootFolder;
        private readonly ILogger _log;
        private readonly IFileSystem _fs;

        public AnalysisCache(string cacheRootFolder, IServiceContainer services) {
            _cacheRootFolder = cacheRootFolder;
            _analysisRootFolder = Path.Combine(_cacheRootFolder, $"analysis.v{_analysisCacheFormatVersion}");

            var stubsRootFolder = Path.Combine(_cacheRootFolder, $"stubs.v{_stubCacheFormatVersion}");
            _stubsCache = new StubsCache(stubsRootFolder, services);

            _log = services.GetService<ILogger>();
            _fs = services.GetService<IFileSystem>();
        }

        public string GetStubCacheFilePath(string moduleName, string modulePath, string stubContent)
            => _stubsCache.GetStubCacheFilePath(moduleName, modulePath, stubContent);

        public Task SaveAnalysisAsync(IPythonModule module, CancellationToken cancellationToken) {
            if (module.Stub != null || module.ModuleType != ModuleType.Library) {
                return Task.CompletedTask;
            }

            var sb = new StringBuilder();
            WriteScope(sb, 0, null);
        }

        public IMember GetFunctionReturnValue(IPythonFunctionType ft) {
            throw new NotImplementedException();
        }

    }
}
