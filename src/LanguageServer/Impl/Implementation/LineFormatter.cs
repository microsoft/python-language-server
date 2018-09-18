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
using System.IO;
using System.Linq;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.Python.LanguageServer.Implementation {
    /// <summary>
    /// LineFormatter formats lines of code to generally conform with PEP8.
    /// </summary>
    public class LineFormatter {
        private static readonly TextEdit[] NoEdits = Array.Empty<TextEdit>();

        private readonly TokenizerWrapper _tokenizer;
        private readonly Dictionary<int, List<TokenExt>> _lineTokens;

        /// <summary>
        /// Creates a LineFormatter from a reader. It will only read as much
        /// of the input as is needed to format the requested line.
        /// </summary>
        /// <param name="reader">The code to be formatted. LineFormatter does not dispose of the reader.</param>
        /// <param name="languageVersion">Language version to use in the tokenization format.</param>
        public LineFormatter(TextReader reader, PythonLanguageVersion languageVersion) {
            var tokenizer = new Tokenizer(languageVersion, options: TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins | TokenizerOptions.GroupingRecovery);
            tokenizer.Initialize(reader);
            _tokenizer = new TokenizerWrapper(tokenizer);
            _lineTokens = new Dictionary<int, List<TokenExt>>();
        }

        private void AddToken(TokenExt token) {
            var line = token.Line;

            // Explicit line joins ("\") appear at the end of a line, but
            // their span ends on another line, so move backward so they can
            // be inserted in the right place.
            if (token.Kind == TokenKind.ExplicitLineJoin) {
                line--;
            }

            if (!_lineTokens.TryGetValue(line, out List<TokenExt> tokens)) {
                tokens = new List<TokenExt>();
                _lineTokens.Add(token.Line, tokens);
            }

            tokens.Add(token);
        }

        /// <summary>
        /// Tokenizes up to and including the specified line. Tokens are
        /// stored in _lineTokens. If the provided line number is past the
        /// end of the input text, then the tokenizer will stop.
        /// Additionally, this function will attempt to read ahead onto the
        /// next line to the first non-ignored token so that the formatter
        /// can look ahead.
        /// </summary>
        /// <param name="line">One-indexed line number.</param>
        private void TokenizeLine(int line) {
            if (line < 1) {
                return;
            }

            var extraToken = true;

            var peeked = _tokenizer.Peek();
            while (peeked != null && (peeked.Line <= line || extraToken)) {
                var token = _tokenizer.Next();
                AddToken(token);
                peeked = _tokenizer.Peek();

                if (token.Line > line && !token.IsIgnored) {
                    extraToken = false;
                }
            }
        }

        /// <summary>
        /// Formats a single line and returns TextEdits to replace the old text.
        /// </summary>
        /// <param name="line">One-indexed line number.</param>
        /// <returns>A list of TextEdits needed to format the line.</returns>
        public TextEdit[] FormatLine(int line) {
            if (line < 1) {
                return NoEdits;
            }

            TokenizeLine(line);

            if (!_lineTokens.TryGetValue(line, out List<TokenExt> tokens)) {
                return NoEdits;
            }

            if (tokens.Count == 0) {
                return NoEdits;
            }

            var builder = new TextBuilder();
            var beginCol = -1;
            var skipFirst = false;

            for (var i = 0; i < tokens.Count; i++) {
                var token = tokens[i];

                if (i == 0 && token.IsMultilineString) {
                    // If the line begins with a multiline-string (rather than some whitespace),
                    // then begin the edit right after. The token will be skipped but not
                    // removed so it can still be used for formatting rules which look at
                    // the previous token.
                    beginCol = token.EndCol;
                    skipFirst = true;
                    builder.SoftAppendSpace(allowLeading: true);
                    break;
                }

                if (!token.IsIgnored) {
                    if (i != 0) {
                        beginCol = tokens[i - 1].EndCol;
                    }
                    break;
                }
            }

            // Keep ExplictLineJoin because it has text associated with it.
            tokens = tokens.Where(t => !t.IsIgnored || t.Kind == TokenKind.ExplicitLineJoin).ToList();

            if (tokens.Count == 0) {
                return NoEdits;
            }

            if (beginCol == -1) {
                // The beginning column couldn't be deduced from the tokens,
                // so resort to looking at the first token's preceeding whitespace.
                var firstWhitespace = tokens.First().PreceedingWhitespace ?? "";
                firstWhitespace = SplitByNewline(firstWhitespace).Last();
                beginCol = 1 + firstWhitespace.Length;
            }

            for (var i = 0; i < tokens.Count; i++) {
                if (i == 0 && skipFirst) {
                    continue;
                }

                var token = tokens[i];
                var prev = tokens.ElementAtOrDefault(i - 1);
                var next = tokens.ElementAtOrDefault(i + 1);

                switch (token.Kind) {
                    case TokenKind.Comment:
                        builder.SoftAppendSpace(2);
                        builder.Append(token);
                        break;

                    case TokenKind.Assign:
                        if (token.IsInsideFunctionArgs && prev?.PrevNonIgnored?.Kind != TokenKind.Colon) {
                            builder.Append(token);
                            break;
                        }

                        goto case TokenKind.AddEqual;

                    // "Normal" assignment and function parameters with type hints
                    case TokenKind.AddEqual:
                    case TokenKind.SubtractEqual:
                    case TokenKind.PowerEqual:
                    case TokenKind.MultiplyEqual:
                    case TokenKind.MatMultiplyEqual:
                    case TokenKind.FloorDivideEqual:
                    case TokenKind.DivideEqual:
                    case TokenKind.ModEqual:
                    case TokenKind.LeftShiftEqual:
                    case TokenKind.RightShiftEqual:
                    case TokenKind.BitwiseAndEqual:
                    case TokenKind.BitwiseOrEqual:
                    case TokenKind.ExclusiveOrEqual:
                        builder.SoftAppendSpace();
                        builder.Append(token);
                        builder.SoftAppendSpace();
                        break;

                    case TokenKind.Comma:
                        builder.Append(token);
                        if (next != null && !next.IsClose && next.Kind != TokenKind.Colon) {
                            builder.SoftAppendSpace();
                        }
                        break;

                    case TokenKind.Colon:
                        // Slicing
                        if (token.Inside?.Kind == TokenKind.LeftBracket) {
                            if (!token.IsSimpleSliceToLeft) {
                                builder.SoftAppendSpace();
                            }

                            builder.Append(token);

                            if (!token.IsSimpleSliceToRight) {
                                builder.SoftAppendSpace();
                            }

                            break;
                        }

                        builder.Append(token);
                        if (next?.Kind != TokenKind.Colon) {
                            builder.SoftAppendSpace();
                        }
                        break;

                    case TokenKind.At:
                        if (prev != null) {
                            goto case TokenKind.MatMultiply;
                        }

                        builder.Append(token);
                        break;

                    // Unary
                    case TokenKind.Add:
                    case TokenKind.Subtract:
                    case TokenKind.Twiddle:
                        if (prev != null && (prev.IsOperator || prev.IsOpen || prev.Is(TokenKind.Comma, TokenKind.Colon))) {
                            builder.Append(token);
                            break;
                        }
                        goto case TokenKind.MatMultiply;

                    case TokenKind.Power:
                    case TokenKind.Multiply:
                        if (token.Inside != null) {
                            var actualPrev = token.PrevNonIgnored;
                            if (actualPrev != null) {
                                if (actualPrev.Kind == TokenKind.Comma || actualPrev.IsOpen || token.Inside.Kind == TokenKind.KeywordLambda) {
                                    builder.Append(token);
                                    break;
                                }
                            }
                        }

                        if (token.Kind == TokenKind.Multiply) {
                            // Check unpacking case
                            var actualPrev = token.PrevNonIgnored;
                            if (actualPrev == null || (actualPrev.Kind != TokenKind.Name && actualPrev.Kind != TokenKind.Constant && !actualPrev.IsClose)) {
                                builder.Append(token);
                                break;
                            }
                        }

                        goto case TokenKind.MatMultiply;

                    // Operators
                    case TokenKind.MatMultiply:
                    case TokenKind.FloorDivide:
                    case TokenKind.Divide:
                    case TokenKind.Mod:
                    case TokenKind.LeftShift:
                    case TokenKind.RightShift:
                    case TokenKind.BitwiseAnd:
                    case TokenKind.BitwiseOr:
                    case TokenKind.ExclusiveOr:
                    case TokenKind.LessThan:
                    case TokenKind.GreaterThan:
                    case TokenKind.LessThanOrEqual:
                    case TokenKind.GreaterThanOrEqual:
                    case TokenKind.Equals:
                    case TokenKind.NotEquals:
                    case TokenKind.LessThanGreaterThan:
                    case TokenKind.Arrow:
                        builder.SoftAppendSpace();
                        builder.Append(token);
                        builder.SoftAppendSpace();
                        break;

                    case TokenKind.Dot:
                        if (prev != null && (prev.Kind == TokenKind.KeywordFrom || prev.IsNumber)) {
                            builder.SoftAppendSpace();
                        }

                        builder.Append(token);

                        if (next?.Kind == TokenKind.KeywordImport) {
                            builder.SoftAppendSpace();
                        }

                        break;

                    case TokenKind.LeftBrace:
                    case TokenKind.LeftBracket:
                    case TokenKind.LeftParenthesis:
                    case TokenKind.RightBrace:
                    case TokenKind.RightBracket:
                    case TokenKind.RightParenthesis:
                        builder.Append(token);
                        break;

                    case TokenKind.Semicolon:
                        builder.Append(token);
                        builder.SoftAppendSpace();
                        break;

                    case TokenKind.Name:
                    case TokenKind.Constant:
                    case TokenKind.KeywordFalse:
                    case TokenKind.KeywordTrue:
                    case TokenKind.Ellipsis: // Ellipsis is a value
                        builder.Append(token);
                        break;

                    case TokenKind.ExplicitLineJoin:
                        builder.SoftAppendSpace();
                        builder.Append("\\"); // Hardcoded string so that any following whitespace doesn't make it in.
                        break;

                    default:
                        if (token.Kind == TokenKind.KeywordLambda) {
                            if (token.IsInsideFunctionArgs && prev?.Kind == TokenKind.Assign) {
                                builder.Append(token);

                                if (next?.Kind != TokenKind.Colon) {
                                    builder.SoftAppendSpace();
                                }

                                break;
                            }
                        }

                        if (token.IsKeyword) {
                            if (prev != null && !prev.IsOpen) {
                                builder.SoftAppendSpace();
                            }

                            builder.Append(token);

                            if (next != null && next.Kind != TokenKind.Colon && next.Kind != TokenKind.Semicolon) {
                                builder.SoftAppendSpace();
                            }

                            break;
                        }

                        if (prev != null && (prev.IsOpen || prev.Kind == TokenKind.Colon)) {
                            builder.Append(token);
                            break;
                        }

                        throw new Exception($"Unhandled token on line {line} of kind {token.Kind}: {token}");
                }
            }

            var newText = builder.ToString();
            var endCol = _tokenizer.EndOfLineCol(line);

            var afterLast = tokens.Last().Next;
            if (afterLast != null && afterLast.IsMultilineString) {
                // If the the next token is a multiline string, then make
                // sure to include that string's prefix on this line.
                var afterLastFirst = SplitByNewline(afterLast.ToString()).First();
                endCol -= afterLastFirst.Length;

                if (tokens.Last().IsOperator) {
                    newText += " ";
                }
            }

            if (newText == "") {
                return NoEdits;
            }

            var edit = new TextEdit {
                range = new Range {
                    start = new SourceLocation(line, beginCol),
                    end = new SourceLocation(line, endCol)
                },
                newText = newText
            };

            return new[] { edit };
        }

        private class TokenExt {
            public Token Token { get; set; }
            public SourceSpan Span { get; set; }
            public int Line => Span.End.Line;
            public int EndCol => Span.End.Column;
            public TokenExt Inside { get; set; }
            public TokenExt Prev { get; set; }
            public TokenExt Next { get; set; }
            public string PreceedingWhitespace { get; set; }
            public TokenKind Kind => Token.Kind;

            public override string ToString() => Token.VerbatimImage;

            public bool Is(params TokenKind[] kinds) => kinds.Contains(Kind);

            public bool IsIgnored => Is(TokenKind.NewLine, TokenKind.NLToken, TokenKind.Indent, TokenKind.Dedent, TokenKind.ExplicitLineJoin);

            public bool IsOpen => Is(TokenKind.LeftBrace, TokenKind.LeftBracket, TokenKind.LeftParenthesis);

            public bool IsClose => Is(TokenKind.RightBrace, TokenKind.RightBracket, TokenKind.RightParenthesis);

            public bool MatchesClose(TokenExt other) {
                switch (Kind) {
                    case TokenKind.LeftBrace:
                        return other.Kind == TokenKind.RightBrace;
                    case TokenKind.LeftBracket:
                        return other.Kind == TokenKind.RightBracket;
                    case TokenKind.LeftParenthesis:
                        return other.Kind == TokenKind.RightParenthesis;
                }

                return false;
            }

            public bool IsOperator => Token is OperatorToken || Is(TokenKind.Dot, TokenKind.Assign, TokenKind.Twiddle);

            public bool IsUnaryOp => Is(TokenKind.Add, TokenKind.Subtract, TokenKind.Twiddle);

            public bool IsInsideFunctionArgs => (Inside?.Kind == TokenKind.LeftParenthesis && Inside.PrevNonIgnored?.Kind == TokenKind.Name) || (Inside?.Kind == TokenKind.KeywordLambda);

            public bool IsNumber => Kind == TokenKind.Constant && Token != Tokens.NoneToken && !(Token.Value is string || Token.Value is AsciiString);

            public bool IsKeyword => (Kind >= TokenKind.FirstKeyword && Kind <= TokenKind.LastKeyword) || Kind == TokenKind.KeywordAsync || Kind == TokenKind.KeywordAwait;

            public bool IsMultilineString
            {
                get
                {
                    if (Kind != TokenKind.Constant || Token == Tokens.NoneToken) {
                        return false;
                    }

                    if (!(Token.Value is string || Token.Value is AsciiString)) {
                        return false;
                    }

                    return Span.Start.Line != Span.End.Line;
                }
            }

            public bool IsSimpleSliceToLeft
            {
                get
                {
                    if (Kind != TokenKind.Colon) {
                        return false;
                    }

                    var a = PrevNonIgnored;
                    var b = a?.PrevNonIgnored;
                    var c = b?.PrevNonIgnored;

                    if (a == null) {
                        return false;
                    }

                    if (a.Is(TokenKind.LeftBracket, TokenKind.Colon)) {
                        return true;
                    }

                    if ((!a.IsNumber && a.Kind != TokenKind.Name) || b == null) {
                        return false;
                    }

                    if (b.Is(TokenKind.LeftBracket, TokenKind.Colon)) {
                        return true;
                    }

                    if (!b.IsUnaryOp || c == null) {
                        return false;
                    }

                    return c.Is(TokenKind.LeftBracket, TokenKind.Colon);
                }
            }

            public bool IsSimpleSliceToRight
            {
                get
                {
                    if (Kind != TokenKind.Colon) {
                        return false;
                    }

                    var a = NextNonIgnored;
                    var b = a?.NextNonIgnored;
                    var c = b?.NextNonIgnored;

                    if (a == null) {
                        return false;
                    }

                    if (a.Is(TokenKind.RightBracket, TokenKind.Colon)) {
                        return true;
                    }

                    if (b == null) {
                        return false;
                    }

                    if (a.IsUnaryOp) {
                        if (c == null) {
                            return false;
                        }
                        return (b.IsNumber || b.Kind == TokenKind.Name) && c.Is(TokenKind.RightBracket, TokenKind.Colon);
                    }

                    return (a.IsNumber || a.Kind == TokenKind.Name) && b.Is(TokenKind.RightBracket, TokenKind.Colon);
                }
            }

            public TokenExt PrevNonIgnored
            {
                get
                {
                    if (Prev != null) {
                        if (Prev.IsIgnored) {
                            return Prev.PrevNonIgnored;
                        }
                        return Prev;
                    }
                    return null;
                }
            }

            public TokenExt NextNonIgnored
            {
                get
                {
                    if (Next != null) {
                        if (Next.IsIgnored) {
                            return Next.NextNonIgnored;
                        }
                        return Next;
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// TokenizerWrapper wraps a tokenizer, producing a stream of TokenExt
        /// instead of regular Tokens. The wrapper keeps track of brackets and
        /// lambdas, and allows peeking forward at the next token without
        /// advancing the tokenizer.
        /// </summary>
        private class TokenizerWrapper {
            private readonly Tokenizer _tokenizer;
            private Stack<TokenExt> _insides = new Stack<TokenExt>();
            private TokenExt _peeked = null;
            private TokenExt _prev = null;

            public TokenizerWrapper(Tokenizer tokenizer) {
                _tokenizer = tokenizer;
            }

            /// <summary>
            /// Returns the next token, and advances the tokenizer. Note that
            /// the returned token's Next will not be set until the tokenizer
            /// actually reads that next token.
            /// </summary>
            /// <returns>The next token</returns>
            public TokenExt Next() {
                if (_peeked != null) {
                    var tmp = _peeked;
                    _peeked = null;
                    return tmp;
                }

                if (_tokenizer.IsEndOfFile) {
                    return null;
                }

                var token = _tokenizer.GetNextToken();

                if (token.Kind == TokenKind.EndOfFile) {
                    return null;
                }

                var tokenSpan = _tokenizer.TokenSpan;
                var sourceSpan = new SourceSpan(_tokenizer.IndexToLocation(tokenSpan.Start), _tokenizer.IndexToLocation(tokenSpan.End));

                var tokenExt = new TokenExt {
                    Token = token,
                    PreceedingWhitespace = _tokenizer.PreceedingWhiteSpace,
                    Span = sourceSpan,
                    Prev = _prev
                };

                if (tokenExt.IsClose) {
                    if (_insides.Count == 0 || !_insides.Peek().MatchesClose(tokenExt)) {
                        throw new Exception($"Close bracket ({token.Kind}) has no matching open");
                    }
                    _insides.Pop();
                } else if (tokenExt.Kind == TokenKind.Colon && _insides.Count != 0 && _insides.Peek().Kind == TokenKind.KeywordLambda) {
                    _insides.Pop();
                }

                if (_insides.TryPeek(out TokenExt inside)) {
                    tokenExt.Inside = inside;
                }

                if (tokenExt.IsOpen || tokenExt.Kind == TokenKind.KeywordLambda) {
                    _insides.Push(tokenExt);
                }

                if (_prev != null) {
                    _prev.Next = tokenExt;
                }

                _prev = tokenExt;
                return tokenExt;
            }

            /// <summary>
            /// Returns the next token without advancing the tokenizer. Note that
            /// the returned token's Next will not be set until the tokenizer
            /// actually reads that next token.
            /// </summary>
            /// <returns>The next token</returns>
            public TokenExt Peek() {
                if (_peeked != null) {
                    return _peeked;
                }

                _peeked = Next();
                return _peeked;
            }

            /// <summary>
            /// Gets the one-indexed column number of the end of a line. The
            /// tokenizer must be past the line's newline (or at EOF) in order
            /// for this function to work.
            /// </summary>
            /// <param name="line">A one-indexed line number.</param>
            /// <returns>One-indexed column number for the end of the line</returns>
            public int EndOfLineCol(int line) {
                if (line > _tokenizer.CurrentPosition.Line || (line == _tokenizer.CurrentPosition.Line && !_tokenizer.IsEndOfFile)) {
                    throw new ArgumentException("tokenizer must be at EOF or past line's newline", nameof(line));
                }

                var idx = line - 1;
                var lines = _tokenizer.GetLineLocations();

                if (idx < lines.Length) {
                    var nlLoc = lines[idx];

                    var sourceLocation = _tokenizer.IndexToLocation(nlLoc.EndIndex - 1);
                    return sourceLocation.Column;
                }

                return _tokenizer.CurrentPosition.Column;
            }
        }

        private static string[] SplitByNewline(string s) => s.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }
}
