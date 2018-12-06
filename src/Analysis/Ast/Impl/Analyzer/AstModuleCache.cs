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
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AstModuleCache: IModuleCache {
        private readonly IPythonInterpreterFactory _factory;
        private readonly bool _skipCache;
        private bool _loggedBadDbPath;

        public AstModuleCache(IPythonInterpreterFactory factory) {
            _factory = factory;
            _skipCache = string.IsNullOrEmpty(_factory.DatabasePath);
        }

        public string DatabasePath => _factory.DatabasePath;
        public string SearchPathCachePath { get; }

        public IPythonModule ImportFromCache(string name, IPythonInterpreter interpreter) {
            if (string.IsNullOrEmpty(DatabasePath)) {
                return null;
            }

            var cache = GetCacheFilePath("python.{0}.pyi".FormatInvariant(name));
            if (!File.Exists(cache)) {
                cache = GetCacheFilePath("python._{0}.pyi".FormatInvariant(name));
                if (!File.Exists(cache)) {
                    cache = GetCacheFilePath("{0}.pyi".FormatInvariant(name));
                    if (!File.Exists(cache)) {
                        return null;
                    }
                }
            }

            return PythonModuleLoader.FromTypeStub(interpreter, cache, _factory.Configuration.Version.ToLanguageVersion(), name);
        }

        public void Clear() {
            if (!string.IsNullOrEmpty(SearchPathCachePath) && File.Exists(SearchPathCachePath)) {
                PathUtils.DeleteFile(SearchPathCachePath);
            }
        }

        public string GetCacheFilePath(string filePath) {
            if (!PathEqualityComparer.IsValidPath(DatabasePath)) {
                if (!_loggedBadDbPath) {
                    _loggedBadDbPath = true;
                    _factory.Log?.Log(TraceEventType.Warning, $"Invalid database path: {DatabasePath}");
                }
                return null;
            }

            var name = PathUtils.GetFileName(filePath);
            if (!PathEqualityComparer.IsValidPath(name)) {
                _factory.Log?.Log(TraceEventType.Warning, $"Invalid cache name: {name}");
                return null;
            }
            try {
                var candidate = Path.ChangeExtension(Path.Combine(DatabasePath, name), ".pyi");
                if (File.Exists(candidate)) {
                    return candidate;
                }
            } catch (ArgumentException) {
                return null;
            }

            var hash = SHA256.Create();
            var dir = Path.GetDirectoryName(filePath);
            if (IsWindows()) {
                dir = dir.ToLowerInvariant();
            }

            var dirHash = Convert.ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(dir)))
                .Replace('/', '_').Replace('+', '-');

            return Path.ChangeExtension(Path.Combine(
                DatabasePath,
                Path.Combine(dirHash, name)
            ), ".pyi");
        }

        private static bool IsWindows()
            => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public Stream ReadCachedModule(string filePath) {
            if (_skipCache) {
                return null;
            }

            var path = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            var file = PathUtils.OpenWithRetry(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (file == null || _factory.UseDefaultDatabase) {
                return file;
            }

            bool fileIsOkay = false;
            try {
                var cacheTime = File.GetLastWriteTimeUtc(path);
                var sourceTime = File.GetLastWriteTimeUtc(filePath);
                if (sourceTime <= cacheTime) {
                    var assemblyTime = File.GetLastWriteTimeUtc(typeof(AstPythonInterpreterFactory).Assembly.Location);
                    if (assemblyTime <= cacheTime) {
                        fileIsOkay = true;
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
            }

            if (fileIsOkay) {
                return file;
            }

            file.Dispose();
            file = null;

            _factory.Log?.Log(TraceEventType.Verbose, "Invalidate cached module", path);

            PathUtils.DeleteFile(path);
            return null;
        }

        public void WriteCachedModule(string filePath, Stream code) {
            if (_factory.UseDefaultDatabase) {
                return;
            }

            var cache = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(cache)) {
                return;
            }

            _factory.Log?.Log(TraceEventType.Verbose, "Write cached module: ", cache);

            try {
                using (var stream = PathUtils.OpenWithRetry(cache, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                    if (stream != null) {
                        code.CopyTo(stream);
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                PathUtils.DeleteFile(cache);
            }
        }
    }
}
