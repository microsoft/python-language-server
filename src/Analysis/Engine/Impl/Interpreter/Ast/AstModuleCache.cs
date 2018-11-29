// Python Tools for Visual Studio
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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    internal sealed class AstModuleCache {
        private readonly InterpreterConfiguration _configuration;
        private readonly bool _skipCache;
        private readonly bool _useDefaultDatabase;
        private readonly AnalysisLogWriter _log;
        private bool _loggedBadDbPath;

        public AstModuleCache(InterpreterConfiguration configuration, string databasePath, bool useDefaultDatabase, bool useExistingCache, AnalysisLogWriter log) {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            DatabasePath = databasePath;
            _useDefaultDatabase = useDefaultDatabase;
            _log = log;

            if (_useDefaultDatabase) {
                var dbPath = Path.Combine("DefaultDB", $"v{_configuration.Version.Major}", "python.pyi");
                if (InstallPath.TryGetFile(dbPath, out string biPath)) {
                    DatabasePath = Path.GetDirectoryName(biPath);
                } else {
                    _skipCache = true;
                }
            } else {
                SearchPathCachePath = Path.Combine(DatabasePath, "database.path");
            }
            _skipCache = !useExistingCache;
        }

        public string DatabasePath { get; }
        public string SearchPathCachePath { get; }

        public IPythonModule ImportFromCache(string name, TryImportModuleContext context) {
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

            return PythonModuleLoader.FromTypeStub(context.Interpreter, cache, _configuration.Version.ToLanguageVersion(), name);
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
                    _log?.Log(TraceLevel.Warning, "InvalidDatabasePath", DatabasePath);
                }
                return null;
            }

            var name = PathUtils.GetFileName(filePath);
            if (!PathEqualityComparer.IsValidPath(name)) {
                _log?.Log(TraceLevel.Warning, "InvalidCacheName", name);
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

            if (file == null || _useDefaultDatabase) {
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

            _log?.Log(TraceLevel.Info, "InvalidateCachedModule", path);

            PathUtils.DeleteFile(path);
            return null;
        }

        internal void WriteCachedModule(string filePath, Stream code) {
            if (_useDefaultDatabase) {
                return;
            }

            var cache = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(cache)) {
                return;
            }

            _log?.Log(TraceLevel.Info, "WriteCachedModule", cache);

            try {
                using (var stream = PathUtils.OpenWithRetry(cache, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                    if (stream == null) {
                        return;
                    }

                    code.CopyTo(stream);
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                try {
                    File.Delete(cache);
                } catch (Exception) {
                }
            }
        }
    }
}
