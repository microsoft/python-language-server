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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class PartialExpressionCompletion {
        public static async Task<CompletionResult> GetCompletionsAsync(ScopeStatement scope, Node statement, Node expression, CompletionContext context, CancellationToken cancellationToken = default) {
            if (!(expression is ErrorExpression)) {
                return CompletionResult.Empty;
            }

            if (statement is AssignmentStatement assign && expression == assign.Right) {
                return CompletionResult.Empty;
            }

            var tokens = context.TokenSource.Tokens.Reverse().ToArray();
            var es = new ExpressionSource(context.TokenSource);

            string code;
            SourceLocation location;
            IEnumerable<CompletionItem> items;
            SourceSpan? applicableSpan;
            Expression e;

            var lastToken = tokens.FirstOrDefault();
            var nextLast = tokens.ElementAtOrDefault(1).Value?.Kind ?? TokenKind.EndOfFile;
            switch (lastToken.Value.Kind) {
                case TokenKind.Dot:
                    code = es.ReadExpression(tokens.Skip(1));
                    applicableSpan = new SourceSpan(context.Location, context.Location);
                    e = GetExpressionFromText(code, context, out var s1, out _);
                    items = await ExpressionCompletion.GetCompletionsFromMembersAsync(e, s1, context, cancellationToken);
                    break;

                case TokenKind.KeywordDef when lastToken.Key.End < context.Position && scope is FunctionDefinition fd:
                    applicableSpan = new SourceSpan(context.Location, context.Location);
                    location = context.TokenSource.GetTokenSpan(lastToken.Key).Start;
                    items = FunctionDefinitionCompletion.GetCompletionsForOverride(fd, context, location).Completions;
                    break;

                case TokenKind.Name when nextLast == TokenKind.Dot:
                    code = es.ReadExpression(tokens.Skip(2));
                    applicableSpan = new SourceSpan(context.TokenSource.GetTokenSpan(lastToken.Key).Start, context.Location);
                    e = GetExpressionFromText(code, context, out var s2, out _);
                    items = await ExpressionCompletion.GetCompletionsFromMembersAsync(e, s2, context, cancellationToken);
                    break;

                case TokenKind.Name when nextLast == TokenKind.KeywordDef && scope is FunctionDefinition fd:
                    applicableSpan = new SourceSpan(context.TokenSource.GetTokenSpan(lastToken.Key).Start, context.Location);
                    location = context.TokenSource.GetTokenSpan(tokens.ElementAt(1).Key).Start;
                    items = FunctionDefinitionCompletion.GetCompletionsForOverride(fd, context, location).Completions;
                    break;

                case TokenKind.KeywordFor:
                case TokenKind.KeywordAs:
                    return lastToken.Key.Start <= context.Position && context.Position <= lastToken.Key.End ? null : CompletionResult.Empty;

                default:
                    Debug.WriteLine($"Unhandled completions from error.\nTokens were: ({lastToken.Value.Image}:{lastToken.Value.Kind}), {string.Join(", ", tokens.AsEnumerable().Take(10).Select(t => $"({t.Value.Image}:{t.Value.Kind})"))}");
                    return CompletionResult.Empty;
            }

            return new CompletionResult(items, applicableSpan);
        }

        private static Expression GetExpressionFromText(string text, CompletionContext context, out IScope scope, out PythonAst expressionAst) {
            scope = context.Analysis.FindScope(context.Location);
            using (var reader = new StringReader(text)) {
                var parser = Parser.CreateParser(reader, context.Ast.LanguageVersion, new ParserOptions());
                expressionAst = parser.ParseTopExpression();
                return Statement.GetExpression(expressionAst.Body);
            }
        }
    }
}
