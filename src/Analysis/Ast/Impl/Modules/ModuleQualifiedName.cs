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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules {
    internal static class ModuleQualifiedName {
        public static string CalculateQualifiedName(IPythonModule module, IFileSystem fs) {
            var config = module.Interpreter.Configuration;
            var sitePackagesPath = PythonLibraryPath.GetSitePackagesPath(config);

            if (fs.IsPathUnderRoot(sitePackagesPath, module.FilePath)) {
                // If module is in site-packages and is versioned, then unique id = name + version + interpreter version.
                // Example: 'requests' and 'requests-2.21.0.dist-info'.
                var moduleFolder = Path.GetDirectoryName(Path.GetDirectoryName(module.FilePath));
                
                // TODO: for egg (https://github.com/microsoft/python-language-server/issues/196), consider *.egg-info
                var folders = fs
                    .GetFileSystemEntries(moduleFolder, "*-*.dist-info", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Where(n => n.StartsWith(module.Name, StringComparison.OrdinalIgnoreCase)) // Module name can be capitalized differently.
                    .ToArray();

                if (folders.Length == 1) {
                    var fileName = Path.GetFileNameWithoutExtension(folders[0]);
                    var dash = fileName.IndexOf('-');
                    return $"{fileName.Substring(0, dash)}({fileName.Substring(dash + 1)})";
                }
            }

            var standardLibraryPath = PythonLibraryPath.GetStandardLibraryPath(config);
            if (fs.IsPathUnderRoot(standardLibraryPath, module.FilePath)) {
                // If module is a standard library, unique id is its name + interpreter version.
                return $"{module.Name}({config.Version})";
            }

            // If all else fails, hash the entire content.
            return $"{module.Name}.{HashModuleContent(Path.GetDirectoryName(module.FilePath), fs)}";
        }

        public static string GetModuleName(string moduleQualifiedName) {
            var index = moduleQualifiedName.IndexOf('(');
            return index >= 0 ? moduleQualifiedName.Substring(0, index) : moduleQualifiedName;
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
