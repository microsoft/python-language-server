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

using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class TryExceptHandler : StatementHandler {
        public TryExceptHandler(AnalysisWalker walker) : base(walker) { }

        public bool HandleTryExcept(TryStatement node) {
            node.Body.Walk(Walker);
            foreach (var handler in node.Handlers.MaybeEnumerate()) {
                if (handler.Test != null && handler.Target is NameExpression nex) {
                    var value = Eval.GetValueFromExpression(handler.Test);
                    Eval.DeclareVariable(nex.Name, value ?? Eval.UnknownType, VariableSource.Declaration, Module, nex);
                }
                handler.Body.Walk(Walker);
            }

            node.Finally?.Walk(Walker);
            node.Else?.Walk(Walker);
            return false;
        }
    }
}
