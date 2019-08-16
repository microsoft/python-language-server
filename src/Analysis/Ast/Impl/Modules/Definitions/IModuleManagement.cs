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
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents module resolution and search subsystem.
    /// </summary>
    public interface IModuleManagement : IModuleResolution {
        /// <summary>
        /// Locates module by path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        ModulePath FindModule(string filePath);

        /// <summary>
        /// Cache of module stubs generated from compiled modules.
        /// </summary>
        IStubCache StubCache { get; }

        bool TryAddModulePath(in string path, in long fileSize, in bool allowNonRooted, out string fullName);

        /// <summary>
        /// Provides ability to specialize module by replacing module import by
        /// <see cref="IPythonModule"/> implementation in code. Real module
        /// content is loaded and analyzed only for class/functions definitions
        /// so the original documentation can be extracted.
        /// </summary>
        /// <param name="fullName">Module to specialize.</param>
        /// <param name="specializationConstructor">Specialized module constructor.</param>
        /// <param name="replaceExisting">Replace existing loaded module, if any.</param>
        /// <returns>Specialized module.</returns>
        IPythonModule SpecializeModule(string fullName, Func<string, IPythonModule> specializationConstructor, bool replaceExisting = false);

        /// <summary>
        /// Returns specialized module, if any. Will attempt to load module from persistent state.
        /// </summary>
        IPythonModule GetSpecializedModule(string fullName, bool allowCreation = false, string modulePath = null);

        /// <summary>
        /// Determines of module is specialized or exists in the database.
        /// </summary>
        bool IsSpecializedModule(string fullName, string modulePath = null);

        /// <summary>
        /// Root directory of the path resolver.
        /// </summary>
        string Root { get; }

        /// <summary>
        /// Set of interpreter paths.
        /// </summary>
        ImmutableArray<string> InterpreterPaths { get; }

        /// <summary>
        /// Interpreter paths with additional classification.
        /// </summary>
        IReadOnlyList<PythonLibraryPath> LibraryPaths { get; }
    }
}
