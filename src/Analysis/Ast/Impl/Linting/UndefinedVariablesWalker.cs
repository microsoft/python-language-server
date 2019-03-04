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

using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting {
    internal sealed class UndefinedVariablesWalker : LinterWalker {
        private readonly ExpressionWalker _expressionWalker;

        public UndefinedVariablesWalker(IDocumentAnalysis analysis, IServiceContainer services)
            : base(analysis, services) {
            _expressionWalker = new ExpressionWalker(analysis);
        }

        public override bool Walk(AssignmentStatement node) {
            if (node.Right is ErrorExpression) {
                return false;
            }
            node.Right?.Walk(_expressionWalker);
            return false;
        }

        public override bool Walk(CallExpression node) {
            foreach (var arg in node.Args) {
                arg?.Expression?.Walk(_expressionWalker);
            }
            return false;
        }

        public override bool Walk(IfStatement node) {
            foreach (var test in node.Tests) {
                test.Test.Walk(_expressionWalker);
            }
            return true;
        }

        public override bool Walk(GlobalStatement node) {
            foreach (var nex in node.Names) {
                var m = Eval.LookupNameInScopes(nex.Name, out _, LookupOptions.Global);
                if (m == null) {
                    Analysis.ReportUndefinedVariable(nex);
                }
            }
            return false;
        }

        public override bool Walk(NonlocalStatement node) {
            foreach (var nex in node.Names) {
                var m = Eval.LookupNameInScopes(nex.Name, out _, LookupOptions.Nonlocal);
                if (m == null) {
                    Analysis.ReportUndefinedVariable(nex);
                }
            }
            return false;
        }
    }
}
