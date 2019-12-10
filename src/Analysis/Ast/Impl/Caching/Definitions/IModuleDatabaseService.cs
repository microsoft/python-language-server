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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching {
    internal interface IModuleDatabaseService: IModuleDatabaseCache {
        /// <summary>
        /// Restores module from database.
        /// </summary>
        IPythonModule RestoreModule(string moduleName, string modulePath, ModuleType moduleType);

        /// <summary>
        /// Writes module data to the database.
        /// </summary>
        /// <param name="analysis">Document analysis</param>
        /// <param name="immediate">
        /// True if database should be written to disk immediately
        /// as opposed to delaying writing until complete analysis event from the <see cref="IPythonAnalyzer"/>
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StoreModuleAnalysisAsync(IDocumentAnalysis analysis, bool immediate = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines if module analysis exists in the storage.
        /// </summary>
        bool ModuleExistsInStorage(string name, string filePath, ModuleType moduleType);
    }

    internal static class ModuleDatabaseExtensions {
        public static bool ModuleExistsInStorage(this IModuleDatabaseService dbs, IPythonModule module)
            => dbs.ModuleExistsInStorage(module.Name, module.FilePath, module.ModuleType);
    }
}
