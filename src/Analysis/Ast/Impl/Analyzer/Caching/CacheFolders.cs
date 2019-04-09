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
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal static class CacheFolders {
        public static string GetCacheFilePath(string root, string moduleName, string content, IFileSystem fs) {
            // Folder for all module versions and variants
            // {root}/module_name/content_hash.pyi
            var folder = Path.Combine(root, moduleName);

            var filePath = Path.Combine(root, folder, $"{FileNameFromContent(content)}.pyi");
            if (fs.StringComparison == StringComparison.Ordinal) {
                filePath = filePath.ToLowerInvariant();
            }
            return filePath;
        }

        public static string GetCacheFolder(IOSPlatform platform) {
            if (platform.IsWindows) {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Python Language Server");
            }

            if (platform.IsMac) {
                var macCachesFolder = Path.GetFullPath("~/Library/Caches");
                return Path.Combine(macCachesFolder, "Microsoft/Python.Language.Server");
            }

            var linuxCachesFolder = Path.GetFullPath("~/.cache");
            return Path.Combine(linuxCachesFolder, "Microsoft/Python.Language.Server");
        }

        public static string FileNameFromContent(string content) {
            // File name depends on the content so we can distinguish between different versions.
            var hash = SHA256.Create();
            return Convert
                .ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(content)))
                .Replace('/', '_').Replace('+', '-');
        }

        public static string GetAnalysisCacheFilePath(string analysisRootFolder, string moduleName, string content, IFileSystem fs)
            => GetCacheFilePath(analysisRootFolder, moduleName, content, fs);
    }
}
