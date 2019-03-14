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
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting {
    internal abstract class LinterWalker: PythonWalker {
        private readonly Stack<IDisposable> _scopeStack = new Stack<IDisposable>();

        protected IDocumentAnalysis Analysis { get; }
        protected IExpressionEvaluator Eval => Analysis.ExpressionEvaluator;
        protected IServiceContainer Services { get; }

        protected LinterWalker(IDocumentAnalysis analysis, IServiceContainer services) {
            Analysis = analysis;
            Services = services;
        }

        public override bool Walk(ClassDefinition cd) {
            _scopeStack.Push(Eval.OpenScope(Analysis.Document, cd));
            return true;
        }
        public override void PostWalk(ClassDefinition cd) => _scopeStack.Pop().Dispose();

        public override bool Walk(FunctionDefinition fd) {
            _scopeStack.Push(Eval.OpenScope(Analysis.Document, fd));
            return true;
        }
        public override void PostWalk(FunctionDefinition cd) => _scopeStack.Pop().Dispose();
    }
}
