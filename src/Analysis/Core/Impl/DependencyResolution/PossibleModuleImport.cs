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

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public class PossibleModuleImport : IImportSearchResult {
        public string PossibleModuleFullName { get; }
        public string RootPath { get; }
        public string PrecedingModuleFullName { get; }
        public string PrecedingModulePath { get; }
        public IReadOnlyList<string> RemainingNameParts { get; }

        public PossibleModuleImport(string possibleModuleFullName, string rootPath, string precedingModuleFullName, string precedingModulePath, IReadOnlyList<string> remainingNameParts) {
            PossibleModuleFullName = possibleModuleFullName;
            RootPath = rootPath;
            PrecedingModuleFullName = precedingModuleFullName;
            PrecedingModulePath = precedingModulePath;
            RemainingNameParts = remainingNameParts;
        }
    }
}
