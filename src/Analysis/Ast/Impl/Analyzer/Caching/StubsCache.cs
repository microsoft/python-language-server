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
using System.Security.Cryptography;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class StubsCache {
        private readonly string _stubsRootFolder;
        private readonly ILogger _log;
        private readonly IFileSystem _fs;

        public StubsCache(string stubsRootFolder, IServiceContainer services) {
            _stubsRootFolder = stubsRootFolder;
            _log = services.GetService<ILogger>();
            _fs = services.GetService<IFileSystem>();
        }

        public string GetStubCacheFilePath(string moduleName, string modulePath, string stubContent) {
            // {root}/module_name/content_hash.pyi
            // To content we add # python_version, original_path
            var name = PathUtils.GetFileName(modulePath);
            if (!PathEqualityComparer.IsValidPath(name)) {
                _log?.Log(TraceEventType.Warning, $"Invalid cache name: {name}");
                return null;
            }

            // Folder for all module versions and variants
            var folder = Path.Combine(_stubsRootFolder, moduleName);

            // File name depends on the content so we can distinguish between different versions.
            var hash = SHA256.Create();
            var fileName = Convert
                .ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(stubContent)))
                .Replace('/', '_').Replace('+', '-');

            var filePath = Path.Combine(folder, $"{fileName}.pyi");
            if (_fs.StringComparison == StringComparison.OrdinalIgnoreCase) {
                filePath = filePath.ToLowerInvariant();
            }
            return filePath;
        }
    }
}
