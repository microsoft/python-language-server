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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Documentation;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Implementation {
    class CompletionAnalysis {
        private readonly Node _statement;
        private readonly ScopeStatement _scope;
        private readonly ILogger _log;
        private readonly DocumentationBuilder _textBuilder;
        private readonly Func<TextReader> _openDocument;
        private readonly bool _addBrackets;

        public CompletionAnalysis(
            IModuleAnalysis analysis,
            PythonAst tree,
            SourceLocation position,
            GetMemberOptions opts,
            ServerSettings.PythonCompletionOptions completionSettings,
            DocumentationBuilder textBuilder,
            ILogger log,
            Func<TextReader> openDocument
        ) {
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            Position = position;
            Index = Tree.LocationToIndex(Position);
            Options = opts;
            _textBuilder = textBuilder;
            _log = log;
            _openDocument = openDocument;
            _addBrackets = completionSettings.addBrackets;

            var finder = new ExpressionFinder(Tree, new GetExpressionOptions {
                Names = true,
                Members = true,
                NamedArgumentNames = true,
                ImportNames = true,
                ImportAsNames = true,
                Literals = true,
                Errors = true
            });

            finder.Get(Index, Index, out var node, out _statement, out _scope);

            var index = Index;
            var col = Position.Column;
            while (CanBackUp(Tree, node, _statement, _scope, col)) {
                col -= 1;
                index -= 1;
                finder.Get(index, index, out node, out _statement, out _scope);
            }

            Node = node ?? (_statement as ExpressionStatement)?.Expression;
        }

        private static bool CanBackUp(PythonAst tree, Node node, Node statement, ScopeStatement scope, int column) {
            if (node != null || !((statement as ExpressionStatement)?.Expression is ErrorExpression)) {
                return false;
            }

            var top = 1;
            if (scope != null) {
                var scopeStart = scope.GetStart(tree);
                if (scope.Body != null) {
                    top = scope.Body.GetEnd(tree).Line == scopeStart.Line
                        ? scope.Body.GetStart(tree).Column
                        : scopeStart.Column;
                } else {
                    top = scopeStart.Column;
                }
            }

            return column > top;
        }

        private static readonly IEnumerable<CompletionItem> Empty = Enumerable.Empty<CompletionItem>();

        public IModuleAnalysis Analysis { get; }
        public PythonAst Tree { get; }
        public SourceLocation Position { get; }
        public int Index { get; }
        public GetMemberOptions Options { get; set; }
        public SourceSpan? ApplicableSpan { get; set; }

        public bool? ShouldCommitByDefault { get; set; }
        public bool? ShouldAllowSnippets { get; set; }

        public Node Node { get; private set; }
        public Node Statement => _statement;
        public ScopeStatement Scope => _scope;

        /// <summary>
        /// The node that members were returned for, if any.
        /// </summary>
        public Expression ParentExpression { get; private set; }


        private IReadOnlyList<KeyValuePair<IndexSpan, Token>> _tokens;
        private NewLineLocation[] _tokenNewlines;

        private IEnumerable<KeyValuePair<IndexSpan, Token>> Tokens {
            get {
                EnsureTokens();
                return _tokens;
            }
        }

        private SourceSpan GetTokenSpan(IndexSpan span) {
            EnsureTokens();
            return new SourceSpan(
                NewLineLocation.IndexToLocation(_tokenNewlines, span.Start),
                NewLineLocation.IndexToLocation(_tokenNewlines, span.End)
            );
        }

        private void EnsureTokens() {
            if (_tokens != null) {
                return;
            }

            var reader = _openDocument?.Invoke();
            if (reader == null) {
                _log.TraceMessage($"Cannot get completions at error node without sources");
                _tokens = Array.Empty<KeyValuePair<IndexSpan, Token>>();
                _tokenNewlines = Array.Empty<NewLineLocation>();
                return;
            }

            var tokens = new List<KeyValuePair<IndexSpan, Token>>();
            Tokenizer tokenizer;
            using (reader) {
                tokenizer = new Tokenizer(Tree.LanguageVersion, options: TokenizerOptions.GroupingRecovery);
                tokenizer.Initialize(reader);
                for (var t = tokenizer.GetNextToken();
                    t.Kind != TokenKind.EndOfFile && tokenizer.TokenSpan.Start < Index;
                    t = tokenizer.GetNextToken()) {
                    tokens.Add(new KeyValuePair<IndexSpan, Token>(tokenizer.TokenSpan, t));
                }
            }

            _tokens = tokens;
            _tokenNewlines = tokenizer.GetLineLocations();
        }


        public IEnumerable<CompletionItem> GetCompletionsFromString(string expr) {
            Check.ArgumentNotNullOrEmpty(nameof(expr), expr);
            _log.TraceMessage($"Completing expression '{expr}'");
            return Analysis.GetMembers(expr, Position, Options).Select(ToCompletionItem);
        }

        public IEnumerable<CompletionItem> GetCompletions() {
            switch (Node) {
                case MemberExpression me when me.Target != null && me.DotIndex > me.StartIndex && Index > me.DotIndex:
                    return GetCompletionsFromMembers(me);
                case ConstantExpression ce when ce.Value != null:
                case null when IsInsideComment():
                    return null;
            }

            switch (Statement) {
                case ImportStatement import when TryGetCompletionsInImport(import, out var result):
                    return result;
                case FromImportStatement fromImport when TryGetCompletionsInFromImport(fromImport, out var result):
                    return result;
                case FunctionDefinition fd when TryGetCompletionsForOverride(fd, out var result):
                    return result;
                case FunctionDefinition fd when NoCompletionsInFunctionDefinition(fd):
                    return null;
                case ClassDefinition cd:
                    if (NoCompletionsInClassDefinition(cd, out var addMetadataArg)) {
                        return null;
                    }

                    return addMetadataArg
                        ? GetCompletionsFromTopLevel().Append(MetadataArgCompletion)
                        : GetCompletionsFromTopLevel();

                case ForStatement forStatement when TryGetCompletionsInForStatement(forStatement, out var result):
                    return result;
                case WithStatement withStatement when TryGetCompletionsInWithStatement(withStatement, out var result):
                    return result;
                case RaiseStatement raiseStatement when TryGetCompletionsInRaiseStatement(raiseStatement, out var result):
                    return result;
                case TryStatementHandler tryStatement when TryGetCompletionsInExceptStatement(tryStatement, out var result):
                    return result;
                default:
                    return GetCompletionsFromError() ?? GetCompletionsFromTopLevel();
            }
        }

        private static IEnumerable<CompletionItem> Once(CompletionItem item) {
            yield return item;
        }

        private static IEnumerable<(T1, T2)> ZipLongest<T1, T2>(IEnumerable<T1> src1, IEnumerable<T2> src2) {
            using (var e1 = src1?.GetEnumerator())
            using (var e2 = src2?.GetEnumerator()) {
                bool b1 = e1?.MoveNext() ?? false, b2 = e2?.MoveNext() ?? false;
                while (b1 && b2) {
                    yield return (e1.Current, e2.Current);
                    b1 = e1.MoveNext();
                    b2 = e2.MoveNext();
                }

                while (b1) {
                    yield return (e1.Current, default(T2));
                    b1 = e1.MoveNext();
                }

                while (b2) {
                    yield return (default(T1), e2.Current);
                    b2 = e2.MoveNext();
                }
            }
        }

        private IEnumerable<CompletionItem> GetCompletionsFromMembers(MemberExpression me) {
            _log.TraceMessage(
                $"Completing expression {me.Target.ToCodeString(Tree, CodeFormattingOptions.Traditional)}");
            ParentExpression = me.Target;
            if (!string.IsNullOrEmpty(me.Name)) {
                Node = new NameExpression(me.Name);
                Node.SetLoc(me.NameHeader, me.NameHeader + me.Name.Length);
            } else {
                Node = null;
            }

            ShouldCommitByDefault = true;
            return Analysis.GetMembers(me.Target, Position, Options | GetMemberOptions.ForEval)
                .Select(ToCompletionItem);
        }

        private IEnumerable<CompletionItem> GetModules(string[] names, bool includeMembers) {
            if (names.Any()) {
                return Analysis.ProjectState
                    .GetModuleMembers(Analysis.InterpreterContext, names, includeMembers)
                    .Select(ToCompletionItem);
            }

            return GetModules();
        }

        private IEnumerable<CompletionItem> GetModules()
            => Analysis.ProjectState.GetModules().Select(ToCompletionItem);

        private IEnumerable<CompletionItem> GetModulesFromNode(DottedName name, bool includeMembers = false) => GetModules(GetNamesFromDottedName(name), includeMembers);

        private string[] GetNamesFromDottedName(DottedName name) => name.Names.TakeWhile(n => Index > n.EndIndex).Select(n => n.Name).ToArray();

        private void SetApplicableSpanToLastToken(Node containingNode) {
            if (containingNode != null && Index >= containingNode.EndIndex) {
                var token = Tokens.LastOrDefault();
                if (token.Key.End >= Index) {
                    ApplicableSpan = GetTokenSpan(token.Key);
                }
            }
        }

        private bool TryGetCompletionsInImport(ImportStatement import, out IEnumerable<CompletionItem> result) {
            result = null;

            // No names, so if we're at the end return modules
            if (import.Names.Count == 0 && Index > import.KeywordEndIndex) {
                result = GetModules();
                return true;
            }

            foreach (var (name, asName) in ZipLongest(import.Names, import.AsNames).Reverse()) {
                if (asName != null && Index >= asName.StartIndex) {
                    return true;
                }

                if (name != null && Index >= name.StartIndex) {
                    if (Index > name.EndIndex && name.EndIndex > name.StartIndex) {
                        SetApplicableSpanToLastToken(import);
                        result = Once(AsKeywordCompletion);
                    } else {
                        Node = name.Names.LastOrDefault(n => n.StartIndex <= Index && Index <= n.EndIndex);
                        result = GetModulesFromNode(name);
                    }

                    return true;
                }
            }

            return false;
        }

        private bool TryGetCompletionsInFromImport(FromImportStatement fromImport, out IEnumerable<CompletionItem> result) {
            result = null;

            // No more completions after '*', ever!
            if (fromImport.Names != null && fromImport.Names.Any(n => n?.Name == "*" && Index > n.EndIndex)) {
                return true;
            }

            foreach (var (name, asName) in ZipLongest(fromImport.Names, fromImport.AsNames).Reverse()) {
                if (asName != null && Index >= asName.StartIndex) {
                    return true;
                }

                if (name != null) {
                    if (Index > name.EndIndex && name.EndIndex > name.StartIndex) {
                        SetApplicableSpanToLastToken(fromImport);
                        result = Once(AsKeywordCompletion);
                        return true;
                    }

                    if (Index >= name.StartIndex) {
                        ApplicableSpan = name.GetSpan(Tree);
                        var mods = GetModulesFromNode(fromImport.Root, true).ToArray();
                        result = mods.Any() && fromImport.Names.Count == 1 ? Once(StarCompletion).Concat(mods) : mods;
                        return true;
                    }
                }
            }

            if (fromImport.ImportIndex > fromImport.StartIndex) {
                if (Index > fromImport.ImportIndex + 6 && Analysis.Scope.AnalysisValue is ModuleInfo moduleInfo) {
                    var root = fromImport.Root;
                    var rootName = root.MakeString();
                    if (moduleInfo.TryGetModuleReference(rootName, out var moduleReference)) {
                        var moduleMembers = PythonAnalyzer.GetModuleMembers(Analysis.InterpreterContext, GetNamesFromDottedName(root), true, moduleReference.Module);
                        result = Once(StarCompletion).Concat(moduleMembers.Select(ToCompletionItem));
                    }
                    return true;
                }

                if (Index >= fromImport.ImportIndex) {
                    ApplicableSpan = new SourceSpan(
                        Tree.IndexToLocation(fromImport.ImportIndex),
                        Tree.IndexToLocation(Math.Min(fromImport.ImportIndex + 6, fromImport.EndIndex))
                    );
                    result = Once(ImportKeywordCompletion);
                    return true;
                }
            }

            if (Index > fromImport.Root.EndIndex && fromImport.Root.EndIndex > fromImport.Root.StartIndex) {
                if (Index > fromImport.EndIndex) {
                    // Only end up here for "from ... imp", and "imp" is not counted
                    // as part of our span
                    var token = Tokens.LastOrDefault();
                    if (token.Key.End >= Index) {
                        ApplicableSpan = GetTokenSpan(token.Key);
                    }
                }

                result = Once(ImportKeywordCompletion);
                return true;
            }

            if (Index >= fromImport.Root.StartIndex) {
                Node = fromImport.Root.Names.MaybeEnumerate()
                    .LastOrDefault(n => n.StartIndex <= Index && Index <= n.EndIndex);
                result = GetModulesFromNode(fromImport.Root);
                return true;
            }

            if (Index > fromImport.KeywordEndIndex) {
                result = GetModules();
                return true;
            }

            return false;
        }

        private bool TryGetCompletionsForOverride(FunctionDefinition function, out IEnumerable<CompletionItem> result) {
            if (function.Parent is ClassDefinition cd && string.IsNullOrEmpty(function.Name) && function.NameExpression != null && Index > function.NameExpression.StartIndex) {
                var loc = function.GetStart(Tree);
                ShouldCommitByDefault = false;
                result = Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));
                return true;
            }

            result = null;
            return false;
        }

        private CompletionItem ToOverrideCompletionItem(IOverloadResult o, ClassDefinition cd, string indent) {
            return new CompletionItem {
                label = o.Name,
                insertText = MakeOverrideCompletionString(indent, o, cd.Name),
                insertTextFormat = InsertTextFormat.PlainText,
                kind = CompletionItemKind.Method
            };
        }

        private bool NoCompletionsInFunctionDefinition(FunctionDefinition fd) {
            // Here we work backwards through the various parts of the definitions.
            // When we find that Index is within a part, we return either the available
            // completions 
            if (fd.HeaderIndex > fd.StartIndex && Index > fd.HeaderIndex) {
                return false;
            }

            if (Index == fd.HeaderIndex) {
                return true;
            }

            foreach (var p in fd.Parameters.Reverse()) {
                if (Index >= p.StartIndex) {
                    if (p.Annotation != null) {
                        return Index < p.Annotation.StartIndex;
                    }

                    if (p.DefaultValue != null) {
                        return Index < p.DefaultValue.StartIndex;
                    }
                }
            }

            if (fd.NameExpression != null && fd.NameExpression.StartIndex > fd.KeywordEndIndex && Index >= fd.NameExpression.StartIndex) {
                return true;
            }

            return Index > fd.KeywordEndIndex;
        }

        private bool NoCompletionsInClassDefinition(ClassDefinition cd, out bool addMetadataArg) {
            addMetadataArg = false;

            if (cd.HeaderIndex > cd.StartIndex && Index > cd.HeaderIndex) {
                return false;
            }

            if (Index == cd.HeaderIndex) {
                return true;
            }

            if (cd.Bases.Length > 0 && Index >= cd.Bases[0].StartIndex) {
                foreach (var p in cd.Bases.Reverse()) {
                    if (Index >= p.StartIndex) {
                        if (p.Name == null && Tree.LanguageVersion.Is3x() && cd.Bases.All(b => b.Name != "metaclass")) {
                            addMetadataArg = true;
                        }

                        return false;
                    }
                }
            }

            if (cd.NameExpression != null && cd.NameExpression.StartIndex > cd.KeywordEndIndex && Index >= cd.NameExpression.StartIndex) {
                return true;
            }

            return Index > cd.KeywordEndIndex;
        }

        private bool TryGetCompletionsInForStatement(ForStatement forStatement, out IEnumerable<CompletionItem> result) {
            result = null;

            if (forStatement.Left == null) {
                return false;
            }

            if (forStatement.InIndex > forStatement.StartIndex) {
                if (Index > forStatement.InIndex + 2) {
                    return false;
                }

                if (Index >= forStatement.InIndex) {
                    ApplicableSpan = new SourceSpan(Tree.IndexToLocation(forStatement.InIndex), Tree.IndexToLocation(forStatement.InIndex + 2));
                    result = Once(InKeywordCompletion);
                    return true;
                }
            }

            if (forStatement.Left.StartIndex > forStatement.StartIndex && forStatement.Left.EndIndex > forStatement.Left.StartIndex && Index > forStatement.Left.EndIndex) {
                SetApplicableSpanToLastToken(forStatement);
                result = Once(InKeywordCompletion);
                return true;
            }

            return forStatement.ForIndex >= forStatement.StartIndex && Index > forStatement.ForIndex + 3;
        }

        private bool TryGetCompletionsInWithStatement(WithStatement withStatement, out IEnumerable<CompletionItem> result) {
            result = null;

            if (Index > withStatement.HeaderIndex && withStatement.HeaderIndex > withStatement.StartIndex) {
                return false;
            }

            foreach (var item in withStatement.Items.Reverse().MaybeEnumerate()) {
                if (item.AsIndex > item.StartIndex) {
                    if (Index > item.AsIndex + 2) {
                        return true;
                    }

                    if (Index >= item.AsIndex) {
                        ApplicableSpan = new SourceSpan(Tree.IndexToLocation(item.AsIndex), Tree.IndexToLocation(item.AsIndex + 2));
                        result = Once(AsKeywordCompletion);
                        return true;
                    }
                }

                if (item.ContextManager != null && !(item.ContextManager is ErrorExpression)) {
                    if (Index > item.ContextManager.EndIndex && item.ContextManager.EndIndex > item.ContextManager.StartIndex) {
                        result = Once(AsKeywordCompletion);
                        return true;
                    }

                    if (Index >= item.ContextManager.StartIndex) {
                        return false;
                    }
                }
            }

            return false;
        }

        private bool TryGetCompletionsInRaiseStatement(RaiseStatement raiseStatement, out IEnumerable<CompletionItem> result) {
            result = null;

            // raise Type, Value, Traceback with Cause
            if (raiseStatement.Cause != null && Index >= raiseStatement.CauseFieldStartIndex) {
                return false;
            }

            if (raiseStatement.Traceback != null && Index >= raiseStatement.TracebackFieldStartIndex) {
                return false;
            }

            if (raiseStatement.Value != null && Index >= raiseStatement.ValueFieldStartIndex) {
                return false;
            }

            if (raiseStatement.ExceptType == null) {
                return false;
            }

            if (Index <= raiseStatement.ExceptType.EndIndex) {
                return false;
            }

            if (Tree.LanguageVersion.Is3x()) {
                SetApplicableSpanToLastToken(raiseStatement);
                result = Once(FromKeywordCompletion);
            }

            return true;
        }

        private bool TryGetCompletionsInExceptStatement(TryStatementHandler tryStatement, out IEnumerable<CompletionItem> result) {
            result = null;

            // except Test as Target
            if (tryStatement.Target != null && Index >= tryStatement.Target.StartIndex) {
                return true;
            }

            if (tryStatement.Test is TupleExpression || tryStatement.Test is null) {
                return false;
            }

            if (Index <= tryStatement.Test.EndIndex) {
                return false;
            }

            SetApplicableSpanToLastToken(tryStatement);
            result = Once(AsKeywordCompletion);
            return true;
        }

        private bool IsInsideComment() {
            var match = Array.BinarySearch(Tree._commentLocations, Position);
            // If our index = -1, it means we're before the first comment
            if (match == -1) {
                return false;
            }

            if (match < 0) {
                // If we couldn't find an exact match for this position, get the nearest
                // matching comment before this point
                match = ~match - 1;
            }

            if (match >= Tree._commentLocations.Length) {
                Debug.Fail("Failed to find nearest preceding comment in AST");
                return false;
            }

            if (Tree._commentLocations[match].Line != Position.Line) {
                return false;
            }

            if (Tree._commentLocations[match].Column >= Position.Column) {
                return false;
            }

            // We are inside a comment
            return true;
        }

        private IEnumerable<CompletionItem> GetCompletionsFromError() {
            if (!(Node is ErrorExpression)) {
                return null;
            }

            if (Statement is AssignmentStatement assign && Node == assign.Right) {
                return null;
            }

            bool ScopeIsClassDefinition(out ClassDefinition classDefinition) {
                classDefinition = Scope as ClassDefinition ?? (Scope as FunctionDefinition)?.Parent as ClassDefinition;
                return classDefinition != null;
            }

            var tokens = Tokens.Reverse().ToArray();

            string exprString;
            SourceLocation loc;
            var lastToken = tokens.FirstOrDefault();
            var nextLast = tokens.ElementAtOrDefault(1).Value?.Kind ?? TokenKind.EndOfFile;
            switch (lastToken.Value.Kind) {
                case TokenKind.Dot:
                    exprString = ReadExpression(tokens.Skip(1));
                    ApplicableSpan = new SourceSpan(Position, Position);
                    return Analysis.GetMembers(exprString, Position, Options).Select(ToCompletionItem);

                case TokenKind.KeywordDef when lastToken.Key.End < Index && ScopeIsClassDefinition(out var cd):
                    ApplicableSpan = new SourceSpan(Position, Position);
                    loc = GetTokenSpan(lastToken.Key).Start;
                    ShouldCommitByDefault = false;
                    return Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));

                case TokenKind.Name when nextLast == TokenKind.Dot:
                    exprString = ReadExpression(tokens.Skip(2));
                    ApplicableSpan = new SourceSpan(GetTokenSpan(lastToken.Key).Start, Position);
                    return Analysis.GetMembers(exprString, Position, Options).Select(ToCompletionItem);

                case TokenKind.Name when nextLast == TokenKind.KeywordDef && ScopeIsClassDefinition(out var cd):
                    ApplicableSpan = new SourceSpan(GetTokenSpan(lastToken.Key).Start, Position);
                    loc = GetTokenSpan(tokens.ElementAt(1).Key).Start;
                    ShouldCommitByDefault = false;
                    return Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));

                case TokenKind.KeywordFor:
                case TokenKind.KeywordAs:
                    return lastToken.Key.Start <= Index && Index <= lastToken.Key.End ? null : Empty;

                default:
                    Debug.WriteLine($"Unhandled completions from error.\nTokens were: ({lastToken.Value.Image}:{lastToken.Value.Kind}), {string.Join(", ", tokens.AsEnumerable().Take(10).Select(t => $"({t.Value.Image}:{t.Value.Kind})"))}");
                    return null;
            }
        }

        private IEnumerable<CompletionItem> GetCompletionsFromTopLevel() {
            if (Node != null && Node.EndIndex < Index) {
                return Empty;
            }

            var options = Options | GetMemberOptions.ForEval | GetMemberOptionsForTopLevelCompletions(Statement, Index, out var span);
            if (span.HasValue) {
                ApplicableSpan = new SourceSpan(Tree.IndexToLocation(span.Value.Start), Tree.IndexToLocation(span.Value.End));
            }

            ShouldAllowSnippets = options.HasFlag(GetMemberOptions.IncludeExpressionKeywords);

            _log.TraceMessage($"Completing all names");
            var members = Analysis.GetAllMembers(Position, options);

            var finder = new ExpressionFinder(Tree, new GetExpressionOptions { Calls = true });
            if (finder.GetExpression(Index) is CallExpression callExpr && callExpr.GetArgumentAtIndex(Tree, Index, out _)) {
                var argNames = Analysis.GetSignatures(callExpr.Target, Position)
                    .SelectMany(o => o.Parameters).Select(p => p?.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .Except(callExpr.Args.MaybeEnumerate().Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)))
                    .Select(n => new MemberResult($"{n}=", PythonMemberType.NamedArgument) as IMemberResult)
                    .ToArray();

                _log.TraceMessage($"Including {argNames.Length} named arguments");
                members = members.Concat(argNames);
            }

            return members
                .Where(m => !string.IsNullOrEmpty(m.Completion) || !string.IsNullOrEmpty(m.Name))
                .Select(ToCompletionItem);
        }

        private static GetMemberOptions GetMemberOptionsForTopLevelCompletions(Node statement, int index, out IndexSpan? span) {
            span = null;

            const GetMemberOptions noKeywords = GetMemberOptions.None;
            const GetMemberOptions exceptionsOnly = GetMemberOptions.ExceptionsOnly;
            const GetMemberOptions includeExpressionKeywords = GetMemberOptions.IncludeExpressionKeywords;
            const GetMemberOptions includeStatementKeywords = GetMemberOptions.IncludeStatementKeywords;
            const GetMemberOptions includeAllKeywords = includeExpressionKeywords | includeStatementKeywords;

            switch (statement) {
                // Disallow keywords, unless we're between the end of decorators and the
                // end of the "[async] def" keyword.
                case FunctionDefinition fd when index > fd.KeywordEndIndex || fd.Decorators != null && index < fd.Decorators.EndIndex:
                case ClassDefinition cd when index > cd.KeywordEndIndex || cd.Decorators != null && index < cd.Decorators.EndIndex:
                    return noKeywords;

                case TryStatementHandler tryStatement when tryStatement.Test is TupleExpression || index >= tryStatement.Test.StartIndex:
                    return exceptionsOnly;

                case null:
                    return includeAllKeywords;

                // Always allow keywords in non-keyword statements
                case ExpressionStatement _:
                    return includeAllKeywords;

                case ImportStatement _:
                case FromImportStatement _:
                    return includeAllKeywords;

                // Allow keywords at start of assignment, but not in subsequent names
                case AssignmentStatement ss:
                    var firstAssign = ss.Left?.FirstOrDefault();
                    return firstAssign == null || index <= firstAssign.EndIndex ? includeAllKeywords : includeExpressionKeywords;

                // Allow keywords when we are in another keyword
                case Statement s when index <= s.KeywordEndIndex:
                    var keywordStart = s.KeywordEndIndex - s.KeywordLength;
                    if (index >= keywordStart) {
                        span = new IndexSpan(keywordStart, s.KeywordLength);
                    } else if ((s as IMaybeAsyncStatement)?.IsAsync == true) {
                        // Must be in the "async" at the start of the keyword
                        span = new IndexSpan(s.StartIndex, "async".Length);
                    }
                    return includeAllKeywords;

                case RaiseStatement raise when raise.ExceptType != null && index >= raise.ExceptType.StartIndex || index > raise.KeywordEndIndex:
                    return includeExpressionKeywords | exceptionsOnly;

                // TryStatementHandler is 'except', but not a Statement subclass
                case TryStatementHandler except when index <= except.KeywordEndIndex:
                    var exceptKeywordStart = except.KeywordEndIndex - except.KeywordLength;
                    if (index >= exceptKeywordStart) {
                        span = new IndexSpan(exceptKeywordStart, except.KeywordLength);
                    }

                    return includeAllKeywords;

                // Allow keywords in function body (we'd have a different statement if we were deeper)
                case FunctionDefinition fd when index >= fd.HeaderIndex:
                    return includeAllKeywords;

                // Allow keywords within with blocks, but not in their definition
                case WithStatement ws:
                    return index >= ws.HeaderIndex || index <= ws.KeywordEndIndex ? includeAllKeywords : includeExpressionKeywords;

                default:
                    return includeExpressionKeywords;
            }
        }

        private static readonly CompletionItem MetadataArgCompletion = ToCompletionItem("metaclass=", PythonMemberType.NamedArgument);
        private static readonly CompletionItem AsKeywordCompletion = ToCompletionItem("as", PythonMemberType.Keyword);
        private static readonly CompletionItem FromKeywordCompletion = ToCompletionItem("from", PythonMemberType.Keyword);
        private static readonly CompletionItem InKeywordCompletion = ToCompletionItem("in", PythonMemberType.Keyword);
        private static readonly CompletionItem ImportKeywordCompletion = ToCompletionItem("import", PythonMemberType.Keyword);
        private static readonly CompletionItem WithKeywordCompletion = ToCompletionItem("with", PythonMemberType.Keyword);
        private static readonly CompletionItem StarCompletion = ToCompletionItem("*", PythonMemberType.Keyword);

        private CompletionItem ToCompletionItem(IMemberResult m) {
            var completion = m.Completion;
            if (string.IsNullOrEmpty(completion)) {
                completion = m.Name;
            }

            if (string.IsNullOrEmpty(completion)) {
                return default(CompletionItem);
            }

            var doc = _textBuilder.GetDocumentation(m.Values, string.Empty);
            var kind = ToCompletionItemKind(m.MemberType);

            var res = new CompletionItem {
                label = m.Name,
                insertText = completion,
                insertTextFormat = InsertTextFormat.PlainText,
                documentation = string.IsNullOrWhiteSpace(doc) ? null : new MarkupContent {
                    kind = _textBuilder.DisplayOptions.preferredFormat,
                    value = doc
                },
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(completion, 0) ? "1" : "2",
                kind = ToCompletionItemKind(m.MemberType),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            if (_addBrackets && (kind == CompletionItemKind.Constructor || kind == CompletionItemKind.Function || kind == CompletionItemKind.Method)) {
                res.insertText += "($0)";
                res.insertTextFormat = InsertTextFormat.Snippet;
            }

            return res;
        }

        private static CompletionItem ToCompletionItem(string text, PythonMemberType type, string label = null) {
            return new CompletionItem {
                label = label ?? text,
                insertText = text,
                insertTextFormat = InsertTextFormat.PlainText,
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(text, 0) ? "1" : "2",
                kind = ToCompletionItemKind(type),
                _kind = type.ToString().ToLowerInvariant()
            };
        }

        private static CompletionItemKind ToCompletionItemKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return CompletionItemKind.None;
                case PythonMemberType.Class: return CompletionItemKind.Class;
                case PythonMemberType.Instance: return CompletionItemKind.Value;
                case PythonMemberType.Enum: return CompletionItemKind.Enum;
                case PythonMemberType.EnumInstance: return CompletionItemKind.EnumMember;
                case PythonMemberType.Function: return CompletionItemKind.Function;
                case PythonMemberType.Method: return CompletionItemKind.Method;
                case PythonMemberType.Module: return CompletionItemKind.Module;
                case PythonMemberType.Constant: return CompletionItemKind.Constant;
                case PythonMemberType.Event: return CompletionItemKind.Event;
                case PythonMemberType.Field: return CompletionItemKind.Field;
                case PythonMemberType.Property: return CompletionItemKind.Property;
                case PythonMemberType.Multiple: return CompletionItemKind.Value;
                case PythonMemberType.Keyword: return CompletionItemKind.Keyword;
                case PythonMemberType.CodeSnippet: return CompletionItemKind.Snippet;
                case PythonMemberType.NamedArgument: return CompletionItemKind.Variable;
                default:
                    return CompletionItemKind.None;
            }
        }

        private static string MakeOverrideDefParamater(ParameterResult result) {
            if (!string.IsNullOrEmpty(result.DefaultValue)) {
                return result.Name + "=" + result.DefaultValue;
            }

            return result.Name;
        }

        private static string MakeOverrideCallParameter(ParameterResult result) {
            if (result.Name.StartsWithOrdinal("*")) {
                return result.Name;
            }

            if (!string.IsNullOrEmpty(result.DefaultValue)) {
                return result.Name + "=" + result.Name;
            }

            return result.Name;
        }

        private string MakeOverrideCompletionString(string indentation, IOverloadResult result, string className) {
            var sb = new StringBuilder();

            var self = result.SelfParameter != null ? new[] { result.SelfParameter } : Array.Empty<ParameterResult>();
            var parameters = self.Concat(result.Parameters).ToArray();

            sb.AppendLine(result.Name + "(" + string.Join(", ", parameters.Select(MakeOverrideDefParamater)) + "):");
            sb.Append(indentation);

            if (parameters.Length > 0) {
                var parameterString = string.Join(", ", 
                    result.Parameters
                        .Where(p => p.Name != "self")
                        .Select(MakeOverrideCallParameter));

                if (Tree.LanguageVersion.Is3x()) {
                    sb.AppendFormat("return super().{0}({1})",
                        result.Name,
                        parameterString);
                } else if (!string.IsNullOrEmpty(className)) {
                    sb.AppendFormat("return super({0}, {1}).{2}({3})",
                        className,
                        parameters.FirstOrDefault()?.Name ?? string.Empty,
                        result.Name,
                        parameterString);
                } else {
                    sb.Append("pass");
                }
            } else {
                sb.Append("pass");
            }

            return sb.ToString();
        }

        private string ReadExpression(IEnumerable<KeyValuePair<IndexSpan, Token>> tokens) {
            var expr = ReadExpressionTokens(tokens);
            return string.Join("", expr.Select(e => e.VerbatimImage ?? e.Image));
        }

        private IEnumerable<Token> ReadExpressionTokens(IEnumerable<KeyValuePair<IndexSpan, Token>> tokens) {
            int nesting = 0;
            var exprTokens = new Stack<Token>();
            int currentLine = -1;

            foreach (var t in tokens) {
                var p = GetTokenSpan(t.Key).Start;
                if (p.Line > currentLine) {
                    currentLine = p.Line;
                } else if (p.Line < currentLine && nesting == 0) {
                    break;
                }

                exprTokens.Push(t.Value);

                switch (t.Value.Kind) {
                    case TokenKind.RightParenthesis:
                    case TokenKind.RightBracket:
                    case TokenKind.RightBrace:
                        nesting += 1;
                        break;
                    case TokenKind.LeftParenthesis:
                    case TokenKind.LeftBracket:
                    case TokenKind.LeftBrace:
                        if (--nesting < 0) {
                            exprTokens.Pop();
                            return exprTokens;
                        }

                        break;

                    case TokenKind.Comment:
                        exprTokens.Pop();
                        break;

                    case TokenKind.Name:
                    case TokenKind.Constant:
                    case TokenKind.Dot:
                    case TokenKind.Ellipsis:
                    case TokenKind.MatMultiply:
                    case TokenKind.KeywordAwait:
                        break;

                    case TokenKind.Assign:
                    case TokenKind.LeftShiftEqual:
                    case TokenKind.RightShiftEqual:
                    case TokenKind.BitwiseAndEqual:
                    case TokenKind.BitwiseOrEqual:
                    case TokenKind.ExclusiveOrEqual:
                        exprTokens.Pop();
                        return exprTokens;

                    default:
                        if (t.Value.Kind >= TokenKind.FirstKeyword || nesting == 0) {
                            exprTokens.Pop();
                            return exprTokens;
                        }
                        break;
                }
            }

            return exprTokens;
        }
    }
}
