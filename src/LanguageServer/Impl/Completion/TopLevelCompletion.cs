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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class TopLevelCompletion {
        public static async Task<CompletionResult> GetCompletionsAsync(Node statement, ScopeStatement scopeStatement, CompletionContext context, CancellationToken cancellationToken = default) {
            SourceSpan? applicableSpan = null;
            var eval = context.Analysis.ExpressionEvaluator;

            var options = GetOptions(statement, context.Position, out var span);
            if (span.HasValue) {
                applicableSpan = new SourceSpan(context.IndexToLocation(span.Value.Start), context.IndexToLocation(span.Value.End));
            }

            var scope = context.Analysis.FindScope(context.Location);
            IEnumerable<CompletionItem> items;
            using (eval.OpenScope(scope)) {
                // Get variables declared in the module.
                var variables = eval.CurrentScope.EnumerateTowardsGlobal.SelectMany(s => s.Variables).ToArray();
                items = variables.Select(v => context.ItemSource.CreateCompletionItem(v.Name, v)).ToArray();
            }

            // Get builtins
            var builtins = context.Analysis.Document.Interpreter.ModuleResolution.BuiltinsModule;
            var builtinItems = builtins.GetMemberNames()
                .Select(n => {
                    var m = builtins.GetMember(n);
                    if ((options & CompletionListOptions.ExceptionsOnly) == CompletionListOptions.ExceptionsOnly && !IsExceptionType(m.GetPythonType())) {
                        return null;
                    }
                    return context.ItemSource.CreateCompletionItem(n, m);
                }).ExcludeDefault();
            items = items.Concat(builtinItems);

            // Add possible function arguments.
            var finder = new ExpressionFinder(context.Ast, new FindExpressionOptions { Calls = true });
            if (finder.GetExpression(context.Position) is CallExpression callExpr && callExpr.GetArgumentAtIndex(context.Ast, context.Position, out _)) {
                var value = await eval.GetValueFromExpressionAsync(callExpr.Target, cancellationToken);
                if (value?.GetPythonType() is IPythonFunctionType ft) {
                    var arguments = ft.Overloads.SelectMany(o => o.Parameters).Select(p => p?.Name)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Distinct()
                        .Except(callExpr.Args.MaybeEnumerate().Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)))
                        .Select(n => CompletionItemSource.CreateCompletionItem($"{n}=", CompletionItemKind.Variable))
                        .ToArray();

                    items = items.Concat(arguments).ToArray();
                }
            }

            var keywords = GetKeywordItems(context, options, scopeStatement);
            items = items.Concat(keywords);

            return new CompletionResult(items, applicableSpan);
        }

        [Flags]
        enum CompletionListOptions {
            NoKeywords = 1,
            ExceptionsOnly = 2,
            StatementKeywords = 4,
            ExpressionKeywords = 8,
            AllKeywords = StatementKeywords | ExpressionKeywords
        }

        private static CompletionListOptions GetOptions(Node statement, int index, out IndexSpan? span) {
            span = null;

            switch (statement) {
                // Disallow keywords, unless we're between the end of decorators and the
                // end of the "[async] def" keyword.
                case FunctionDefinition fd when index > fd.KeywordEndIndex || fd.Decorators != null && index < fd.Decorators.EndIndex:
                case ClassDefinition cd when index > cd.KeywordEndIndex || cd.Decorators != null && index < cd.Decorators.EndIndex:
                    return CompletionListOptions.NoKeywords;

                case TryStatementHandler tryStatement when tryStatement.Test is TupleExpression || index >= tryStatement.Test.StartIndex:
                    return CompletionListOptions.ExceptionsOnly;

                // Always allow keywords in non-keyword statements
                case ExpressionStatement _:
                case ImportStatement _:
                case FromImportStatement _:
                case null:
                    return CompletionListOptions.AllKeywords;

                // Allow keywords at start of assignment, but not in subsequent names
                case AssignmentStatement ss:
                    var firstAssign = ss.Left?.FirstOrDefault();
                    return firstAssign == null || index <= firstAssign.EndIndex
                        ? CompletionListOptions.AllKeywords : CompletionListOptions.ExpressionKeywords;

                // Allow keywords when we are in another keyword
                case Statement s when index <= s.KeywordEndIndex:
                    var keywordStart = s.KeywordEndIndex - s.KeywordLength;
                    if (index >= keywordStart) {
                        span = new IndexSpan(keywordStart, s.KeywordLength);
                    } else if ((s as IMaybeAsyncStatement)?.IsAsync == true) {
                        // Must be in the "async" at the start of the keyword
                        span = new IndexSpan(s.StartIndex, "async".Length);
                    }
                    return CompletionListOptions.AllKeywords;

                case RaiseStatement raise when raise.ExceptType != null && index >= raise.ExceptType.StartIndex || index > raise.KeywordEndIndex:
                    return CompletionListOptions.ExpressionKeywords | CompletionListOptions.ExceptionsOnly;

                // TryStatementHandler is 'except', but not a Statement subclass
                case TryStatementHandler except when index <= except.KeywordEndIndex:
                    var exceptKeywordStart = except.KeywordEndIndex - except.KeywordLength;
                    if (index >= exceptKeywordStart) {
                        span = new IndexSpan(exceptKeywordStart, except.KeywordLength);
                    }
                    return CompletionListOptions.AllKeywords;

                // Allow keywords in function body (we'd have a different statement if we were deeper)
                case FunctionDefinition fd when index >= fd.HeaderIndex:
                    return CompletionListOptions.AllKeywords;

                // Allow keywords within with blocks, but not in their definition
                case WithStatement ws:
                    return index >= ws.HeaderIndex || index <= ws.KeywordEndIndex
                        ? CompletionListOptions.AllKeywords : CompletionListOptions.ExpressionKeywords;

                default:
                    return CompletionListOptions.ExpressionKeywords;
            }
        }
        private static IEnumerable<CompletionItem> GetKeywordItems(CompletionContext context, CompletionListOptions options, ScopeStatement scope) {
            var keywords = Enumerable.Empty<string>();

            if ((options & CompletionListOptions.ExpressionKeywords) == CompletionListOptions.ExpressionKeywords) {
                // keywords available in any context
                keywords = PythonKeywords.Expression(context.Ast.LanguageVersion);
            }

            if ((options & CompletionListOptions.StatementKeywords) == CompletionListOptions.StatementKeywords) {
                keywords = keywords.Union(PythonKeywords.Statement(context.Ast.LanguageVersion));
            }

            if (!(scope is FunctionDefinition)) {
                keywords = keywords.Except(PythonKeywords.InvalidOutsideFunction(context.Ast.LanguageVersion));
            }

            return keywords.Select(kw => CompletionItemSource.CreateCompletionItem(kw, CompletionItemKind.Keyword));
        }

        private static bool IsExceptionType(IPythonType type) {
            if (type.Name.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.Name.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }

            if (type is IPythonClassType cls) {
                var baseCls = type.DeclaringModule.Interpreter.ModuleResolution.BuiltinsModule.GetMember("BaseException") as IPythonType;
                return cls.Mro.Any(b => b is IPythonClassType c && c.Bases.Contains(baseCls));
            }
            return false;
        }
    }
}
