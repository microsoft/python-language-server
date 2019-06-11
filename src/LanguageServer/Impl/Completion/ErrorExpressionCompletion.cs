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

using System.IO;
using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class ErrorExpressionCompletion {
        public static CompletionResult GetCompletions(ScopeStatement scope, Node statement, Node expression, CompletionContext context) {
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
            var items = Enumerable.Empty<CompletionItem>();
            SourceSpan? applicableSpan;
            Expression e;

            var lastToken = tokens.FirstOrDefault();
            if(lastToken.Value == null) {
                return CompletionResult.Empty;
            }

            var nextLast = tokens.ElementAtOrDefault(1).Value?.Kind ?? TokenKind.EndOfFile;
            switch (lastToken.Value.Kind) {
                case TokenKind.Dot:
                    code = es.ReadExpression(tokens.Skip(1));
                    applicableSpan = new SourceSpan(context.Location, context.Location);
                    e = GetExpressionFromText(code, context, out var s1, out _);
                    items = ExpressionCompletion.GetCompletionsFromMembers(e, s1, context);
                    break;

                case TokenKind.KeywordDef when lastToken.Key.End < context.Position && scope is FunctionDefinition fd: {
                        applicableSpan = new SourceSpan(context.Location, context.Location);
                        location = context.TokenSource.GetTokenSpan(lastToken.Key).Start;
                        if (FunctionDefinitionCompletion.TryGetCompletionsForOverride(fd, context, location, out var result)) {
                            items = result.Completions;
                        }

                        break;
                    }

                case TokenKind.Name when nextLast == TokenKind.Dot:
                    code = es.ReadExpression(tokens.Skip(2));
                    applicableSpan = new SourceSpan(context.TokenSource.GetTokenSpan(lastToken.Key).Start, context.Location);
                    e = GetExpressionFromText(code, context, out var s2, out _);
                    items = ExpressionCompletion.GetCompletionsFromMembers(e, s2, context);
                    break;

                case TokenKind.Name when nextLast == TokenKind.KeywordDef && scope is FunctionDefinition fd: {
                        applicableSpan = new SourceSpan(context.TokenSource.GetTokenSpan(lastToken.Key).Start, context.Location);
                        location = context.TokenSource.GetTokenSpan(tokens.ElementAt(1).Key).Start;
                        if (FunctionDefinitionCompletion.TryGetCompletionsForOverride(fd, context, location, out var result)) {
                            items = result.Completions;
                        }
                        break;
                    }

                case TokenKind.KeywordFor:
                case TokenKind.KeywordAs:
                    return lastToken.Key.Start <= context.Position && context.Position <= lastToken.Key.End ? null : CompletionResult.Empty;

                case TokenKind.Error:
                    return null;

                default:
                    return CompletionResult.Empty;
            }

            return new CompletionResult(items, applicableSpan);
        }

        private static Expression GetExpressionFromText(string text, CompletionContext context, out IScope scope, out PythonAst expressionAst) {
            scope = context.Analysis.FindScope(context.Location);
            using (var reader = new StringReader(text)) {
                var parser = Parser.CreateParser(reader, context.Ast.LanguageVersion, new ParserOptions());
                expressionAst = parser.ParseTopExpression(null);
                return Statement.GetExpression(expressionAst.Body);
            }
        }
    }
}
