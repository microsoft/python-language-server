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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class WithHandler : StatementHandler {
        public WithHandler(AnalysisWalker walker) : base(walker) { }

        public void HandleWith(WithStatement node) {
            foreach (var item in node.Items.Where(x => x.Variable != null)) {
                var contextManager = Eval.GetValueFromExpression(item.ContextManager);
                var cmType = contextManager?.GetPythonType();
                IMember context = Eval.UnknownType;

                var enter = cmType?.GetMember(node.IsAsync ? @"__aenter__" : @"__enter__")?.GetPythonType<IPythonFunctionType>();
                if (enter != null) {
                    var instance = contextManager as IPythonInstance;
                    var callExpr = item.ContextManager as CallExpression;
                    context = Eval.GetValueFromFunctionType(enter, instance, callExpr);
                    // If fetching context from __enter__ failed, annotation in the stub may be using
                    // type from typing that we haven't specialized yet or there may be an issue in
                    // the stub itself, such as type or incorrect type. Try using context manager then.
                    context = context.IsUnknown() ? contextManager : context;
                }

                switch (item.Variable) {
                    case NameExpression nameExpr when !string.IsNullOrEmpty(nameExpr.Name):
                        Eval.DeclareVariable(nameExpr.Name, context, VariableSource.Declaration, item);
                        break;
                    case SequenceExpression seqExpr:
                        var sequenceHandler = new SequenceExpressionHandler(Walker);
                        SequenceExpressionHandler.Assign(new[] { item.Variable }, context, Eval);
                        break;
                }
            }
        }
    }
}
