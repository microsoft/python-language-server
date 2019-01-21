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
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class ExpressionSource {
        private readonly TokenSource _tokenSource;

        public ExpressionSource(TokenSource tokenSource) {
            _tokenSource = tokenSource;
        }

        public string ReadExpression(IEnumerable<KeyValuePair<IndexSpan, Token>> tokens) {
            var expr = ReadExpressionTokens(tokens);
            return string.Join("", expr.Select(e => e.VerbatimImage ?? e.Image));
        }

        private IEnumerable<Token> ReadExpressionTokens(IEnumerable<KeyValuePair<IndexSpan, Token>> tokens) {
            var nesting = 0;
            var exprTokens = new Stack<Token>();
            var currentLine = -1;

            foreach (var t in tokens) {
                var p = _tokenSource.GetTokenSpan(t.Key).Start;
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
