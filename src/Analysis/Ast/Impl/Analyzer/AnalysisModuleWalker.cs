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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Module.Name} : {Module.ModuleType}")]
    internal partial class AnalysisModuleWalker : AnalysisWalker {
        private IDisposable _classScope;

        public AnalysisModuleWalker(IServiceContainer services, IPythonModule module, PythonAst ast)
            : base(services, module, ast) {
            // TODO: handle typing module
        }

        public override async Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) {
            Check.InvalidOperation(() => Ast == node, "walking wrong AST");

            CollectTopLevelDefinitions();
            cancellationToken.ThrowIfCancellationRequested();

            return await base.WalkAsync(node, cancellationToken);
        }

        private void CollectTopLevelDefinitions() {
            var statement = (Ast.Body as SuiteStatement)?.Statements.ToArray() ?? Array.Empty<Statement>();

            foreach (var node in statement.OfType<FunctionDefinition>()) {
                ProcessFunctionDefinition(node);
            }

            foreach (var node in statement.OfType<ClassDefinition>()) {
                var classInfo = CreateClass(node);
                Lookup.DeclareVariable(node.Name, classInfo, GetLoc(node));
            }
        }
    }
}
