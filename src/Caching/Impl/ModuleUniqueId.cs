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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Caching {
    internal static class ModuleUniqueId {
        private struct ModuleKey {
            public string ModuleName { get; set; }
            public string FilePath { get; set; }
            public ModuleType ModuleType { get; set; }
        }

        private static readonly ConcurrentDictionary<ModuleKey, string> _nameCache = new ConcurrentDictionary<ModuleKey, string>();

        public static string GetUniqueId(this IPythonModule module, IServiceContainer services, AnalysisCachingLevel cachingLevel = AnalysisCachingLevel.Library) {
            // If module is a standalone stub, permit it. Otherwise redirect to the main module
            // since during stub merge types from stub normally become part of the primary module.
            if (module.ModuleType == ModuleType.Stub && module.PrimaryModule != null) {
                module = module.PrimaryModule;
            }
            return GetUniqueId(module.Name, module.FilePath, module.ModuleType, services, cachingLevel);
        }

        public static string GetUniqueId(string moduleName, string filePath, ModuleType moduleType,
            IServiceContainer services, AnalysisCachingLevel cachingLevel = AnalysisCachingLevel.Library) {
            if (cachingLevel == AnalysisCachingLevel.None) {
                return null;
            }

            if (moduleType == ModuleType.User) {
                return moduleName; // For tests where user modules are cached.
            }

            var key = new ModuleKey { ModuleName = moduleName, FilePath = filePath, ModuleType = moduleType };
            if (_nameCache.TryGetValue(key, out var id)) {
                return id;
            }

            var interpreter = services.GetService<IPythonInterpreter>();
            var fs = services.GetService<IFileSystem>();

            var moduleResolution = interpreter.ModuleResolution;
            var modulePathType = GetModulePathType(filePath, moduleResolution.LibraryPaths, fs);
            switch (modulePathType) {
                case PythonLibraryPathType.Site when cachingLevel < AnalysisCachingLevel.Library:
                    return null;
                case PythonLibraryPathType.StdLib when cachingLevel < AnalysisCachingLevel.System:
                    return null;
            }

            if (!string.IsNullOrEmpty(filePath)) {
                var index = filePath.IndexOfOrdinal(".dist-info");
                if (index > 0) {
                    // If module is in site-packages and is versioned, then unique id = name + version + interpreter version.
                    // Example: 'requests' and 'requests-2.21.0.dist-info'.
                    // TODO: for egg (https://github.com/microsoft/python-language-server/issues/196), consider *.egg-info
                    var dash = filePath.IndexOf('-');
                    id = $"{moduleName}({filePath.Substring(dash + 1, index - dash - 1)})";
                }
            }

            if (id == null) {
                var config = interpreter.Configuration;
                if (moduleType == ModuleType.CompiledBuiltin || string.IsNullOrEmpty(filePath) || modulePathType == PythonLibraryPathType.StdLib) {
                    // If module is a standard library, unique id is its name + interpreter version.
                    id = $"{moduleName}({config.Version.Major}.{config.Version.Minor})";
                }
            }

            if (id == null) {
                var parent = moduleResolution.CurrentPathResolver.GetModuleParentFromModuleName(moduleName);
                if (parent == null) {
                    id = moduleName;
                } else {
                    var hash = HashModuleFileSizes(parent);
                    // If all else fails, hash modules file sizes.
                    id = $"{moduleName}.{(ulong)hash}";
                }
            }

            if (id != null) {
                _nameCache[key] = id;
            }
            return id;
        }

        private static long HashModuleFileSizes(IImportChildrenSource source) {
            var hash = 0L;
            var names = source.GetChildrenNames();
            foreach (var name in names) {
                if (source.TryGetChildImport(name, out var child)) {
                    if (child is ModuleImport moduleImport) {
                        if (moduleImport.ModuleFileSize == 0) {
                            continue; // Typically test case, memory-only module.
                        }
                        hash = unchecked(hash * 31 ^ moduleImport.ModuleFileSize);
                    }

                    if (child is IImportChildrenSource childSource) {
                        hash = unchecked(hash * 31 ^ HashModuleFileSizes(childSource));
                    }
                }
            }

            return hash;
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
