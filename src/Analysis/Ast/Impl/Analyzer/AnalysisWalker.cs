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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Analyzer.Handlers;
using Microsoft.Python.Analysis.Analyzer.Symbols;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Base class with common functionality to module and function analysis walkers.
    /// </summary>
    internal abstract class AnalysisWalker : PythonWalker {
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

        protected AnalysisWalker(ExpressionEval eval, IImportedVariableHandler importedVariableHandler) {
            Eval = eval;
            ImportHandler = new ImportHandler(this, importedVariableHandler);
            AssignmentHandler = new AssignmentHandler(this);
            LoopHandler = new LoopHandler(this);
            ConditionalHandler = new ConditionalHandler(this);
            WithHandler = new WithHandler(this);
            TryExceptHandler = new TryExceptHandler(this);
            NonLocalHandler = new NonLocalHandler(this);
        }

        #region AST walker overrides
        public override bool Walk(AssignmentStatement node) {
            AssignmentHandler.HandleAssignment(node);
            return base.Walk(node);
        }

        public override bool Walk(NamedExpression node) {
            AssignmentHandler.HandleNamedExpression(node);
            return base.Walk(node);
        }

        public override bool Walk(ExpressionStatement node) {
            switch (node.Expression) {
                case ExpressionWithAnnotation ea:
                    AssignmentHandler.HandleAnnotatedExpression(ea, null);
                    return false;
                case Comprehension comp:
                    Eval.ProcessComprehension(comp);
                    return false;
                case CallExpression callex:
                    Eval.ProcessCallForReferences(callex);
                    return true;
                default:
                    return base.Walk(node);
            }
        }

        public override bool Walk(ForStatement node) => LoopHandler.HandleFor(node);
        public override bool Walk(FromImportStatement node) => ImportHandler.HandleFromImport(node);
        public override bool Walk(GlobalStatement node) => NonLocalHandler.HandleGlobal(node);
        public override bool Walk(IfStatement node) => ConditionalHandler.HandleIf(node);

        public override bool Walk(ImportStatement node) => ImportHandler.HandleImport(node);
        public override bool Walk(NonlocalStatement node) => NonLocalHandler.HandleNonLocal(node);

        public override bool Walk(TryStatement node) => TryExceptHandler.HandleTryExcept(node);

        public override bool Walk(WhileStatement node) => LoopHandler.HandleWhile(node);

        public override bool Walk(WithStatement node) {
            WithHandler.HandleWith(node); // HandleWith does not walk the body.
            return base.Walk(node);
        }
        #endregion

        protected T[] GetStatements<T>(ScopeStatement s)
            => (s.Body as SuiteStatement)?.Statements.OfType<T>().ToArray() ?? Array.Empty<T>();
    }
}
