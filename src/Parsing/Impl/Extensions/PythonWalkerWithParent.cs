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

using System.Collections.Generic;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing.Extensions {
    /// <summary>
    /// base walker that gives parent node of current node visited. and provide a way to get spine nodes up to starting node
    /// </summary>
    /// <remarks>
    /// for now, done this way to make it least intrusive. 
    /// best way will be changing tree to have parent pointer.
    /// either when it construct tree or using red-green node trick (https://blog.yaakov.online/red-green-trees/).
    /// 
    /// if that is not possible, another simpler approach is passing parent (itself) in 
    /// when Node.Walk calls its child node's Walk method with one caveat where starting node can't know its parent.
    /// 
    /// if that is not possible, then this approach where walker track parents for tree. but at least put this in generated walker.
    /// </remarks>
    public abstract class PythonWalkerWithParent : PythonWalker {

        private readonly Stack<Node> _spineStack = new Stack<Node>();
        private readonly Dictionary<Node, Node> _parentMap = new Dictionary<Node, Node>();

        /// <summary>
        /// return parent node of the given node. this can only return parent of spine nodes of current node up to starting node
        /// in other words, one can use this to get parent of parent of ... but not arbitary nodes in current tree
        /// </summary>
        protected Node GetParent(Node node) => _parentMap[node];

        private Node PushSpine(Node node) {
            var parent = PeekSpine();
            _parentMap.Add(node, parent);
            _spineStack.Push(node);
            return parent;
        }

        private Node PopSpine(Node node) {
            var parent = GetParent(node);

            var removed = _spineStack.Pop();
            var peeked = PeekSpine();

            Check.InvalidOperation(removed == node, $"'{node}' and '{removed}' must be same");
            Check.InvalidOperation(parent == peeked, $"'{parent}' and '{peeked}' must be same");
            Check.InvalidOperation(_parentMap.Remove(node), $"'{node}' must exist");

            return parent;
        }

        private Node PeekSpine() => _spineStack.Count == 0 ? null : _spineStack.Peek();

        public virtual bool DefaultWalk(Node node, Node parent) { return true; }
        public virtual void DefaultPostWalk(Node node, Node parent) { }

        // AndExpression
        public sealed override bool Walk(AndExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(AndExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(AndExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(AndExpression node, Node parent) => DefaultPostWalk(node, parent);

        // AwaitExpression
        public sealed override bool Walk(AwaitExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(AwaitExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(AwaitExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(AwaitExpression node, Node parent) => DefaultPostWalk(node, parent);

        // BackQuoteExpression
        public sealed override bool Walk(BackQuoteExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(BackQuoteExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(BackQuoteExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(BackQuoteExpression node, Node parent) => DefaultPostWalk(node, parent);

        // BinaryExpression
        public sealed override bool Walk(BinaryExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(BinaryExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(BinaryExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(BinaryExpression node, Node parent) => DefaultPostWalk(node, parent);

        // CallExpression
        public sealed override bool Walk(CallExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(CallExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(CallExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(CallExpression node, Node parent) => DefaultPostWalk(node, parent);

        // ConditionalExpression
        public sealed override bool Walk(ConditionalExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ConditionalExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ConditionalExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ConditionalExpression node, Node parent) => DefaultPostWalk(node, parent);

        // ConstantExpression
        public sealed override bool Walk(ConstantExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ConstantExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ConstantExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ConstantExpression node, Node parent) => DefaultPostWalk(node, parent);

        // DictionaryComprehension
        public sealed override bool Walk(DictionaryComprehension node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(DictionaryComprehension node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(DictionaryComprehension node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(DictionaryComprehension node, Node parent) => DefaultPostWalk(node, parent);

        // DictionaryExpression
        public sealed override bool Walk(DictionaryExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(DictionaryExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(DictionaryExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(DictionaryExpression node, Node parent) => DefaultPostWalk(node, parent);

        // ErrorExpression
        public sealed override bool Walk(ErrorExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ErrorExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ErrorExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ErrorExpression node, Node parent) => DefaultPostWalk(node, parent);

        // ExpressionWithAnnotation
        public sealed override bool Walk(ExpressionWithAnnotation node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ExpressionWithAnnotation node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ExpressionWithAnnotation node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ExpressionWithAnnotation node, Node parent) => DefaultPostWalk(node, parent);

        // GeneratorExpression
        public sealed override bool Walk(GeneratorExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(GeneratorExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(GeneratorExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(GeneratorExpression node, Node parent) => DefaultPostWalk(node, parent);

        // IndexExpression
        public sealed override bool Walk(IndexExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(IndexExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(IndexExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(IndexExpression node, Node parent) => DefaultPostWalk(node, parent);

        // LambdaExpression
        public sealed override bool Walk(LambdaExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(LambdaExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(LambdaExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(LambdaExpression node, Node parent) => DefaultPostWalk(node, parent);

        // ListComprehension
        public sealed override bool Walk(ListComprehension node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ListComprehension node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ListComprehension node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ListComprehension node, Node parent) => DefaultPostWalk(node, parent);

        // ListExpression
        public sealed override bool Walk(ListExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ListExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ListExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ListExpression node, Node parent) => DefaultPostWalk(node, parent);

        // MemberExpression
        public sealed override bool Walk(MemberExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(MemberExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(MemberExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(MemberExpression node, Node parent) => DefaultPostWalk(node, parent);

        // NameExpression
        public sealed override bool Walk(NameExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(NameExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(NameExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(NameExpression node, Node parent) => DefaultPostWalk(node, parent);

        // OrExpression
        public sealed override bool Walk(OrExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(OrExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(OrExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(OrExpression node, Node parent) => DefaultPostWalk(node, parent);

        // ParenthesisExpression
        public sealed override bool Walk(ParenthesisExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ParenthesisExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ParenthesisExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ParenthesisExpression node, Node parent) => DefaultPostWalk(node, parent);

        // SetComprehension
        public sealed override bool Walk(SetComprehension node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(SetComprehension node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(SetComprehension node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(SetComprehension node, Node parent) => DefaultPostWalk(node, parent);

        // SetExpression
        public sealed override bool Walk(SetExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(SetExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(SetExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(SetExpression node, Node parent) => DefaultPostWalk(node, parent);

        // SliceExpression
        public sealed override bool Walk(SliceExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(SliceExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(SliceExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(SliceExpression node, Node parent) => DefaultPostWalk(node, parent);

        // TupleExpression
        public sealed override bool Walk(TupleExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(TupleExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(TupleExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(TupleExpression node, Node parent) => DefaultPostWalk(node, parent);

        // UnaryExpression
        public sealed override bool Walk(UnaryExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(UnaryExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(UnaryExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(UnaryExpression node, Node parent) => DefaultPostWalk(node, parent);

        // YieldExpression
        public sealed override bool Walk(YieldExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(YieldExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(YieldExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(YieldExpression node, Node parent) => DefaultPostWalk(node, parent);

        // YieldFromExpression
        public sealed override bool Walk(YieldFromExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(YieldFromExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(YieldFromExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(YieldFromExpression node, Node parent) => DefaultPostWalk(node, parent);

        // StarredExpression
        public sealed override bool Walk(StarredExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(StarredExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(StarredExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(StarredExpression node, Node parent) => DefaultPostWalk(node, parent);

        // AssertStatement
        public sealed override bool Walk(AssertStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(AssertStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(AssertStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(AssertStatement node, Node parent) => DefaultPostWalk(node, parent);

        // AssignmentStatement
        public sealed override bool Walk(AssignmentStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(AssignmentStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(AssignmentStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(AssignmentStatement node, Node parent) => DefaultPostWalk(node, parent);

        // AugmentedAssignStatement
        public sealed override bool Walk(AugmentedAssignStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(AugmentedAssignStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(AugmentedAssignStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(AugmentedAssignStatement node, Node parent) => DefaultPostWalk(node, parent);

        // BreakStatement
        public sealed override bool Walk(BreakStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(BreakStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(BreakStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(BreakStatement node, Node parent) => DefaultPostWalk(node, parent);

        // ClassDefinition
        public sealed override bool Walk(ClassDefinition node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ClassDefinition node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ClassDefinition node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ClassDefinition node, Node parent) => DefaultPostWalk(node, parent);

        // ContinueStatement
        public sealed override bool Walk(ContinueStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ContinueStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ContinueStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ContinueStatement node, Node parent) => DefaultPostWalk(node, parent);

        // DelStatement
        public sealed override bool Walk(DelStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(DelStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(DelStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(DelStatement node, Node parent) => DefaultPostWalk(node, parent);

        // EmptyStatement
        public sealed override bool Walk(EmptyStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(EmptyStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(EmptyStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(EmptyStatement node, Node parent) => DefaultPostWalk(node, parent);

        // ExecStatement
        public sealed override bool Walk(ExecStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ExecStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ExecStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ExecStatement node, Node parent) => DefaultPostWalk(node, parent);

        // ExpressionStatement
        public sealed override bool Walk(ExpressionStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ExpressionStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ExpressionStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ExpressionStatement node, Node parent) => DefaultPostWalk(node, parent);

        // ForStatement
        public sealed override bool Walk(ForStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ForStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ForStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ForStatement node, Node parent) => DefaultPostWalk(node, parent);

        // FromImportStatement
        public sealed override bool Walk(FromImportStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(FromImportStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(FromImportStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(FromImportStatement node, Node parent) => DefaultPostWalk(node, parent);

        // FunctionDefinition
        public sealed override bool Walk(FunctionDefinition node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(FunctionDefinition node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(FunctionDefinition node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(FunctionDefinition node, Node parent) => DefaultPostWalk(node, parent);

        // GlobalStatement
        public sealed override bool Walk(GlobalStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(GlobalStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(GlobalStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(GlobalStatement node, Node parent) => DefaultPostWalk(node, parent);

        // NonlocalStatement
        public sealed override bool Walk(NonlocalStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(NonlocalStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(NonlocalStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(NonlocalStatement node, Node parent) => DefaultPostWalk(node, parent);

        // IfStatement
        public sealed override bool Walk(IfStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(IfStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(IfStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(IfStatement node, Node parent) => DefaultPostWalk(node, parent);

        // ImportStatement
        public sealed override bool Walk(ImportStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ImportStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ImportStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ImportStatement node, Node parent) => DefaultPostWalk(node, parent);

        // PrintStatement
        public sealed override bool Walk(PrintStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(PrintStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(PrintStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(PrintStatement node, Node parent) => DefaultPostWalk(node, parent);

        // PythonAst
        public sealed override bool Walk(PythonAst node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(PythonAst node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(PythonAst node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(PythonAst node, Node parent) => DefaultPostWalk(node, parent);

        // RaiseStatement
        public sealed override bool Walk(RaiseStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(RaiseStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(RaiseStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(RaiseStatement node, Node parent) => DefaultPostWalk(node, parent);

        // ReturnStatement
        public sealed override bool Walk(ReturnStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ReturnStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ReturnStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ReturnStatement node, Node parent) => DefaultPostWalk(node, parent);

        // SuiteStatement
        public sealed override bool Walk(SuiteStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(SuiteStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(SuiteStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(SuiteStatement node, Node parent) => DefaultPostWalk(node, parent);

        // TryStatement
        public sealed override bool Walk(TryStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(TryStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(TryStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(TryStatement node, Node parent) => DefaultPostWalk(node, parent);

        // WhileStatement
        public sealed override bool Walk(WhileStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(WhileStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(WhileStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(WhileStatement node, Node parent) => DefaultPostWalk(node, parent);

        // WithStatement
        public sealed override bool Walk(WithStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(WithStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(WithStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(WithStatement node, Node parent) => DefaultPostWalk(node, parent);

        // WithItem
        public sealed override bool Walk(WithItem node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(WithItem node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(WithItem node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(WithItem node, Node parent) => DefaultPostWalk(node, parent);

        // Arg
        public sealed override bool Walk(Arg node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(Arg node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(Arg node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(Arg node, Node parent) => DefaultPostWalk(node, parent);

        // ComprehensionFor
        public sealed override bool Walk(ComprehensionFor node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ComprehensionFor node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ComprehensionFor node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ComprehensionFor node, Node parent) => DefaultPostWalk(node, parent);

        // ComprehensionIf
        public sealed override bool Walk(ComprehensionIf node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ComprehensionIf node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ComprehensionIf node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ComprehensionIf node, Node parent) => DefaultPostWalk(node, parent);

        // DottedName
        public sealed override bool Walk(DottedName node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(DottedName node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(DottedName node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(DottedName node, Node parent) => DefaultPostWalk(node, parent);

        // IfStatementTest
        public sealed override bool Walk(IfStatementTest node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(IfStatementTest node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(IfStatementTest node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(IfStatementTest node, Node parent) => DefaultPostWalk(node, parent);

        // ModuleName
        public sealed override bool Walk(ModuleName node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ModuleName node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ModuleName node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ModuleName node, Node parent) => DefaultPostWalk(node, parent);

        // Parameter
        public sealed override bool Walk(Parameter node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(Parameter node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(Parameter node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(Parameter node, Node parent) => DefaultPostWalk(node, parent);

        // RelativeModuleName
        public sealed override bool Walk(RelativeModuleName node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(RelativeModuleName node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(RelativeModuleName node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(RelativeModuleName node, Node parent) => DefaultPostWalk(node, parent);

        // SublistParameter
        public sealed override bool Walk(SublistParameter node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(SublistParameter node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(SublistParameter node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(SublistParameter node, Node parent) => DefaultPostWalk(node, parent);

        // TryStatementHandler
        public sealed override bool Walk(TryStatementHandler node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(TryStatementHandler node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(TryStatementHandler node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(TryStatementHandler node, Node parent) => DefaultPostWalk(node, parent);

        // ErrorStatement
        public sealed override bool Walk(ErrorStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(ErrorStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(ErrorStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(ErrorStatement node, Node parent) => DefaultPostWalk(node, parent);

        // DecoratorStatement
        public sealed override bool Walk(DecoratorStatement node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(DecoratorStatement node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(DecoratorStatement node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(DecoratorStatement node, Node parent) => DefaultPostWalk(node, parent);

        // FString
        public sealed override bool Walk(FString node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(FString node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(FString node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(FString node, Node parent) => DefaultPostWalk(node, parent);

        // FormatSpecifier
        public sealed override bool Walk(FormatSpecifier node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(FormatSpecifier node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(FormatSpecifier node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(FormatSpecifier node, Node parent) => DefaultPostWalk(node, parent);

        // FormattedValue
        public sealed override bool Walk(FormattedValue node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(FormattedValue node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(FormattedValue node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(FormattedValue node, Node parent) => DefaultPostWalk(node, parent);

        // NamedExpression
        public sealed override bool Walk(NamedExpression node) => Walk(node, PushSpine(node));
        public sealed override void PostWalk(NamedExpression node) => PostWalk(node, PopSpine(node));
        public virtual bool Walk(NamedExpression node, Node parent) => DefaultWalk(node, parent);
        public virtual void PostWalk(NamedExpression node, Node parent) => DefaultPostWalk(node, parent);
    }
}
