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
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents module resolution and search subsystem.
    /// </summary>
    public interface IModuleManagement: IModuleResolution {
        /// <summary>
        /// Locates module by path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        ModulePath FindModule(string filePath);

        IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, CancellationToken cancellationToken = default);

        IModuleCache ModuleCache { get; }

        bool TryAddModulePath(in string path, out string fullName);

        /// <summary>
        /// Sets user search paths. This changes <see cref="IModuleResolution.CurrentPathResolver"/>.
        /// </summary>
        /// <returns>Added roots.</returns>
        IEnumerable<string> SetUserSearchPaths(in IEnumerable<string> searchPaths);

        /// <summary>
        /// Provides ability to specialize module by replacing module import by
        /// <see cref="IPythonModule"/> implementation in code. Real module
        /// content is loaded and analyzed only for class/functions definitions
        /// so the original documentation can be extracted.
        /// </summary>
        /// <param name="name">Module to specialize.</param>
        /// <param name="specializationConstructor">Specialized module constructor.</param>
        /// <returns>Original (library) module loaded as stub, if any.</returns>
        IPythonModule SpecializeModule(string name, Func<string, IPythonModule> specializationConstructor);

        /// <summary>
        /// Returns specialized module, if any.
        /// </summary>
        IPythonModule GetSpecializedModule(string name);

        /// <summary>
        /// Root directory of the path resolver.
        /// </summary>
        string Root { get; }

        /// <summary>
        /// Set of interpreter paths.
        /// </summary>
        IEnumerable<string> InterpreterPaths { get; }
    }
}
