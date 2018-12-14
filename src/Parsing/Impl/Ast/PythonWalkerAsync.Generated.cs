// Python Tools for Visual Studio
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

using System.Threading.Tasks;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    /// <summary>
    /// PythonWalker class - The Python AST Walker (default result is true)
    /// </summary>
    public class PythonWalkerAsync {

        // AndExpression
        public virtual Task<bool> Walk(AndExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(AndExpression node) => Task.CompletedTask;

        // AwaitExpression
        public virtual Task<bool> Walk(AwaitExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(AwaitExpression node) => Task.CompletedTask;

        // BackQuoteExpression
        public virtual Task<bool> Walk(BackQuoteExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(BackQuoteExpression node) => Task.CompletedTask;

        // BinaryExpression
        public virtual Task<bool> Walk(BinaryExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(BinaryExpression node) => Task.CompletedTask;

        // CallExpression
        public virtual Task<bool> Walk(CallExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(CallExpression node) => Task.CompletedTask;

        // ConditionalExpression
        public virtual Task<bool> Walk(ConditionalExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(ConditionalExpression node) => Task.CompletedTask;

        // ConstantExpression
        public virtual Task<bool> Walk(ConstantExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(ConstantExpression node) => Task.CompletedTask;

        // DictionaryComprehension
        public virtual Task<bool> Walk(DictionaryComprehension node) => Task.FromResult(true);
        public virtual Task PostWalk(DictionaryComprehension node) => Task.CompletedTask;

        // DictionaryExpression
        public virtual Task<bool> Walk(DictionaryExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(DictionaryExpression node) => Task.CompletedTask;

        // ErrorExpression
        public virtual Task<bool> Walk(ErrorExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(ErrorExpression node) => Task.CompletedTask;

        // ExpressionWithAnnotation
        public virtual Task<bool> Walk(ExpressionWithAnnotation node) => Task.FromResult(true);
        public virtual Task PostWalk(ExpressionWithAnnotation node) => Task.CompletedTask;

        // GeneratorExpression
        public virtual Task<bool> Walk(GeneratorExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(GeneratorExpression node) => Task.CompletedTask;

        // IndexExpression
        public virtual Task<bool> Walk(IndexExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(IndexExpression node) => Task.CompletedTask;

        // LambdaExpression
        public virtual Task<bool> Walk(LambdaExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(LambdaExpression node) => Task.CompletedTask;

        // ListComprehension
        public virtual Task<bool> Walk(ListComprehension node) => Task.FromResult(true);
        public virtual Task PostWalk(ListComprehension node) => Task.CompletedTask;

        // ListExpression
        public virtual Task<bool> Walk(ListExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(ListExpression node) => Task.CompletedTask;

        // MemberExpression
        public virtual Task<bool> Walk(MemberExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(MemberExpression node) => Task.CompletedTask;

        // NameExpression
        public virtual Task<bool> Walk(NameExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(NameExpression node) => Task.CompletedTask;

        // OrExpression
        public virtual Task<bool> Walk(OrExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(OrExpression node) => Task.CompletedTask;

        // ParenthesisExpression
        public virtual Task<bool> Walk(ParenthesisExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(ParenthesisExpression node) => Task.CompletedTask;

        // SetComprehension
        public virtual Task<bool> Walk(SetComprehension node) => Task.FromResult(true);
        public virtual Task PostWalk(SetComprehension node) => Task.CompletedTask;

        // SetExpression
        public virtual Task<bool> Walk(SetExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(SetExpression node) => Task.CompletedTask;

        // SliceExpression
        public virtual Task<bool> Walk(SliceExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(SliceExpression node) => Task.CompletedTask;

        // TupleExpression
        public virtual Task<bool> Walk(TupleExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(TupleExpression node) => Task.CompletedTask;

        // UnaryExpression
        public virtual Task<bool> Walk(UnaryExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(UnaryExpression node) => Task.CompletedTask;

        // YieldExpression
        public virtual Task<bool> Walk(YieldExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(YieldExpression node) => Task.CompletedTask;

        // YieldFromExpression
        public virtual Task<bool> Walk(YieldFromExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(YieldFromExpression node) => Task.CompletedTask;

        // StarredExpression
        public virtual Task<bool> Walk(StarredExpression node) => Task.FromResult(true);
        public virtual Task PostWalk(StarredExpression node) => Task.CompletedTask;

        // AssertStatement
        public virtual Task<bool> Walk(AssertStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(AssertStatement node) => Task.CompletedTask;

        // AssignmentStatement
        public virtual Task<bool> Walk(AssignmentStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(AssignmentStatement node) => Task.CompletedTask;

        // AugmentedAssignStatement
        public virtual Task<bool> Walk(AugmentedAssignStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(AugmentedAssignStatement node) => Task.CompletedTask;

        // BreakStatement
        public virtual Task<bool> Walk(BreakStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(BreakStatement node) => Task.CompletedTask;

        // ClassDefinition
        public virtual Task<bool> Walk(ClassDefinition node) => Task.FromResult(true);
        public virtual Task PostWalk(ClassDefinition node) => Task.CompletedTask;

        // ContinueStatement
        public virtual Task<bool> Walk(ContinueStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(ContinueStatement node) => Task.CompletedTask;

        // DelStatement
        public virtual Task<bool> Walk(DelStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(DelStatement node) => Task.CompletedTask;

        // EmptyStatement
        public virtual Task<bool> Walk(EmptyStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(EmptyStatement node) => Task.CompletedTask;

        // ExecStatement
        public virtual Task<bool> Walk(ExecStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(ExecStatement node) => Task.CompletedTask;

        // ExpressionStatement
        public virtual Task<bool> Walk(ExpressionStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(ExpressionStatement node) => Task.CompletedTask;

        // ForStatement
        public virtual Task<bool> Walk(ForStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(ForStatement node) => Task.CompletedTask;

        // FromImportStatement
        public virtual Task<bool> Walk(FromImportStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(FromImportStatement node) => Task.CompletedTask;

        // FunctionDefinition
        public virtual Task<bool> Walk(FunctionDefinition node) => Task.FromResult(true);
        public virtual Task PostWalk(FunctionDefinition node) => Task.CompletedTask;

        // GlobalStatement
        public virtual Task<bool> Walk(GlobalStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(GlobalStatement node) => Task.CompletedTask;

        // NonlocalStatement
        public virtual Task<bool> Walk(NonlocalStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(NonlocalStatement node) => Task.CompletedTask;

        // IfStatement
        public virtual Task<bool> Walk(IfStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(IfStatement node) => Task.CompletedTask;

        // ImportStatement
        public virtual Task<bool> Walk(ImportStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(ImportStatement node) => Task.CompletedTask;

        // PrintStatement
        public virtual Task<bool> Walk(PrintStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(PrintStatement node) => Task.CompletedTask;

        // PythonAst
        public virtual Task<bool> Walk(PythonAst node) => Task.FromResult(true);
        public virtual Task PostWalk(PythonAst node) => Task.CompletedTask;

        // RaiseStatement
        public virtual Task<bool> Walk(RaiseStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(RaiseStatement node) => Task.CompletedTask;

        // ReturnStatement
        public virtual Task<bool> Walk(ReturnStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(ReturnStatement node) => Task.CompletedTask;

        // SuiteStatement
        public virtual Task<bool> Walk(SuiteStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(SuiteStatement node) => Task.CompletedTask;

        // TryStatement
        public virtual Task<bool> Walk(TryStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(TryStatement node) => Task.CompletedTask;

        // WhileStatement
        public virtual Task<bool> Walk(WhileStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(WhileStatement node) => Task.CompletedTask;

        // WithStatement
        public virtual Task<bool> Walk(WithStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(WithStatement node) => Task.CompletedTask;

        // WithItem
        public virtual Task<bool> Walk(WithItem node) => Task.FromResult(true);
        public virtual Task PostWalk(WithItem node) => Task.CompletedTask;

        // Arg
        public virtual Task<bool> Walk(Arg node) => Task.FromResult(true);
        public virtual Task PostWalk(Arg node) => Task.CompletedTask;

        // ComprehensionFor
        public virtual Task<bool> Walk(ComprehensionFor node) => Task.FromResult(true);
        public virtual Task PostWalk(ComprehensionFor node) => Task.CompletedTask;

        // ComprehensionIf
        public virtual Task<bool> Walk(ComprehensionIf node) => Task.FromResult(true);
        public virtual Task PostWalk(ComprehensionIf node) => Task.CompletedTask;

        // DottedName
        public virtual Task<bool> Walk(DottedName node) => Task.FromResult(true);
        public virtual Task PostWalk(DottedName node) => Task.CompletedTask;

        // IfStatementTest
        public virtual Task<bool> Walk(IfStatementTest node) => Task.FromResult(true);
        public virtual Task PostWalk(IfStatementTest node) => Task.CompletedTask;

        // ModuleName
        public virtual Task<bool> Walk(ModuleName node) => Task.FromResult(true);
        public virtual Task PostWalk(ModuleName node) => Task.CompletedTask;

        // Parameter
        public virtual Task<bool> Walk(Parameter node) => Task.FromResult(true);
        public virtual Task PostWalk(Parameter node) => Task.CompletedTask;

        // RelativeModuleName
        public virtual Task<bool> Walk(RelativeModuleName node) => Task.FromResult(true);
        public virtual Task PostWalk(RelativeModuleName node) => Task.CompletedTask;

        // SublistParameter
        public virtual Task<bool> Walk(SublistParameter node) => Task.FromResult(true);
        public virtual Task PostWalk(SublistParameter node) => Task.CompletedTask;

        // TryStatementHandler
        public virtual Task<bool> Walk(TryStatementHandler node) => Task.FromResult(true);
        public virtual Task PostWalk(TryStatementHandler node) => Task.CompletedTask;

        // ErrorStatement
        public virtual Task<bool> Walk(ErrorStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(ErrorStatement node) => Task.CompletedTask;

        // DecoratorStatement
        public virtual Task<bool> Walk(DecoratorStatement node) => Task.FromResult(true);
        public virtual Task PostWalk(DecoratorStatement node) => Task.CompletedTask;
    }


    /// <summary>
    /// PythonWalkerNonRecursive class - The Python AST Walker (default result is false)
    /// </summary>
    public class PythonWalkerNonRecursiveAsync : PythonWalkerAsync {
        // AndExpression
        public override Task<bool> Walk(AndExpression node) => Task.FromResult(false);
        public override Task PostWalk(AndExpression node) => Task.CompletedTask;

        // AndExpression
        public override Task<bool> Walk(AwaitExpression node) => Task.FromResult(false);
        public override Task PostWalk(AwaitExpression node) => Task.CompletedTask;

        // BackQuoteExpression
        public override Task<bool> Walk(BackQuoteExpression node) => Task.FromResult(false);
        public override Task PostWalk(BackQuoteExpression node) => Task.CompletedTask;

        // BinaryExpression
        public override Task<bool> Walk(BinaryExpression node) => Task.FromResult(false);
        public override Task PostWalk(BinaryExpression node) => Task.CompletedTask;

        // CallExpression
        public override Task<bool> Walk(CallExpression node) => Task.FromResult(false);
        public override Task PostWalk(CallExpression node) => Task.CompletedTask;

        // ConditionalExpression
        public override Task<bool> Walk(ConditionalExpression node) => Task.FromResult(false);
        public override Task PostWalk(ConditionalExpression node) => Task.CompletedTask;

        // ConstantExpression
        public override Task<bool> Walk(ConstantExpression node) => Task.FromResult(false);
        public override Task PostWalk(ConstantExpression node) => Task.CompletedTask;

        // DictionaryComprehension
        public override Task<bool> Walk(DictionaryComprehension node) => Task.FromResult(false);
        public override Task PostWalk(DictionaryComprehension node) => Task.CompletedTask;

        // DictionaryExpression
        public override Task<bool> Walk(DictionaryExpression node) => Task.FromResult(false);
        public override Task PostWalk(DictionaryExpression node) => Task.CompletedTask;

        // ErrorExpression
        public override Task<bool> Walk(ErrorExpression node) => Task.FromResult(false);
        public override Task PostWalk(ErrorExpression node) => Task.CompletedTask;

        // ExpressionWithAnnotation
        public override Task<bool> Walk(ExpressionWithAnnotation node) => Task.FromResult(false);
        public override Task PostWalk(ExpressionWithAnnotation node) => Task.CompletedTask;

        // GeneratorExpression
        public override Task<bool> Walk(GeneratorExpression node) => Task.FromResult(false);
        public override Task PostWalk(GeneratorExpression node) => Task.CompletedTask;

        // IndexExpression
        public override Task<bool> Walk(IndexExpression node) => Task.FromResult(false);
        public override Task PostWalk(IndexExpression node) => Task.CompletedTask;

        // LambdaExpression
        public override Task<bool> Walk(LambdaExpression node) => Task.FromResult(false);
        public override Task PostWalk(LambdaExpression node) => Task.CompletedTask;

        // ListComprehension
        public override Task<bool> Walk(ListComprehension node) => Task.FromResult(false);
        public override Task PostWalk(ListComprehension node) => Task.CompletedTask;

        // ListExpression
        public override Task<bool> Walk(ListExpression node) => Task.FromResult(false);
        public override Task PostWalk(ListExpression node) => Task.CompletedTask;

        // MemberExpression
        public override Task<bool> Walk(MemberExpression node) => Task.FromResult(false);
        public override Task PostWalk(MemberExpression node) => Task.CompletedTask;

        // NameExpression
        public override Task<bool> Walk(NameExpression node) => Task.FromResult(false);
        public override Task PostWalk(NameExpression node) => Task.CompletedTask;

        // OrExpression
        public override Task<bool> Walk(OrExpression node) => Task.FromResult(false);
        public override Task PostWalk(OrExpression node) => Task.CompletedTask;

        // ParenthesisExpression
        public override Task<bool> Walk(ParenthesisExpression node) => Task.FromResult(false);
        public override Task PostWalk(ParenthesisExpression node) => Task.CompletedTask;

        // SetComprehension
        public override Task<bool> Walk(SetComprehension node) => Task.FromResult(false);
        public override Task PostWalk(SetComprehension node) => Task.CompletedTask;

        // SetExpression
        public override Task<bool> Walk(SetExpression node) => Task.FromResult(false);
        public override Task PostWalk(SetExpression node) => Task.CompletedTask;

        // SliceExpression
        public override Task<bool> Walk(SliceExpression node) => Task.FromResult(false);
        public override Task PostWalk(SliceExpression node) => Task.CompletedTask;

        // TupleExpression
        public override Task<bool> Walk(TupleExpression node) => Task.FromResult(false);
        public override Task PostWalk(TupleExpression node) => Task.CompletedTask;

        // UnaryExpression
        public override Task<bool> Walk(UnaryExpression node) => Task.FromResult(false);
        public override Task PostWalk(UnaryExpression node) => Task.CompletedTask;

        // YieldExpression
        public override Task<bool> Walk(YieldExpression node) => Task.FromResult(false);
        public override Task PostWalk(YieldExpression node) => Task.CompletedTask;

        // YieldFromExpression
        public override Task<bool> Walk(YieldFromExpression node) => Task.FromResult(false);
        public override Task PostWalk(YieldFromExpression node) => Task.CompletedTask;

        // StarredExpression
        public override Task<bool> Walk(StarredExpression node) => Task.FromResult(false);
        public override Task PostWalk(StarredExpression node) => Task.CompletedTask;

        // AssertStatement
        public override Task<bool> Walk(AssertStatement node) => Task.FromResult(false);
        public override Task PostWalk(AssertStatement node) => Task.CompletedTask;

        // AssignmentStatement
        public override Task<bool> Walk(AssignmentStatement node) => Task.FromResult(false);
        public override Task PostWalk(AssignmentStatement node) => Task.CompletedTask;

        // AugmentedAssignStatement
        public override Task<bool> Walk(AugmentedAssignStatement node) => Task.FromResult(false);
        public override Task PostWalk(AugmentedAssignStatement node) => Task.CompletedTask;

        // BreakStatement
        public override Task<bool> Walk(BreakStatement node) => Task.FromResult(false);
        public override Task PostWalk(BreakStatement node) => Task.CompletedTask;

        // ClassDefinition
        public override Task<bool> Walk(ClassDefinition node) => Task.FromResult(false);
        public override Task PostWalk(ClassDefinition node) => Task.CompletedTask;

        // ContinueStatement
        public override Task<bool> Walk(ContinueStatement node) => Task.FromResult(false);
        public override Task PostWalk(ContinueStatement node) => Task.CompletedTask;

        // DelStatement
        public override Task<bool> Walk(DelStatement node) => Task.FromResult(false);
        public override Task PostWalk(DelStatement node) => Task.CompletedTask;

        // EmptyStatement
        public override Task<bool> Walk(EmptyStatement node) => Task.FromResult(false);
        public override Task PostWalk(EmptyStatement node) => Task.CompletedTask;

        // ExecStatement
        public override Task<bool> Walk(ExecStatement node) => Task.FromResult(false);
        public override Task PostWalk(ExecStatement node) => Task.CompletedTask;

        // ExpressionStatement
        public override Task<bool> Walk(ExpressionStatement node) => Task.FromResult(false);
        public override Task PostWalk(ExpressionStatement node) => Task.CompletedTask;

        // ForStatement
        public override Task<bool> Walk(ForStatement node) => Task.FromResult(false);
        public override Task PostWalk(ForStatement node) => Task.CompletedTask;

        // FromImportStatement
        public override Task<bool> Walk(FromImportStatement node) => Task.FromResult(false);
        public override Task PostWalk(FromImportStatement node) => Task.CompletedTask;

        // FunctionDefinition
        public override Task<bool> Walk(FunctionDefinition node) => Task.FromResult(false);
        public override Task PostWalk(FunctionDefinition node) => Task.CompletedTask;

        // GlobalStatement
        public override Task<bool> Walk(GlobalStatement node) => Task.FromResult(false);
        public override Task PostWalk(GlobalStatement node) => Task.CompletedTask;

        // NonlocalStatement
        public override Task<bool> Walk(NonlocalStatement node) => Task.FromResult(false);
        public override Task PostWalk(NonlocalStatement node) => Task.CompletedTask;

        // IfStatement
        public override Task<bool> Walk(IfStatement node) => Task.FromResult(false);
        public override Task PostWalk(IfStatement node) => Task.CompletedTask;

        // ImportStatement
        public override Task<bool> Walk(ImportStatement node) => Task.FromResult(false);
        public override Task PostWalk(ImportStatement node) => Task.CompletedTask;

        // PrintStatement
        public override Task<bool> Walk(PrintStatement node) => Task.FromResult(false);
        public override Task PostWalk(PrintStatement node) => Task.CompletedTask;

        // PythonAst
        public override Task<bool> Walk(PythonAst node) => Task.FromResult(false);
        public override Task PostWalk(PythonAst node) => Task.CompletedTask;

        // RaiseStatement
        public override Task<bool> Walk(RaiseStatement node) => Task.FromResult(false);
        public override Task PostWalk(RaiseStatement node) => Task.CompletedTask;

        // ReturnStatement
        public override Task<bool> Walk(ReturnStatement node) => Task.FromResult(false);
        public override Task PostWalk(ReturnStatement node) => Task.CompletedTask;

        // SuiteStatement
        public override Task<bool> Walk(SuiteStatement node) => Task.FromResult(false);
        public override Task PostWalk(SuiteStatement node) => Task.CompletedTask;

        // TryStatement
        public override Task<bool> Walk(TryStatement node) => Task.FromResult(false);
        public override Task PostWalk(TryStatement node) => Task.CompletedTask;

        // WhileStatement
        public override Task<bool> Walk(WhileStatement node) => Task.FromResult(false);
        public override Task PostWalk(WhileStatement node) => Task.CompletedTask;

        // WithStatement
        public override Task<bool> Walk(WithStatement node) => Task.FromResult(false);
        public override Task PostWalk(WithStatement node) => Task.CompletedTask;

        // WithItem
        public override Task<bool> Walk(WithItem node) => Task.FromResult(false);
        public override Task PostWalk(WithItem node) => Task.CompletedTask;

        // Arg
        public override Task<bool> Walk(Arg node) => Task.FromResult(false);
        public override Task PostWalk(Arg node) => Task.CompletedTask;

        // ComprehensionFor
        public override Task<bool> Walk(ComprehensionFor node) => Task.FromResult(false);
        public override Task PostWalk(ComprehensionFor node) => Task.CompletedTask;

        // ComprehensionIf
        public override Task<bool> Walk(ComprehensionIf node) => Task.FromResult(false);
        public override Task PostWalk(ComprehensionIf node) => Task.CompletedTask;

        // DottedName
        public override Task<bool> Walk(DottedName node) => Task.FromResult(false);
        public override Task PostWalk(DottedName node) => Task.CompletedTask;

        // IfStatementTest
        public override Task<bool> Walk(IfStatementTest node) => Task.FromResult(false);
        public override Task PostWalk(IfStatementTest node) => Task.CompletedTask;

        // ModuleName
        public override Task<bool> Walk(ModuleName node) => Task.FromResult(false);
        public override Task PostWalk(ModuleName node) => Task.CompletedTask;

        // Parameter
        public override Task<bool> Walk(Parameter node) => Task.FromResult(false);
        public override Task PostWalk(Parameter node) => Task.CompletedTask;

        // RelativeModuleName
        public override Task<bool> Walk(RelativeModuleName node) => Task.FromResult(false);
        public override Task PostWalk(RelativeModuleName node) => Task.CompletedTask;

        // SublistParameter
        public override Task<bool> Walk(SublistParameter node) => Task.FromResult(false);
        public override Task PostWalk(SublistParameter node) => Task.CompletedTask;

        // TryStatementHandler
        public override Task<bool> Walk(TryStatementHandler node) => Task.FromResult(false);
        public override Task PostWalk(TryStatementHandler node) => Task.CompletedTask;

        // ErrorStatement
        public override Task<bool> Walk(ErrorStatement node) => Task.FromResult(false);
        public override Task PostWalk(ErrorStatement node) => Task.CompletedTask;

        // DecoratorStatement
        public override Task<bool> Walk(DecoratorStatement node) => Task.FromResult(false);
        public override Task PostWalk(DecoratorStatement node) => Task.CompletedTask;
    }

    /// <summary>
    /// PythonWalkerWithLocation class - The Python AST Walker (default result
    /// is true if the node contains Location, otherwise false)
    /// </summary>
    public class PythonWalkerWithLocationAsync : PythonWalkerAsync {
        public readonly int Location;

        private SourceLocation _loc = SourceLocation.Invalid;

        public PythonWalkerWithLocationAsync(int location) {
            Location = location;
        }

        /// <summary>
        /// Required when ExtendedStatements is set.
        /// </summary>
        public PythonAst Tree { get; set; }

        /// <summary>
        /// When enabled, statements will be walked if Location is on the same line.
        /// Note that this may walk multiple statements if they are on the same line. Ensure
        /// your walker state can handle this!
        /// </summary>
        public bool ExtendedStatements { get; set; }

        private bool Contains(Statement stmt) {
            if (Location < stmt.StartIndex) {
                return false;
            }
            if (Location <= stmt.EndIndex) {
                return true;
            }
            if (!ExtendedStatements || Tree == null) {
                return false;
            }
            if (!_loc.IsValid) {
                _loc = Tree.IndexToLocation(Location);
            }
            var start = Tree.IndexToLocation(stmt.StartIndex);
            return _loc.Line == start.Line && _loc.Column > start.Column;
        }

        // AndExpression
        public override Task<bool> Walk(AndExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // AndExpression
        public override Task<bool> Walk(AwaitExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // BackQuoteExpression
        public override Task<bool> Walk(BackQuoteExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // BinaryExpression
        public override Task<bool> Walk(BinaryExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // CallExpression
        public override Task<bool> Walk(CallExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ConditionalExpression
        public override Task<bool> Walk(ConditionalExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ConstantExpression
        public override Task<bool> Walk(ConstantExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // DictionaryComprehension
        public override Task<bool> Walk(DictionaryComprehension node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // DictionaryExpression
        public override Task<bool> Walk(DictionaryExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ErrorExpression
        public override Task<bool> Walk(ErrorExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ExpressionWithAnnotation
        public override Task<bool> Walk(ExpressionWithAnnotation node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // GeneratorExpression
        public override Task<bool> Walk(GeneratorExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // IndexExpression
        public override Task<bool> Walk(IndexExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // LambdaExpression
        public override Task<bool> Walk(LambdaExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ListComprehension
        public override Task<bool> Walk(ListComprehension node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ListExpression
        public override Task<bool> Walk(ListExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // MemberExpression
        public override Task<bool> Walk(MemberExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // NameExpression
        public override Task<bool> Walk(NameExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // OrExpression
        public override Task<bool> Walk(OrExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ParenthesisExpression
        public override Task<bool> Walk(ParenthesisExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SetComprehension
        public override Task<bool> Walk(SetComprehension node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SetExpression
        public override Task<bool> Walk(SetExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SliceExpression
        public override Task<bool> Walk(SliceExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // TupleExpression
        public override Task<bool> Walk(TupleExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // UnaryExpression
        public override Task<bool> Walk(UnaryExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // YieldExpression
        public override Task<bool> Walk(YieldExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // YieldFromExpression
        public override Task<bool> Walk(YieldFromExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // StarredExpression
        public override Task<bool> Walk(StarredExpression node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // AssertStatement
        public override Task<bool> Walk(AssertStatement node) => Task.FromResult(Contains(node));

        // AssignmentStatement
        public override Task<bool> Walk(AssignmentStatement node) => Task.FromResult(Contains(node));

        // AugmentedAssignStatement
        public override Task<bool> Walk(AugmentedAssignStatement node) => Task.FromResult(Contains(node));

        // BreakStatement
        public override Task<bool> Walk(BreakStatement node) => Task.FromResult(Contains(node));

        // ClassDefinition
        public override Task<bool> Walk(ClassDefinition node) => Task.FromResult(Contains(node));

        // ContinueStatement
        public override Task<bool> Walk(ContinueStatement node) => Task.FromResult(Contains(node));

        // DelStatement
        public override Task<bool> Walk(DelStatement node) => Task.FromResult(Contains(node));

        // EmptyStatement
        public override Task<bool> Walk(EmptyStatement node) => Task.FromResult(Contains(node));

        // ExecStatement
        public override Task<bool> Walk(ExecStatement node) => Task.FromResult(Contains(node));

        // ExpressionStatement
        public override Task<bool> Walk(ExpressionStatement node) => Task.FromResult(Contains(node));

        // ForStatement
        public override Task<bool> Walk(ForStatement node) => Task.FromResult(Contains(node));

        // FromImportStatement
        public override Task<bool> Walk(FromImportStatement node) => Task.FromResult(Contains(node));

        // FunctionDefinition
        public override Task<bool> Walk(FunctionDefinition node) => Task.FromResult(Contains(node));

        // GlobalStatement
        public override Task<bool> Walk(GlobalStatement node) => Task.FromResult(Contains(node));

        // NonlocalStatement
        public override Task<bool> Walk(NonlocalStatement node) => Task.FromResult(Contains(node));

        // IfStatement
        public override Task<bool> Walk(IfStatement node) => Task.FromResult(Contains(node));

        // ImportStatement
        public override Task<bool> Walk(ImportStatement node) => Task.FromResult(Contains(node));

        // PrintStatement
        public override Task<bool> Walk(PrintStatement node) => Task.FromResult(Contains(node));

        // PythonAst
        public override Task<bool> Walk(PythonAst node) => Task.FromResult(Contains(node));

        // RaiseStatement
        public override Task<bool> Walk(RaiseStatement node) => Task.FromResult(Contains(node));

        // ReturnStatement
        public override Task<bool> Walk(ReturnStatement node) => Task.FromResult(Contains(node));

        // SuiteStatement
        public override Task<bool> Walk(SuiteStatement node) => Task.FromResult(Contains(node));

        // TryStatement
        public override Task<bool> Walk(TryStatement node) => Task.FromResult(Contains(node));

        // WhileStatement
        public override Task<bool> Walk(WhileStatement node) => Task.FromResult(Contains(node));

        // WithStatement
        public override Task<bool> Walk(WithStatement node) => Task.FromResult(Contains(node));

        // WithItem
        public override Task<bool> Walk(WithItem node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // Arg
        public override Task<bool> Walk(Arg node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ComprehensionFor
        public override Task<bool> Walk(ComprehensionFor node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ComprehensionIf
        public override Task<bool> Walk(ComprehensionIf node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // DottedName
        public override Task<bool> Walk(DottedName node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // IfStatementTest
        public override Task<bool> Walk(IfStatementTest node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ModuleName
        public override Task<bool> Walk(ModuleName node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // Parameter
        public override Task<bool> Walk(Parameter node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // RelativeModuleName
        public override Task<bool> Walk(RelativeModuleName node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SublistParameter
        public override Task<bool> Walk(SublistParameter node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // TryStatementHandler
        public override Task<bool> Walk(TryStatementHandler node) => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ErrorStatement
        public override Task<bool> Walk(ErrorStatement node) => Task.FromResult(Contains(node));

        // DecoratorStatement
        public override Task<bool> Walk(DecoratorStatement node) => Task.FromResult(Contains(node));
    }

}
