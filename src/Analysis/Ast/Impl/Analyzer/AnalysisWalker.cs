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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Analyzer.Handlers;
using Microsoft.Python.Analysis.Analyzer.Symbols;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Base class with common functionality to module and function analysis walkers.
    /// </summary>
    internal abstract class AnalysisWalker : PythonWalkerAsync {
        protected ImportHandler ImportHandler { get; }
        protected LoopHandler LoopHandler { get; }
        protected ConditionalHandler ConditionalHandler { get; }
        protected AssignmentHandler AssignmentHandler { get; }
        protected WithHandler WithHandler { get; }
        protected TryExceptHandler TryExceptHandler { get; }
        protected NonLocalHandler NonLocalHandler { get; }

        public ExpressionEval Eval { get; }
        public IPythonModule Module => Eval.Module;
        public PythonAst Ast => Eval.Ast;
        protected ModuleSymbolTable SymbolTable => Eval.SymbolTable;

        protected AnalysisWalker(ExpressionEval eval) {
            Eval = eval;
            ImportHandler = new ImportHandler(this);
            AssignmentHandler = new AssignmentHandler(this);
            LoopHandler = new LoopHandler(this);
            ConditionalHandler = new ConditionalHandler(this);
            WithHandler = new WithHandler(this);
            TryExceptHandler = new TryExceptHandler(this);
            NonLocalHandler = new NonLocalHandler(this);
        }

        protected AnalysisWalker(IServiceContainer services, IPythonModule module, PythonAst ast)
            : this(new ExpressionEval(services, module, ast)) {
        }

        #region AST walker overrides
        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            await AssignmentHandler.HandleAssignmentAsync(node, cancellationToken);
            return await base.WalkAsync(node, cancellationToken);
        }

        public override async Task<bool> WalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default) {
            await AssignmentHandler.HandleAnnotatedExpressionAsync(node.Expression as ExpressionWithAnnotation, null, cancellationToken);
            return false;
        }

        public override async Task<bool> WalkAsync(ForStatement node, CancellationToken cancellationToken = default) {
            await LoopHandler.HandleForAsync(node, cancellationToken);
            return await base.WalkAsync(node, cancellationToken);
        }

        public override Task<bool> WalkAsync(FromImportStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(ImportHandler.HandleFromImport(node, cancellationToken));

        public override Task<bool> WalkAsync(GlobalStatement node, CancellationToken cancellationToken = default)
            => NonLocalHandler.HandleGlobalAsync(node, cancellationToken);

        public override Task<bool> WalkAsync(IfStatement node, CancellationToken cancellationToken = default)
            => ConditionalHandler.HandleIfAsync(node, cancellationToken);

        public override Task<bool> WalkAsync(ImportStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(ImportHandler.HandleImport(node, cancellationToken));

        public override Task<bool> WalkAsync(NonlocalStatement node, CancellationToken cancellationToken = default)
            => NonLocalHandler.HandleNonLocalAsync(node, cancellationToken);

        public override async Task<bool> WalkAsync(TryStatement node, CancellationToken cancellationToken = default) {
            await TryExceptHandler.HandleTryExceptAsync(node, cancellationToken);
            return await base.WalkAsync(node, cancellationToken);
        }

        public override async Task<bool> WalkAsync(WhileStatement node, CancellationToken cancellationToken = default) {
            await LoopHandler.HandleWhileAsync(node, cancellationToken);
            return await base.WalkAsync(node, cancellationToken);
        }

        public override async Task<bool> WalkAsync(WithStatement node, CancellationToken cancellationToken = default) {
            await WithHandler.HandleWithAsync(node, cancellationToken);
            return await base.WalkAsync(node, cancellationToken);
        }
        #endregion

        protected T[] GetStatements<T>(ScopeStatement s)
            => (s.Body as SuiteStatement)?.Statements.OfType<T>().ToArray() ?? Array.Empty<T>();
    }
}
