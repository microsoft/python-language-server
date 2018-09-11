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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public override async Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params, CancellationToken cancellationToken) {
            // The current line is the line after the one we need to format, so
            // it is also the one-indexed line number for the target line.
            var targetLine = @params.position.line;
            if (@params.ch == ";") {
                // Unless the trigger was a semicolon, in which case the current line is the target.
                targetLine++;
            }

            var uri = @params.textDocument.uri;

            if (!(ProjectFiles.GetEntry(uri) is IDocument doc)) {
                return Array.Empty<TextEdit>();
            }
            var part = ProjectFiles.GetPart(uri);

            using (var reader = doc.ReadDocument(part, out _)) {
                var lineFormatter = new LineFormatter(reader, Analyzer.LanguageVersion);
                return lineFormatter.FormatLine(targetLine);
            }
        }

        private class LineFormatter {
            private static readonly TextEdit[] NoEdits = Array.Empty<TextEdit>();

            private readonly TextReader _reader;
            private readonly TokenizerWrapper _tokenizer;
            private readonly Dictionary<int, List<TokenExt>> _lineTokens;

            public LineFormatter(TextReader reader, PythonLanguageVersion languageVersion) {
                _reader = reader;
                var tokenizer = new Tokenizer(languageVersion, options: TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins);
                tokenizer.Initialize(_reader);
                _tokenizer = new TokenizerWrapper(tokenizer);
                _lineTokens = new Dictionary<int, List<TokenExt>>();
            }

            public LineFormatter(string fileContents, PythonLanguageVersion languageVersion) : this(new StringReader(fileContents), languageVersion) { }

            private void AddToken(TokenExt token) {
                if (!_lineTokens.TryGetValue(token.Line, out List<TokenExt> tokens)) {
                    tokens = new List<TokenExt>();
                    _lineTokens.Add(token.Line, tokens);
                }

                tokens.Add(token);
            }

            /// <summary>
            /// Tokenizes up to and including the specified line. Tokens are
            /// stored in _lineTokens.
            /// </summary>
            /// <param name="line">One-indexed line number</param>
            private void TokenizeLine(int line) {
                if (line < 1) {
                    return;
                }

                while (_tokenizer.Peek()?.Line <= line) {
                    var token = _tokenizer.Next();
                    AddToken(token);
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
                var beginCol = 0;
                var skipFirst = false;

                for (var i = 0; i < tokens.Count; i++) {
                    var token = tokens[i];

                    if (i == 0 && token.Token is ConstantValueToken) {
                        // If the line begins with a constant (rather than some whitespace),
                        // then begin the edit right after. The token will be skipped but not
                        // removed so it can still be used for formatting rules which look at
                        // the previous token.
                        beginCol = token.EndCol;
                        skipFirst = true;
                        builder.SoftAppendSpace(allowLeading: true);
                        break;
                    }

                    if (!token.IsIgnoredKind) {
                        beginCol = tokens[i - 1].EndCol;
                        break;
                    }
                }

                tokens = tokens.Where(t => !t.IsIgnoredKind).ToList();

                if (tokens.Count == 0) {
                    return NoEdits;
                }

                for (var i = 0; i < tokens.Count; i++) {
                    if (i == 0 && skipFirst) {
                        continue;
                    }

                    var token = tokens[i];
                    var prev = tokens.ElementAtOrDefault(i - 1);
                    var next = tokens.ElementAtOrDefault(i + 1);

                    if (token.Kind == TokenKind.Assign) {
                        if (token.ParenLevel > 0) {
                            // Function argument
                            builder.Append(token);
                            continue;
                        }

                        builder.SoftAppendSpace();
                        builder.Append(token);
                        builder.SoftAppendSpace();
                        continue;
                    }

                    if (token.Token is OperatorToken) {
                        HandleOperator(builder, token, prev, next);
                        continue;
                    }

                    if (token.Kind == TokenKind.Comma) {
                        builder.Append(token);
                        if (next != null && !next.IsCloseKind && next.Kind != TokenKind.Colon) {
                            builder.SoftAppendSpace();
                        }
                        continue;
                    }

                    if (token.Kind == TokenKind.Name) {
                        if (prev != null && !prev.IsOpenKind && prev.Kind != TokenKind.Colon && !(prev.Token is OperatorToken)) {
                            builder.SoftAppendSpace();
                        }

                        builder.Append(token);

                        if (token.IsKeywordWithSpaceBeforeOpen && next != null && next.IsOpenKind) {
                            builder.SoftAppendSpace();
                        }

                        continue;
                    }

                    if (token.Kind == TokenKind.Colon) {
                        builder.Append(token);

                        if (token.BracketLevel == 0 && next?.Kind != TokenKind.Colon) {
                            builder.SoftAppendSpace();
                        }

                        continue;
                    }

                    if (token.Kind == TokenKind.Comment) {
                        if (prev != null) {
                            builder.SoftAppendSpace(2);
                        }

                        builder.Append(token);
                        continue;
                    }

                    if (token.Kind == TokenKind.Semicolon) {
                        builder.Append(token);
                        continue;
                    }

                    HandleOther(builder, token, prev, next);
                }

                var edit = new TextEdit {
                    range = new Range {
                        start = new Position { line = line - 1, character = beginCol - 1 },
                        end = new Position { line = line - 1, character = tokens.Last().EndCol - 1 }
                    },
                    newText = builder.ToString()
                };

                return new[] { edit };
            }

            private void HandleOperator(TextBuilder builder, TokenExt token, TokenExt prev, TokenExt next) {
                if (token.Kind == TokenKind.Dot) {
                    if (prev?.Kind == TokenKind.KeywordFrom) {
                        builder.SoftAppendSpace();
                    }

                    builder.Append(token);

                    if (next?.Kind == TokenKind.KeywordImport) {
                        builder.SoftAppendSpace();
                    }

                    return;
                }

                if (token.Kind == TokenKind.At) {
                    if (prev != null) {
                        builder.SoftAppendSpace();
                        builder.Append(token);
                        builder.SoftAppendSpace();
                    } else {
                        builder.Append(token);
                    }

                    return;
                }

                if (token.Kind == TokenKind.Multiply) {
                    if (prev?.Kind == TokenKind.KeywordLambda) {
                        builder.SoftAppendSpace();
                        builder.Append(token);
                        return;
                    }
                }

                if (token.Kind == TokenKind.Power) {
                    if (prev?.Kind == TokenKind.KeywordLambda) {
                        builder.SoftAppendSpace();
                        builder.Append(token);
                        return;
                    }

                    if (prev == null || prev?.Kind != TokenKind.Name || prev?.Kind != TokenKind.Constant) {
                        builder.Append(token);
                        return;
                    }
                }

                if (prev != null && (prev.IsOpenKind || prev.Kind == TokenKind.Comma)) {
                    builder.Append(token);
                    return;
                }

                builder.SoftAppendSpace();
                builder.Append(token);

                if (prev?.Token is OperatorToken) {
                    if (token.Kind == TokenKind.Subtract || token.Kind == TokenKind.Add || token.Kind == TokenKind.Twiddle) {
                        return;
                    }
                }

                builder.SoftAppendSpace();
            }

            private void HandleOther(TextBuilder builder, TokenExt token, TokenExt prev, TokenExt next) {
                if (token.IsOpenCloseKind) {
                    builder.Append(token);
                    return;
                }

                if (prev?.Kind == TokenKind.Assign && token.ParenLevel > 0) {
                    builder.Append(token);
                    return;
                }

                if (prev != null && (prev.IsOpenKind || prev.Kind == TokenKind.Colon)) {
                    builder.Append(token);
                    return;
                }

                // TODO: Special case for ~ before numbers.

                builder.SoftAppendSpace();
                builder.Append(token);
            }

            private class TokenExt {
                public Token Token { get; set; }
                public int Line { get; set; }
                public int EndCol { get; set; }
                public int ParenLevel { get; set; }
                public int BraceLevel { get; set; }
                public int BracketLevel { get; set; }

                public TokenKind Kind { get { return Token.Kind; } }

                public override string ToString() {
                    return Token.VerbatimImage;
                }

                public bool IsIgnoredKind
                {
                    get
                    {
                        switch (Kind) {
                            case TokenKind.NewLine:
                            case TokenKind.NLToken:
                            case TokenKind.Indent:
                            case TokenKind.Dedent:
                                return true;
                        }
                        return false;
                    }
                }

                public bool IsOpenKind
                {
                    get
                    {
                        switch (Kind) {
                            case TokenKind.LeftBrace:
                            case TokenKind.LeftBracket:
                            case TokenKind.LeftParenthesis:
                                return true;
                        }
                        return false;
                    }
                }

                public bool IsCloseKind
                {
                    get
                    {
                        switch (Kind) {
                            case TokenKind.RightBrace:
                            case TokenKind.RightBracket:
                            case TokenKind.RightParenthesis:
                                return true;
                        }
                        return false;
                    }
                }

                public bool IsOpenCloseKind => IsOpenKind || IsCloseKind;

                public bool IsKeywordWithSpaceBeforeOpen
                {
                    get
                    {
                        switch (Kind) {
                            case TokenKind.KeywordAnd:
                            case TokenKind.KeywordAs:
                            case TokenKind.KeywordAssert:
                            case TokenKind.KeywordAwait:
                            case TokenKind.KeywordDel:
                            case TokenKind.KeywordExcept:
                            case TokenKind.KeywordElseIf:
                            case TokenKind.KeywordFor:
                            case TokenKind.KeywordFrom:
                            case TokenKind.KeywordGlobal:
                            case TokenKind.KeywordIf:
                            case TokenKind.KeywordImport:
                            case TokenKind.KeywordIn:
                            case TokenKind.KeywordIs:
                            case TokenKind.KeywordLambda:
                            case TokenKind.KeywordNonlocal:
                            case TokenKind.KeywordNot:
                            case TokenKind.KeywordOr:
                            case TokenKind.KeywordRaise:
                            case TokenKind.KeywordReturn:
                            case TokenKind.KeywordWhile:
                            case TokenKind.KeywordWith:
                            case TokenKind.KeywordYield:
                                return true;
                        }
                        return false;
                    }
                }
            }

            private class TokenizerWrapper {
                private readonly Tokenizer _tokenizer;

                private int _parenLevel = 0;
                private int _braceLevel = 0;
                private int _bracketLevel = 0;

                private TokenExt _next = null;

                public TokenizerWrapper(Tokenizer tokenizer) {
                    _tokenizer = tokenizer;
                }

                public TokenExt Next() {
                    if (_tokenizer.IsEndOfFile) {
                        return null;
                    }

                    if (_next != null) {
                        var tmp = _next;
                        _next = null;
                        return tmp;
                    }

                    var token = _tokenizer.GetNextToken();

                    switch (token.Kind) {
                        case TokenKind.LeftParenthesis:
                            _parenLevel++;
                            break;
                        case TokenKind.RightParenthesis:
                            _parenLevel--;
                            break;
                        case TokenKind.LeftBrace:
                            _braceLevel++;
                            break;
                        case TokenKind.RightBrace:
                            _braceLevel--;
                            break;
                        case TokenKind.LeftBracket:
                            _bracketLevel++;
                            break;
                        case TokenKind.RightBracket:
                            _bracketLevel--;
                            break;
                    }

                    return new TokenExt {
                        Token = token,
                        Line = _tokenizer.CurrentPosition.Line,
                        EndCol = _tokenizer.CurrentPosition.Column,
                        ParenLevel = _parenLevel,
                        BraceLevel = _braceLevel,
                        BracketLevel = _bracketLevel
                    };
                }

                public TokenExt Peek() {
                    if (_next != null) {
                        return _next;
                    }

                    _next = Next();
                    return _next;
                }
            }
        }
    }
}
