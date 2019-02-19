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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Modules {
    internal sealed class ModuleCache : IModuleCache {
        private readonly IServiceContainer _services;
        private readonly IPythonInterpreter _interpreter;
        private readonly IFileSystem _fs;
        private readonly ILogger _log;
        private readonly bool _skipCache;
        private bool _loggedBadDbPath;

        private string ModuleCachePath => _interpreter.Configuration.DatabasePath;

        public ModuleCache(IPythonInterpreter interpreter, IServiceContainer services) {
            _interpreter = interpreter;
            _services = services;
            _fs = services.GetService<IFileSystem>();
            _log = services.GetService<ILogger>();
            _skipCache = string.IsNullOrEmpty(_interpreter.Configuration.DatabasePath);
            SearchPathCachePath = Path.Combine(_interpreter.Configuration.DatabasePath, "database.path");
        }

        public string SearchPathCachePath { get; }

        public async Task<IDocument> ImportFromCacheAsync(string name, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(ModuleCachePath)) {
                return null;
            }

            var cache = GetCacheFilePath("python.{0}.pyi".FormatInvariant(name));
            if (!_fs.FileExists(cache)) {
                cache = GetCacheFilePath("python._{0}.pyi".FormatInvariant(name));
                if (!_fs.FileExists(cache)) {
                    cache = GetCacheFilePath("{0}.pyi".FormatInvariant(name));
                    if (!_fs.FileExists(cache)) {
                        return null;
                    }
                }
            }

            var rdt = _services.GetService<IRunningDocumentTable>();
            var mco = new ModuleCreationOptions {
                ModuleName = name,
                ModuleType = ModuleType.Compiled,
                FilePath = cache
            };
            var module = rdt.AddModule(mco);

            await module.LoadAndAnalyzeAsync(cancellationToken);
            return module;
        }

        public string GetCacheFilePath(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !PathEqualityComparer.IsValidPath(ModuleCachePath)) {
                if (!_loggedBadDbPath) {
                    _loggedBadDbPath = true;
                    _log?.Log(TraceEventType.Warning, $"Invalid module cache path: {ModuleCachePath}");
                }
                return null;
            }

            var name = PathUtils.GetFileName(filePath);
            if (!PathEqualityComparer.IsValidPath(name)) {
                _log?.Log(TraceEventType.Warning, $"Invalid cache name: {name}");
                return null;
            }
            try {
                var candidate = Path.ChangeExtension(Path.Combine(ModuleCachePath, name), ".pyi");
                if (_fs.FileExists(candidate)) {
                    return candidate;
                }
            } catch (ArgumentException) {
                return null;
            }

            var hash = SHA256.Create();
            var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            if (_fs.StringComparison == StringComparison.OrdinalIgnoreCase) {
                dir = dir.ToLowerInvariant();
            }

            var dirHash = Convert.ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(dir)))
                .Replace('/', '_').Replace('+', '-');

            return Path.ChangeExtension(Path.Combine(
                ModuleCachePath,
                Path.Combine(dirHash, name)
            ), ".pyi");
        }

        public string ReadCachedModule(string filePath) {
            if (_skipCache) {
                return string.Empty;
            }

            var cachePath = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(cachePath)) {
                return string.Empty;
            }

            var cachedFileExists = false;
            var cachedFileOlderThanAssembly = false;
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
                        var assemblyTime = _fs.GetLastWriteTimeUtc(GetType().Assembly.Location);
                        if (assemblyTime > cacheTime) {
                            cachedFileOlderThanAssembly = true;
                        } else {
                            return _fs.ReadAllText(cachePath);
                        }
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                exception = ex;
            }

            var reason = "Unknown";
            if (!cachedFileExists) {
                reason = "Cached file does not exist";
            } else if (cachedFileOlderThanAssembly) {
                reason = "Cached file is older than the assembly.";
            } else if (cachedFileOlderThanSource) {
                reason = $"Cached file is older than the source {filePath}.";
            } else {
                reason = $"Exception during cache file check {exception.Message}.";
            }

            _log?.Log(TraceEventType.Verbose, $"Invalidate cached module {cachePath}. Reason: {reason}");
            _fs.DeleteFileWithRetries(cachePath);
            return string.Empty;
        }

        public void WriteCachedModule(string filePath, string code) {
            var cache = GetCacheFilePath(filePath);
            if (!string.IsNullOrEmpty(cache)) {
                _log?.Log(TraceEventType.Verbose, "Write cached module: ", cache);
                // Don't block analysis on cache writes.
                CacheWritingTask = Task.Run(() => _fs.WriteTextWithRetry(cache, code));
                CacheWritingTask.DoNotWait();
            }
        }

        // For tests synchronization
        internal Task CacheWritingTask { get; private set; } = Task.CompletedTask;
    }
}
