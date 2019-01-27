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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents module resolution and search subsystem.
    /// </summary>
    public interface IModuleResolution {
        string BuiltinModuleName { get; }

        /// <summary>
        /// Locates module by path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        ModulePath FindModule(string filePath);

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

        IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines if directory contains Python package.
        /// </summary>
        bool IsPackage(string directory);

        /// <summary>
        /// Builtins module.
        /// </summary>
        IBuiltinsPythonModule BuiltinsModule { get; }

        IModuleCache ModuleCache { get; }

        Task ReloadAsync(CancellationToken token = default);

        void AddModulePath(string path);

        /// <summary>
        /// Provides ability to specialize module by replacing module import by
        /// <see cref="IPythonModule"/> implementation in code. Real module
        /// content is loaded and analyzed only for class/functions definitions
        /// so the original documentation can be extracted.
        /// </summary>
        /// <param name="name">Module to specialize.</param>
        /// <param name="specializationConstructor">Specialized module constructor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Original (library) module loaded as stub.</returns>
        Task<IPythonModule> SpecializeModuleAsync(string name, Func<string, IPythonModule> specializationConstructor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns specialized module, if any.
        /// </summary>
        IPythonModule GetSpecializedModule(string name);
    }
}
