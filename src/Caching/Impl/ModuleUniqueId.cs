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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Caching {
    internal static class ModuleUniqueId {
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
            if(cachingLevel == AnalysisCachingLevel.None) {
                return null;
            }

            var interpreter = services.GetService<IPythonInterpreter>();
            var fs = services.GetService<IFileSystem>();

            var moduleResolution = interpreter.ModuleResolution;
            var modulePathType = GetModulePathType(filePath, moduleResolution.LibraryPaths, fs);
            switch(modulePathType) {
                case PythonLibraryPathType.Site when cachingLevel < AnalysisCachingLevel.Library:
                    return null;
                case PythonLibraryPathType.StdLib when cachingLevel < AnalysisCachingLevel.System:
                    return null;
            }

            var parent = moduleResolution.CurrentPathResolver.GetModuleParentFromModuleName(moduleName);
            if (parent == null) {
                return moduleName;
            }
            var config = interpreter.Configuration;
            return $"{moduleName}({config.Version.Major}.{config.Version.Minor})";
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
