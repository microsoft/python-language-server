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
using System.Collections.Generic;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting {
    internal sealed class LinterWalker: PythonWalker {
        private readonly IDocumentAnalysis _analysis;
        private readonly Stack<IDisposable> _scopeStack = new Stack<IDisposable>();
        private readonly ExpressionWalker _expressionWalker;

        public LinterWalker(IDocumentAnalysis analysis) {
            _analysis = analysis;
            _expressionWalker = new ExpressionWalker(_analysis);
        }

        public override bool Walk(ClassDefinition cd) {
            _scopeStack.Push(_analysis.ExpressionEvaluator.OpenScope(_analysis.Document, cd));
            return true;
        }
        public override void PostWalk(ClassDefinition cd) => _scopeStack.Pop().Dispose();

        public override bool Walk(FunctionDefinition fd) {
            _scopeStack.Push(_analysis.ExpressionEvaluator.OpenScope(_analysis.Document, fd));
            return true;
        }
        public override void PostWalk(FunctionDefinition cd) => _scopeStack.Pop().Dispose();

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
                var m = _analysis.ExpressionEvaluator.LookupNameInScopes(nex.Name, out _, LookupOptions.Global);
                if (m == null) {
                    _analysis.ReportUndefinedVariable(nex);
                }
            }
            return false;
        }

        public override bool Walk(NonlocalStatement node) {
            foreach (var nex in node.Names) {
                var m = _analysis.ExpressionEvaluator.LookupNameInScopes(nex.Name, out _, LookupOptions.Nonlocal);
                if (m == null) {
                    _analysis.ReportUndefinedVariable(nex);
                }
            }
            return false;
        }
        public override bool Walk(ComprehensionFor cfor) {
            return false;
        }
        public override bool Walk(ComprehensionIf cif) {
            return false;
        }
    }
}
