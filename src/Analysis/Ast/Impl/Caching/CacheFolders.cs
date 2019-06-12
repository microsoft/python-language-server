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
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.OS;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class CacheFolderService: ICacheFolderService {
        public CacheFolderService(IServiceContainer services, string cacheRootFolder) {
            CacheFolder = cacheRootFolder ?? GetCacheFolder(services);
        }

        public string CacheFolder { get; }

        public string GetFileNameFromContent(string content) {
            // File name depends on the content so we can distinguish between different versions.
            using (var hash = SHA256.Create()) {
                return Convert
                    .ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(content)))
                    .Replace('/', '_').Replace('+', '-');
            }
        }

        private static string GetCacheFolder(IServiceContainer services) {
            var platform = services.GetService<IOSPlatform>();
            var logger = services.GetService<ILogger>();

            // Default. Not ideal on all platforms, but used as a fall back.
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var plsSubfolder = $"Microsoft{Path.DirectorySeparatorChar}Python Language Server";
            var defaultCachePath = Path.Combine(localAppData, plsSubfolder);
            
            string cachePath = null;
            try {
                const string homeVarName = "HOME";
                var homeFolderPath = Environment.GetEnvironmentVariable(homeVarName);

                if(platform.IsWindows) {
                    cachePath = defaultCachePath;
                }

                if (platform.IsMac) {
                    if (CheckVariableSet(homeVarName, homeFolderPath, logger) 
                        && CheckPathRooted(homeVarName, homeFolderPath, logger)
                        && !string.IsNullOrWhiteSpace(homeFolderPath)) {
                        cachePath = Path.Combine(homeFolderPath, "Library/Caches", plsSubfolder);
                    } else {
                        logger?.Log(TraceEventType.Warning, Resources.EnvVariablePathNotRooted.FormatInvariant(homeVarName));
                    }
                }

                if (platform.IsLinux) {
                    const string xdgCacheVarName = "XDG_CACHE_HOME";
                    var xdgCacheHomePath = Environment.GetEnvironmentVariable(xdgCacheVarName);

                    if (!string.IsNullOrWhiteSpace(xdgCacheHomePath)
                        && CheckPathRooted(xdgCacheVarName, xdgCacheHomePath, logger)) {
                        cachePath = Path.Combine(xdgCacheVarName, plsSubfolder);
                    } else if (!string.IsNullOrWhiteSpace(homeFolderPath)
                               && CheckVariableSet(homeVarName, homeFolderPath, logger)
                               && CheckPathRooted(homeVarName, homeFolderPath, logger)) {
                        cachePath = Path.Combine(homeFolderPath, ".cache", plsSubfolder);
                    } else {
                        logger?.Log(TraceEventType.Warning, Resources.EnvVariablePathNotRooted.FormatInvariant(homeVarName));
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                logger?.Log(TraceEventType.Warning, Resources.UnableToDetermineCachePathException.FormatInvariant(ex.Message, defaultCachePath));
            }

            // Default is same as Windows. Not ideal on all platforms, but it is a fallback anyway.
            if (cachePath == null) {
                logger?.Log(TraceEventType.Warning, Resources.UnableToDetermineCachePath.FormatInvariant(defaultCachePath));
                cachePath = defaultCachePath;
            }

            logger?.Log(TraceEventType.Information, Resources.AnalysisCachePath.FormatInvariant(cachePath));
            return cachePath;
        }

        private static bool CheckPathRooted(string varName, string path, ILogger logger) {
            if (!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path)) {
                return true;
            }

            logger?.Log(TraceEventType.Warning, Resources.EnvVariablePathNotRooted.FormatInvariant(varName));
            return false;
        }

        private static bool CheckVariableSet(string varName, string value, ILogger logger) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return true;
            }

            logger?.Log(TraceEventType.Warning, Resources.EnvVariableNotSet.FormatInvariant(varName));
            return false;
        }
    }
}
