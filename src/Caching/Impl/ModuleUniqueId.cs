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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Caching {
    internal static class ModuleUniqueId {
        public static string GetUniqueId(this IPythonModule module, IServiceContainer services, AnalysisCachingOptions options)
            => GetUniqueId(module.Name, module.FilePath, module.ModuleType, services, options);

        public static string GetUniqueId(string moduleName, string filePath, ModuleType moduleType, IServiceContainer services, AnalysisCachingOptions options) {
            if(options == AnalysisCachingOptions.None) {
                return null;
            }
            if (moduleType == ModuleType.User) {
                // Only for tests.
                return $"{moduleName}";
            }

            var interpreter = services.GetService<IPythonInterpreter>();
            var fs = services.GetService<IFileSystem>();
            
            var modulePathType = GetModulePathType(filePath, interpreter.ModuleResolution.LibraryPaths, fs);
            switch(modulePathType) {
                case PythonLibraryPathType.Site when options < AnalysisCachingOptions.Library:
                    return null;
                case PythonLibraryPathType.StdLib when options < AnalysisCachingOptions.System:
                    return null;
            }

            if (!string.IsNullOrEmpty(filePath) && modulePathType == PythonLibraryPathType.Site) {
                // Module can be a submodule of a versioned package. In this case we want to use
                // version of the enclosing package so we have to look up the chain of folders.
                var moduleRootName = moduleName.Split('.')[0];
                var moduleFilesFolder = Path.GetDirectoryName(filePath);
                var installationFolder = Path.GetDirectoryName(moduleFilesFolder);

                var versionFolder = installationFolder;
                while (!string.IsNullOrEmpty(versionFolder)) {
                    // If module is in site-packages and is versioned, then unique id = name + version + interpreter version.
                    // Example: 'requests' and 'requests-2.21.0.dist-info'.
                    // TODO: for egg (https://github.com/microsoft/python-language-server/issues/196), consider *.egg-info
                    var folders = fs.GetFileSystemEntries(versionFolder, "*-*.dist-info", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .Where(n => n.StartsWith(moduleRootName, StringComparison.OrdinalIgnoreCase)) // Module name can be capitalized differently.
                        .ToArray();

                    if (folders.Length == 1) {
                        var fileName = Path.GetFileNameWithoutExtension(folders[0]);
                        var dash = fileName.IndexOf('-');
                        return $"{moduleName}({fileName.Substring(dash + 1)})";
                    }
                    // Move up if nothing is found.
                    versionFolder = Path.GetDirectoryName(versionFolder);
                }
            }

            var config = interpreter.Configuration;
            if (moduleType == ModuleType.Builtins || moduleType == ModuleType.CompiledBuiltin ||
                string.IsNullOrEmpty(filePath) || modulePathType == PythonLibraryPathType.StdLib) {
                // If module is a standard library, unique id is its name + interpreter version.
                return $"{moduleName}({config.Version.Major}.{config.Version.Minor})";
            }

            // If all else fails, hash module data.
            return $"{moduleName}.{HashModuleContent(Path.GetDirectoryName(filePath), fs)}";
        }

        private static string HashModuleContent(string moduleFolder, IFileSystem fs) {
            // Hash file sizes 
            var total = fs
                .GetFileSystemEntries(moduleFolder, "*.*", SearchOption.AllDirectories)
                .Where(fs.FileExists)
                .Select(fs.FileSize)
                .Aggregate((hash, e) => unchecked(hash * 31 ^ e.GetHashCode()));

            return ((uint)total).ToString();
        }

        private static PythonLibraryPathType GetModulePathType(string modulePath, IEnumerable<PythonLibraryPath> libraryPaths, IFileSystem fs) {
            if (string.IsNullOrEmpty(modulePath)) {
                return PythonLibraryPathType.Unspecified;
            }
            return libraryPaths
                .OrderByDescending(p => p.Path.Length)
                .FirstOrDefault(p => fs.IsPathUnderRoot(p.Path, modulePath))?.Type ?? PythonLibraryPathType.Unspecified;
        }
    }
}
