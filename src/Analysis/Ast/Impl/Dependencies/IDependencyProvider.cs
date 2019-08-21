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

namespace Microsoft.Python.Analysis.Dependencies {
    /// <summary>
    /// Represents an equivalent of import statement in the AST.
    /// </summary>
    internal interface IImportDependency {
        IReadOnlyList<string> ModuleNames { get; }
        bool ForceAbsolute { get; }
    }

    /// <summary>
    /// Represents an equivalent of from import statement in the AST.
    /// </summary>
    internal interface IFromImportDependency {
        IReadOnlyList<string> RootNames { get; }
        int DotCount { get; }
        bool ForceAbsolute { get; }
    }

    /// <summary>
    /// Implemented by a module that can provide list of imports to the dependency analysis.
    /// </summary>
    internal interface IDependencyProvider {
        IReadOnlyList<IImportDependency> Imports { get; }
        IReadOnlyList<IFromImportDependency> FromImports { get; }
    }
}
