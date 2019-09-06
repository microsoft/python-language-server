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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class DependencyWalker : PythonWalker {
        private readonly DependencyCollector _dependencyCollector;

        public HashSet<AnalysisModuleKey> Dependencies => _dependencyCollector.Dependencies;

        public DependencyWalker(IPythonModule module, PythonAst ast = null) {
            _dependencyCollector = new DependencyCollector(module);
            ast = ast ?? module.GetAst();
            ast.Walk(this);
        }

        public override bool Walk(ImportStatement import) {
            var forceAbsolute = import.ForceAbsolute;
            foreach (var moduleName in import.Names) {
                var importNames = ImmutableArray<string>.Empty;
                foreach (var nameExpression in moduleName.Names) {
                    importNames = importNames.Add(nameExpression.Name);
                    _dependencyCollector.AddImport(importNames, forceAbsolute);
                }
            }
            return false;
        }

        public override bool Walk(FromImportStatement fromImport) {
            var rootNames = fromImport.Root.Names.Select(n => n.Name);
            var memberNames = fromImport.Names.Select(n => n.Name);
            var dotCount = fromImport.Root is RelativeModuleName relativeName ? relativeName.DotCount : 0;
            _dependencyCollector.AddFromImport(rootNames, memberNames, dotCount, fromImport.ForceAbsolute);
            return false;
        }
    }
}
