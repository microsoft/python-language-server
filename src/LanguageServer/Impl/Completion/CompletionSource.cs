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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class CompletionSource {
        private readonly CompletionContext _context;
        private readonly Expression _expression;
        private readonly bool _addBrackets;

        public CompletionSource(
            IDocumentAnalysis analysis,
            PythonAst ast,
            SourceLocation location,
            ServerSettings.PythonCompletionOptions completionSettings
        ) {
            _context = new CompletionContext(analysis, ast, location);
            _addBrackets = completionSettings.addBrackets;

            ExpressionLocator.FindExpression(ast, location, out var expression, out var statement, out var scope);
            Scope = scope;
            Statement = statement;
            Expression = expression;
        }

        public ScopeStatement Scope { get; }
        public Node Statement { get; }
        public Node Expression { get; }

        public async Task<CompletionResult> GetCompletionsAsync(CancellationToken cancellationToken = default) {
            switch (Expression) {
                case MemberExpression me when me.Target != null && me.DotIndex > me.StartIndex && _context.Position > me.DotIndex:
                    return new CompletionResult(await GetCompletionsFromMembersAsync(me, cancellationToken));
                case ConstantExpression ce when ce.Value != null:
                case null when _context.Ast.IsInsideComment(_context.Location):
                    return null;
            }

            switch (Statement) {
                case ImportStatement import when ImportCompletion.TryGetCompletionsInImport(import, _context, out var result):
                    return result;
                case FromImportStatement fromImport when ImportCompletion.TryGetCompletionsInFromImport(fromImport, _context, out var result):
                    return result;
                case FunctionDefinition fd when FunctionDefinitionCompletion.TryGetCompletionsForOverride(fd, _context, out var result):
                    return result;
                case FunctionDefinition fd when FunctionDefinitionCompletion.NoCompletions(fd, _context.Position):
                    return null;
                case ClassDefinition cd:
                    if (ClassDefinitionCompletion.NoCompletions(cd, _context, out var addMetadataArg)) {
                        return null;
                    }

                    return addMetadataArg
                        ? GetCompletionsFromTopLevel().Append(CompletionItemSource.MetadataArg)
                        : GetCompletionsFromTopLevel();

                case ForStatement forStatement when ForCompletion.TryGetCompletions(forStatement, _context, out var result):
                    return result;
                case WithStatement withStatement when WithCompletion.TryGetCompletions(withStatement, _context, out var result):
                    return result;
                case RaiseStatement raiseStatement when RaiseCompletion.TryGetCompletions(raiseStatement, _context, out var result):
                    return result;
                case TryStatementHandler tryStatement when ExceptCompletion.TryGetCompletions(tryStatement, _context, out var result):
                    return result;
                default:
                    new PartialExpressionCompletion(Node, )
                    return GetCompletionsFromError() ?? GetCompletionsFromTopLevel();
            }
        }

        private async Task<IEnumerable<CompletionItem>> GetCompletionsFromMembersAsync(MemberExpression me, CancellationToken cancellationToken = default) {
            var value = await _context.Analysis.ExpressionEvaluator.GetValueFromExpressionAsync(me.Target, cancellationToken);
            if (!value.IsUnknown()) {
                var type = value.GetPythonType();
                var names = type.GetMemberNames().ToArray();
                var types = names.Select(n => type.GetMember(n)).ToArray();
                return names.Zip(types, (n, t) => CompletionItemSource.CreateCompletionItem(n, t.MemberType));
            }
            return Enumerable.Empty<CompletionItem>();
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
    }
}
