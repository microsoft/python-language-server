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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class NonLocalHandler : StatementHandler {
        public NonLocalHandler(AnalysisWalker walker) : base(walker) { }

        public Task<bool> HandleNonLocalAsync(NonlocalStatement node, CancellationToken cancellationToken = default) {
            foreach (var nex in node.Names) {
                Eval.CurrentScope.DeclareNonLocal(nex.Name, Eval.GetLoc(nex));
            }
            return Task.FromResult(false);
        }

        public Task<bool> HandleGlobalAsync(GlobalStatement node, CancellationToken cancellationToken = default) {
            foreach (var nex in node.Names) {
                Eval.CurrentScope.DeclareGlobal(nex.Name, Eval.GetLoc(nex));
            }
            return Task.FromResult(false);
        }
    }
}
