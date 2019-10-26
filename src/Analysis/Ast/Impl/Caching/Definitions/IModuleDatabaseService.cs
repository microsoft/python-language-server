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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching {
    internal interface IModuleDatabaseService: IModuleDatabaseCache {
        /// <summary>
        /// Creates global scope from module persistent state.
        /// Global scope is then can be used to construct module analysis.
        /// </summary>
        /// <param name="module">Python module to restore analysis for.</param>
        /// <param name="gs">Python module global scope.</param>
        bool TryRestoreGlobalScope(IPythonModule module, out IRestoredGlobalScope gs);

        /// <summary>
        /// Writes module data to the database.
        /// </summary>
        Task StoreModuleAnalysisAsync(IDocumentAnalysis analysis, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines if module analysis exists in the storage.
        /// </summary>
        bool ModuleExistsInStorage(string moduleName, string filePath);
    }
}
