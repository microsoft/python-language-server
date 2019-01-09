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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class LoopHandler : StatementHandler {
        public LoopHandler(AnalysisWalker walker) : base(walker) { }

        public async Task HandleForAsync(ForStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Left is NameExpression nex) {
                var iterable = await Eval.GetValueFromExpressionAsync(node.List, cancellationToken);
                var value = (iterable as IPythonIterable)?.GetIterator()?.Next ?? Eval.UnknownType;
                Eval.DeclareVariable(nex.Name, value, Eval.GetLoc(node.Left));
            }
            if (node.Body != null) {
                await node.Body.WalkAsync(Walker, cancellationToken);
            }
        }
    }
}
