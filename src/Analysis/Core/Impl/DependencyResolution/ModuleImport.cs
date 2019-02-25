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

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public class ModuleImport : IImportSearchResult {
        public string Name { get; }
        public string FullName { get; }
        public string RootPath { get; }
        public string ModulePath { get; }
        public bool IsCompiled { get; }
        public bool IsLibrary { get; }
        public bool IsBuiltin => IsCompiled && ModulePath == null;

        public ModuleImport(string name, string fullName, string rootPath, string modulePath, bool isCompiled, bool isLibrary) {
            Name = name;
            FullName = fullName;
            RootPath = rootPath;
            ModulePath = modulePath;
            IsCompiled = isCompiled;
            IsLibrary = isLibrary;
        }
    }
}
