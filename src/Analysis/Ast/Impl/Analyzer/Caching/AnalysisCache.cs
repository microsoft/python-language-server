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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
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

        public AnalysisCache(IServiceContainer services, string cacheRootFolder = null) {
            var platform = services.GetService<IOSPlatform>();

            cacheRootFolder = cacheRootFolder ?? GetCacheFolder(platform);
            _analysisRootFolder = Path.Combine(cacheRootFolder, $"analysis.v{_analysisCacheFormatVersion}");
            _stubsRootFolder = Path.Combine(cacheRootFolder, $"stubs.v{_stubCacheFormatVersion}");

            _log = services.GetService<ILogger>();
            _fs = services.GetService<IFileSystem>();
        }

        public Task WriteAnalysisAsync(IDocument document, CancellationToken cancellationToken) {
            if (document.Stub != null || document.ModuleType != ModuleType.Library
                || string.IsNullOrEmpty(document.Content) || document.GlobalScope == null) {
                return Task.CompletedTask;
            }

            var aw = new AnalysisWriter(document);
            var md = aw.WriteModuleData();
            if (string.IsNullOrEmpty(md)) {
                return Task.CompletedTask;
            }

            var filePath = GetAnalysisCacheFilePath(document.Name, document.Content);
            return Task.Run(() => _fs.WriteTextWithRetry(filePath, md), cancellationToken);
        }

        public string GetMemberValueTypeName(string fullyQualifiedName) {
            throw new NotImplementedException();
        }


        public string GetStubCacheFilePath(string moduleName, string content)
            => GetCacheFilePath(_stubsRootFolder, moduleName, content);

        private string GetAnalysisCacheFilePath(string moduleName, string content)
            => GetCacheFilePath(_analysisRootFolder, moduleName, content);

        private string GetCacheFilePath(string root, string moduleName, string content) {
            // Folder for all module versions and variants
            // {root}/module_name/content_hash.pyi
            var folder = Path.Combine(root, moduleName);

            var filePath = Path.Combine(root, folder, $"{FileNameFromContent(content)}.pyi");
            if (_fs.StringComparison == StringComparison.Ordinal) {
                filePath = filePath.ToLowerInvariant();
            }
            return filePath;
        }

        private static string FileNameFromContent(string content) {
            // File name depends on the content so we can distinguish between different versions.
            var hash = SHA256.Create();
            return Convert
                .ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(content)))
                .Replace('/', '_').Replace('+', '-');
        }

        private string GetCacheFolder(IOSPlatform platform) {
            if (platform.IsWindows) {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Python Language Server");
            }
            if (platform.IsMac) {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "microsoft.python.ls");
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
    }
}
