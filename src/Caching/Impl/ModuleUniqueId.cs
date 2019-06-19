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
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Caching {
    internal static class ModuleUniqueId {
        public static string GetUniqieId(this IPythonModule module, IServiceContainer services)
            => GetUniqieId(module.Name, module.FilePath, module.ModuleType, services);

        public static string GetUniqieId(string moduleName, string filePath, ModuleType moduleType, IServiceContainer services) {
            var interpreter = services.GetService<IPythonInterpreter>();
            var stubCache = services.GetService<IStubCache>();
            var fs = services.GetService<IFileSystem>();

            if (moduleType == ModuleType.User) {
                // Only for tests.
                return $"{moduleName}";
            }

            var config = interpreter.Configuration;
            var standardLibraryPath = PythonLibraryPath.GetStandardLibraryPath(config);
            var sitePackagesPath = PythonLibraryPath.GetSitePackagesPath(config);

            if (!string.IsNullOrEmpty(filePath) && fs.IsPathUnderRoot(sitePackagesPath, filePath)) {
                // If module is in site-packages and is versioned, then unique id = name + version + interpreter version.
                // Example: 'requests' and 'requests-2.21.0.dist-info'.
                var moduleFolder = Path.GetDirectoryName(Path.GetDirectoryName(filePath));

                // TODO: for egg (https://github.com/microsoft/python-language-server/issues/196), consider *.egg-info
                var folders = fs
                    .GetFileSystemEntries(moduleFolder, "*-*.dist-info", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Where(n => n.StartsWith(moduleName, StringComparison.OrdinalIgnoreCase)) // Module name can be capitalized differently.
                    .ToArray();

                if (folders.Length == 1) {
                    var fileName = Path.GetFileNameWithoutExtension(folders[0]);
                    var dash = fileName.IndexOf('-');
                    return $"{fileName.Substring(0, dash)}({fileName.Substring(dash + 1)})";
                }
            }

            if (moduleType == ModuleType.Builtins || moduleType == ModuleType.CompiledBuiltin ||
                string.IsNullOrEmpty(filePath) || fs.IsPathUnderRoot(standardLibraryPath, filePath)) {
                // If module is a standard library, unique id is its name + interpreter version.
                return $"{moduleName}({config.Version.Major}.{config.Version.Minor})";
            }

            // If all else fails, hash module data.
            return $"{moduleName}.{HashModuleContent(Path.GetDirectoryName(filePath), fs)}";
        }

        private static string HashModuleContent(string moduleFolder, IFileSystem fs) {
            // Hash file sizes 
            using (var sha256 = SHA256.Create()) {
                var total = fs
                    .GetFileSystemEntries(moduleFolder, "*.*", SearchOption.AllDirectories)
                    .Where(fs.FileExists)
                    .Select(fs.FileSize)
                    .Aggregate((hash, e) => unchecked(hash * 31 ^ e.GetHashCode()));

                return Convert
                    .ToBase64String(sha256.ComputeHash(BitConverter.GetBytes(total)))
                    .Replace('/', '_').Replace('+', '-');
            }
        }
    }
}
