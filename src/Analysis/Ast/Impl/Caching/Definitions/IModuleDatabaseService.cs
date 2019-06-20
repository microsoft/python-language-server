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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching {
    internal interface IModuleDatabaseService {
        /// <summary>
        /// Creates module representation from module persistent state.
        /// </summary>
        /// <param name="moduleName">Module name. If the name is not qualified
        /// the module will ge resolved against active Python version.</param>
        /// <param name="filePath">Module file path.</param>
        /// <param name="module">Python module.</param>
        /// <returns>Module storage state</returns>
        ModuleStorageState TryCreateModule(string moduleName, string filePath, out IPythonModule module);

        /// <summary>
        /// Writes module data to the database.
        /// </summary>
        Task StoreModuleAnalysisAsync(IDocumentAnalysis analysis, CancellationToken cancellationToken = default);
    }
}
