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
using System.Text;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules {
    internal partial class PythonModule {
        private string CalculateUniqueId() {
            var sitePackagesPath = PythonLibraryPath.GetSitePackagesPath(Interpreter.Configuration);
            if (FileSystem.IsPathUnderRoot(sitePackagesPath, FilePath)) {
                // If module is in site-packages and is versioned, then unique id = name + version + interpreter version.
                // Example: 'lxml' and 'lxml-4.2.5.dist-info'.
                var moduleFolder = Path.GetDirectoryName(Path.GetDirectoryName(FilePath));
                var folders = FileSystem
                    .GetFileSystemEntries(moduleFolder, "*-*.dist-info", SearchOption.TopDirectoryOnly)
                    .Where(p => p.StartsWith(Name, StringComparison.OrdinalIgnoreCase)) // Module name can be capitalized differently.
                    .ToArray();
                if (folders.Length == 2) {
                    return Path.GetFileNameWithoutExtension(folders[1]);
                }
            }

            var standardLibraryPath = PythonLibraryPath.GetStandardLibraryPath(Interpreter.Configuration);
            if (FileSystem.IsPathUnderRoot(standardLibraryPath, FilePath)) {
                // If module is a standard library, unique id is its name + interpreter version.
                return $"{Name}({Interpreter.Configuration.Version})";
            }

            // If all else fails, hash the entire content.
            return $"{Name}.{HashModuleContent(Path.GetDirectoryName(FilePath))}";
        }

        private string HashModuleContent(string moduleFolder) {
            // TODO: consider async? What about *.pyd?
            using (var sha256 = SHA256.Create()) {
                var bytes = FileSystem
                    .GetFileSystemEntries(moduleFolder, "*.py", SearchOption.AllDirectories)
                    .Select(p => sha256.ComputeHash(new UTF8Encoding(false).GetBytes(FileSystem.ReadTextWithRetry(p))))
                    .Aggregate((b1, b2) => b1.Zip(b2, (x1, x2) => (byte)(x1 ^ x2)).ToArray());

                return Convert.ToBase64String(bytes).Replace('/', '_').Replace('+', '-');
            }
        }
    }
}
