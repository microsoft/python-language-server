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
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Modules {
    internal sealed class DependencyProvider: IDependencyProvider {
        private readonly IPythonModule _module;
        private readonly IModuleDatabaseService _dbService;

        public static IDependencyProvider Empty { get; } = new EmptyDependencyProvider();

        public DependencyProvider(IPythonModule module, IServiceContainer services) {
            _dbService = services.GetService<IModuleDatabaseService>();
            _module = module;
        }

        #region IDependencyProvider
        public HashSet<AnalysisModuleKey> GetDependencies(PythonAst ast) {
            if (_dbService != null && _dbService.TryRestoreDependencies(_module, out var dp)) {
                return dp.GetDependencies(ast);
            }

            // TODO: try and handle LoadFunctionDependencyModules functionality here.
            var dw = new DependencyWalker(_module, ast);
            return dw.Dependencies;
        }
        #endregion

        private sealed class EmptyDependencyProvider: IDependencyProvider {
            public HashSet<AnalysisModuleKey> GetDependencies(PythonAst ast) => new HashSet<AnalysisModuleKey>();
        }
    }
}
