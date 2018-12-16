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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Modules {
    public interface IModuleResolution {
        string BuiltinModuleName { get; }
        IReadOnlyList<string> SearchPaths { get; }

        Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(IEnumerable<string> searchPaths, CancellationToken cancellationToken = default);
        ModulePath FindModule(string filePath);
        IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, CancellationToken cancellationToken);

        /// <summary>
        /// Determines if directory contains Python package.
        /// </summary>
        bool IsPackage(string directory);

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        PathResolverSnapshot CurrentPathResolver { get; }

        /// <summary>
        /// Returns an IPythonModule for a given module name. Returns null if
        /// the module does not exist. The import is performed asynchronously.
        /// </summary>
        Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Builtins module.
        /// </summary>
        IBuiltinPythonModule BuiltinModule { get; }

        IModuleCache ModuleCache { get; }

        void Reload();
    }
}
