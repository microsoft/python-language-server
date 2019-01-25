﻿// Copyright(c) Microsoft Corporation
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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class WithHandler : StatementHandler {
        public WithHandler(AnalysisWalker walker) : base(walker) { }

        public async Task HandleWithAsync(WithStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in node.Items.Where(x => x.Variable != null)) {
                var contextManager = await Eval.GetValueFromExpressionAsync(item.ContextManager, cancellationToken);
                var cmType = contextManager.GetPythonType();

                var enter = cmType?.GetMember(node.IsAsync ? @"__aenter__" : @"__enter__")?.GetPythonType<IPythonFunctionType>();
                if (enter != null) {
                    var context = await Eval.GetValueFromFunctionTypeAsync(enter, null, null, cancellationToken);
                    if (item.Variable is NameExpression nex && !string.IsNullOrEmpty(nex.Name)) {
                        Eval.DeclareVariable(nex.Name, context, VariableSource.Declaration, Eval.GetLoc(item));
                    }
                }
            }
        }
    }
}
