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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents basic module resolution and search subsystem.
    /// </summary>
    public interface IModuleResolution {
        /// <summary>
        /// Builtins module name.
        /// </summary>
        string BuiltinModuleName { get; }

        /// <summary>
        /// Builtins module.
        /// </summary>
        IBuiltinsPythonModule BuiltinsModule { get; }

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        PathResolverSnapshot CurrentPathResolver { get; }

        /// <summary>
        /// Returns an IPythonModule for a given module name. Returns null if
        /// the module does not exist. The import is performed asynchronously.
        /// </summary>
        Task<IPythonModule> ImportModuleAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns an IPythonModule for a given module name. Returns null if
        /// the module has not been imported.
        /// </summary>
        IPythonModule GetImportedModule(string name);

        /// <summary>
        /// Sets user search paths. This changes <see cref="CurrentPathResolver"/>.
        /// </summary>
        /// <returns>Added roots.</returns>
        IEnumerable<string> SetUserSearchPaths(in IEnumerable<string> searchPaths);

        Task ReloadAsync(CancellationToken token = default);
    }
}
