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
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class StubCache : IStubCache {
        private const int _stubCacheFormatVersion = 1;

        private readonly IFileSystem _fs;
        private readonly ILogger _log;
        private readonly ICacheFolderService _cfs;

        public StubCache(IServiceContainer services) {
            _fs = services.GetService<IFileSystem>();
            _log = services.GetService<ILogger>();

            _cfs = services.GetService<ICacheFolderService>();
            StubCacheFolder = Path.Combine(_cfs.CacheFolder, $"stubs.v{_stubCacheFormatVersion}");
        }

        public string StubCacheFolder { get; }

        public string GetCacheFilePath(string filePath) {
            var name = PathUtils.GetFileName(filePath);
            if (!PathEqualityComparer.IsValidPath(name)) {
                _log?.Log(TraceEventType.Warning, $"Invalid cache name: {name}");
                return null;
            }
            try {
                var candidate = Path.ChangeExtension(Path.Combine(StubCacheFolder, name), ".pyi");
                if (_fs.FileExists(candidate)) {
                    return candidate;
                }
            } catch (ArgumentException) {
                return null;
            }

            var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            if (_fs.StringComparison == StringComparison.OrdinalIgnoreCase) {
                dir = dir.ToLowerInvariant();
            }

            var dirHash = _cfs.GetFileNameFromContent(dir);
            var stubFile = Path.Combine(StubCacheFolder, Path.Combine(dirHash, name));
            return Path.ChangeExtension(stubFile, ".pyi");
        }

        public string ReadCachedModule(string filePath) {
            var cachePath = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(cachePath)) {
                return string.Empty;
            }

            var cachedFileExists = false;
            var cachedFileOlderThanSource = false;
            Exception exception = null;

            try {
                cachedFileExists = _fs.FileExists(cachePath);
                if (cachedFileExists) {
                    // Source path is fake for scraped/compiled modules.
                    // The time will be very old, which is good.
                    var sourceTime = _fs.GetLastWriteTimeUtc(filePath);
                    var cacheTime = _fs.GetLastWriteTimeUtc(cachePath);

                    cachedFileOlderThanSource = cacheTime < sourceTime;
                    if (!cachedFileOlderThanSource) {
                        return _fs.ReadTextWithRetry(cachePath);
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                exception = ex;
            }

            string reason;
            if (!cachedFileExists) {
                reason = "Cached file does not exist";
            } else if (cachedFileOlderThanSource) {
                reason = $"Cached file is older than the source {filePath}.";
            } else {
                reason = $"Exception during cache file check {exception.Message}.";
            }

            _log?.Log(TraceEventType.Verbose, $"Invalidated cached module {cachePath}. Reason: {reason}");
            _fs.DeleteFileWithRetries(cachePath);
            return string.Empty;
        }

        public void WriteCachedModule(string filePath, string code) {
            var cache = GetCacheFilePath(filePath);
            if (!string.IsNullOrEmpty(cache)) {
                _log?.Log(TraceEventType.Verbose, "Writing cached module: ", cache);
                // Don't block analysis on cache writes.
                CacheWritingTask = Task.Run(() => _fs.WriteTextWithRetry(cache, code));
                CacheWritingTask.DoNotWait();
            }
        }

        // For tests synchronization
        internal Task CacheWritingTask { get; private set; } = Task.CompletedTask;
    }
}
