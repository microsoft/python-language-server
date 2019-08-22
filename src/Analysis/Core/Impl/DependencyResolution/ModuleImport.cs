﻿// Copyright(c) Microsoft Corporation
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

using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public class ModuleImport : IImportChildrenSource {
        private readonly IImportChildrenSource _childrenSource;

        public string Name { get; }
        public string FullName { get; }
        public string RootPath { get; }
        public string ModulePath { get; }
        public long ModuleFileSize { get; }
        public bool IsCompiled { get; }
        public bool IsLibrary { get; }
        public bool IsBuiltin => IsCompiled && ModulePath == null;
        public bool IsPersistent { get; set; }

        public ModuleImport(IImportChildrenSource childrenSource, string name, string fullName, string rootPath, string modulePath, long moduleFileSize, bool isCompiled, bool isLibrary) {
            _childrenSource = childrenSource;
            Name = name;
            FullName = fullName;
            RootPath = rootPath;
            ModulePath = modulePath;
            ModuleFileSize = moduleFileSize;
            IsCompiled = isCompiled;
            IsLibrary = isLibrary;
        }

        public ImmutableArray<string> GetChildrenNames() => _childrenSource.GetChildrenNames();
        public bool TryGetChildImport(string name, out IImportSearchResult child) => _childrenSource.TryGetChildImport(name, out child);
    }
}
