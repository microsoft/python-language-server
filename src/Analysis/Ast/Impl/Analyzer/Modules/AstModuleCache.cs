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

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal sealed class AstModuleCache : IModuleCache {
        private readonly IPythonInterpreter _interpreter;
        private readonly IFileSystem _fs;
        private readonly bool _skipCache;
        private bool _loggedBadDbPath;

        private ILogger Log => _interpreter.Log;
        private string ModuleCachePath => _interpreter.Configuration.ModuleCachePath;

        public AstModuleCache(IPythonInterpreter interpreter) {
            _interpreter = interpreter;
            _fs = interpreter.Services.GetService<IFileSystem>();
            _skipCache = string.IsNullOrEmpty(_interpreter.Configuration.ModuleCachePath);
        }


        public IPythonModule ImportFromCache(string name, IPythonInterpreter interpreter) {
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

            return AstStubPythonModule.FromTypeStub(interpreter, cache, name);
        }

        public string GetCacheFilePath(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !PathEqualityComparer.IsValidPath(ModuleCachePath)) {
                if (!_loggedBadDbPath) {
                    _loggedBadDbPath = true;
                    _interpreter.Log?.Log(TraceEventType.Warning, $"Invalid module cache path: {ModuleCachePath}");
                }
                return null;
            }

            var name = PathUtils.GetFileName(filePath);
            if (!PathEqualityComparer.IsValidPath(name)) {
                Log?.Log(TraceEventType.Warning, $"Invalid cache name: {name}");
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

            var path = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            }

            var fileIsOkay = false;
            try {
                var cacheTime = _fs.GetLastWriteTimeUtc(path);
                var sourceTime = _fs.GetLastWriteTimeUtc(filePath);
                if (sourceTime <= cacheTime) {
                    var assemblyTime = _fs.GetLastWriteTimeUtc(GetType().Assembly.Location);
                    if (assemblyTime <= cacheTime) {
                        fileIsOkay = true;
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
            }

            if (fileIsOkay) {
                try {
                    return _fs.ReadAllText(filePath);
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }

            Log?.Log(TraceEventType.Verbose, "Invalidate cached module", path);
            _fs.DeleteFileWithRetries(path);
            return string.Empty;
        }

        public void WriteCachedModule(string filePath, string code) {
            var cache = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(cache)) {
                return;
            }

            Log?.Log(TraceEventType.Verbose, "Write cached module: ", cache);
            try {
                _fs.WriteAllText(cache, code);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                PathUtils.DeleteFile(cache);
            }
        }
    }
}
