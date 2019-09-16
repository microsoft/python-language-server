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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Dependencies {
    /// <summary>
    /// Implements provider that can supply list of imports to the dependency analysis.
    /// Regular modules provide dependency from the AST, persistent/database modules
    /// provide dependencies from their models.
    /// </summary>
    internal interface IDependencyProvider {
        ISet<AnalysisModuleKey> GetDependencies(PythonAst ast);
    }
}
