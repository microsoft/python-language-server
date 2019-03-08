﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringParser {
        private readonly FStringBuilder _builder;
        private readonly string _fString;
        private readonly ErrorSink _errors;
        private readonly PythonLanguageVersion _langVersion;
        private readonly StringBuilder _buffer = new StringBuilder();
        private int _position = 0;
        private int _currentLineNumber;
        private int _currentColNumber;
        private bool _hasErrors = false;
        private readonly Stack<char> _nestedParens = new Stack<char>();
        private readonly SourceLocation _start;
        private static readonly StringSpan doubleOpen = new StringSpan("{{", 0, 2);
        private static readonly StringSpan doubleClose = new StringSpan("}}", 0, 2);
        private static readonly StringSpan notEqualStringSpan = new StringSpan("!=", 0, 2);

        public FStringParser(FStringBuilder builder, string fString, ErrorSink errors, PythonLanguageVersion langVersion,
            SourceLocation start) {

            _fString = fString;
            _builder = builder;
            _errors = errors;
            _langVersion = langVersion;
            _currentLineNumber = start.Line;
            // Adding offset because of f-string start: "f'"
            _currentColNumber = start.Column + 2;
            _start = start;
        }

        public void Parse() {
            while (!EndOfFString()) {
                if (IsNext(doubleOpen)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                } else if (IsNext(doubleClose)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                } else if (CurrentChar() == '{') {
                    AddBufferedSubstring();
                    ParseInnerExpression();
                } else if (CurrentChar() == '}') {
                    ReportSyntaxError(Resources.SingleClosedBraceFStringErrorMsg);
                    _buffer.Append(NextChar());
                } else {
                    _buffer.Append(NextChar());
                }
            }
            AddBufferedSubstring();
        }

        private bool IsNext(StringSpan span)
            => _fString.Slice(_position, span.Length).Equals(span);

        private void ParseInnerExpression() {
            _builder.Append(ParseFStringExpression());
        }

        private Node ParseFStringExpression() {
            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");

            Read('{');
            var initialPosition = _position;
            var initialSourceLocation = new SourceLocation(0, _currentLineNumber, _currentColNumber);

            BufferInnerExpression();

            if (EndOfFString()) {
                if (_nestedParens.Count > 0) {
                    ReportSyntaxError(Resources.UnmatchedFStringErrorMsg.FormatInvariant(_nestedParens.Peek()));
                    _nestedParens.Clear();
                } else {
                    ReportSyntaxError(Resources.ExpectingCharFStringErrorMsg.FormatInvariant('}'));
                }
                return new ErrorExpression(_buffer.ToString(), null);
            }
            if (_hasErrors) {
                var expr = _buffer.ToString();
                _buffer.Clear();
                return new ErrorExpression(expr, null);
            }

            Debug.Assert(CurrentChar() == '}' || CurrentChar() == '!' || CurrentChar() == ':');

            var fStringExpression = CreateExpression(_buffer.ToString(), initialSourceLocation);
            _buffer.Clear();

            var conversion = MaybeReadConversionChar();
            var formatSpecifier = MaybeReadFormatSpecifier();
            Read('}');

            if (_hasErrors) {
                return new ErrorExpression(_fString.Substring(initialPosition, _position - initialPosition), null);
            }
            return new FormattedValue(fStringExpression, conversion, formatSpecifier);
        }

        private Expression MaybeReadFormatSpecifier() {
            Debug.Assert(_buffer.Length == 0);

            Expression formatSpecifier = null;
            if (CurrentChar() == ':') {
                Read(':');
                var start = new SourceLocation(_start.Index + _position, _currentLineNumber, _currentColNumber);
                /* Ideally we would just call the FStringParser here. But we are relying on 
                 * an already cut of string, so we need to find the end of the format 
                 * specifier. */
                BufferFormatSpecifier();

                // If we got to the end, there will be an error when we try to read '}'
                if (!EndOfFString()) {
                    var formatSpecifierBuilder = new FStringBuilder();
                    new FStringParser(formatSpecifierBuilder, _buffer.ToString(), _errors, _langVersion, start).Parse();
                    _buffer.Clear();
                    formatSpecifier = formatSpecifierBuilder.Build();
                }
            }

            return formatSpecifier;
        }

        private char? MaybeReadConversionChar() {
            char? conversion = null;
            if (CurrentChar() == '!') {
                Read('!');
                conversion = NextChar();
                if (!(conversion == 's' || conversion == 'r' || conversion == 'a')) {
                    ReportSyntaxError(Resources.InvalidConversionCharacterFStringErrorMsg.FormatInvariant(conversion));
                }
            }

            return conversion;
        }

        private void BufferInnerExpression() {
            Debug.Assert(_nestedParens.Count == 0);

            char? quoteChar = null;
            int stringType = 0;

            while (!EndOfFString()) {
                var ch = CurrentChar();
                if (!quoteChar.HasValue && _nestedParens.Count == 0 && (ch == '}' || ch == '!' || ch == ':')) {
                    // check that it's not a != comparison
                    if (ch != '!' || !IsNext(notEqualStringSpan)) {
                        break;
                    }
                }
                if (HasBackslash(ch)) {
                    ReportSyntaxError(Resources.BackslashFStringExpressionErrorMsg);
                    _buffer.Append(NextChar());
                    continue;
                }

                if (quoteChar.HasValue) {
                    HandleInsideString(ref quoteChar, ref stringType);
                } else {
                    HandleInnerExprOutsideString(_nestedParens, ref quoteChar, ref stringType);
                }
            }
        }

        private void BufferFormatSpecifier() {
            Debug.Assert(_nestedParens.Count == 0);

            char? quoteChar = null;
            int stringType = 0;

            while (!EndOfFString()) {
                var ch = CurrentChar();
                if (!quoteChar.HasValue && _nestedParens.Count == 0 && (ch == '}' || ch == '!' || ch == ':')) {
                    // check that it's not a != comparison
                    if (ch != '!' || !IsNext(notEqualStringSpan)) {
                        break;
                    }
                }

                if (quoteChar.HasValue) {
                    /* We're inside a string. See if we're at the end. */
                    HandleInsideString(ref quoteChar, ref stringType);
                } else {
                    HandleFormatSpecOutsideString(_nestedParens, ref quoteChar, ref stringType);
                }
            }
        }
        private void HandleFormatSpecOutsideString(Stack<char> nestedParens, ref char? quoteChar, ref int stringType) {
            Debug.Assert(!quoteChar.HasValue);

            var ch = CurrentChar();
            if (ch == '\'' || ch == '"') {
                /* Is this a triple quoted string? */
                quoteChar = ch;
                if (IsNext(new StringSpan($"{ch}{ch}{ch}", 0, 3))) {
                    stringType = 3;
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    return;
                } else {
                    /* Start of a normal string. */
                    stringType = 1;
                }
                /* Start looking for the end of the string. */
            } else if (ch == ')' || ch == '}') {
                char opening = nestedParens.Pop();
                if (!((opening == '{' && ch == '}') ||
                    (opening == '(' && ch == ')'))) {
                    ReportSyntaxError(Resources.ClosingParensNotMatchFStringErrorMsg.FormatInvariant(ch, opening));
                }
            } else if (ch == '(' || ch == '{') {
                nestedParens.Push(ch);
            }

            _buffer.Append(NextChar());
        }

        private void HandleInnerExprOutsideString(Stack<char> nestedParens, ref char? quoteChar, ref int stringType) {
            Debug.Assert(!quoteChar.HasValue);

            var ch = CurrentChar();
            if (ch == '\'' || ch == '"') {
                /* Is this a triple quoted string? */
                quoteChar = ch;
                if (IsNext(new StringSpan($"{ch}{ch}{ch}", 0, 3))) {
                    stringType = 3;
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    return;
                } else {
                    /* Start of a normal string. */
                    stringType = 1;
                }
                /* Start looking for the end of the string. */
            } else if (ch == '#') {
                ReportSyntaxError(Resources.NumberSignFStringExpressionErrorMsg);
            } else if (ch == ')' || ch == '}') {
                char opening = nestedParens.Pop();
                if (!((opening == '{' && ch == '}') ||
                    (opening == '(' && ch == ')'))) {
                    ReportSyntaxError(Resources.ClosingParensNotMatchFStringErrorMsg.FormatInvariant(ch, opening));
                }
            } else if (ch == '(' || ch == '{') {
                nestedParens.Push(ch);
            }

            _buffer.Append(NextChar());
        }

        private void HandleInsideString(ref char? quoteChar, ref int stringType) {
            Debug.Assert(quoteChar.HasValue);

            var ch = CurrentChar();
            /* We're inside a string. See if we're at the end. */
            if (ch == quoteChar.Value) {
                /* Does this match the string_type (single or triple
                   quoted)? */
                if (stringType == 3) {
                    if (IsNext(new StringSpan($"{ch}{ch}{ch}", 0, 3))) {
                        /* We're at the end of a triple quoted string. */
                        _buffer.Append(NextChar());
                        _buffer.Append(NextChar());
                        _buffer.Append(NextChar());
                        stringType = 0;
                        quoteChar = null;
                        return;
                    }
                } else {
                    /* We're at the end of a normal string. */
                    quoteChar = null;
                    stringType = 0;
                }
            }
            _buffer.Append(NextChar());
        }

        private Expression CreateExpression(string subExprStr, SourceLocation initialSourceLocation) {
            if (subExprStr.IsNullOrEmpty()) {
                ReportSyntaxError(Resources.EmptyExpressionFStringErrorMsg);
                return new ErrorExpression(subExprStr, null);
            }
            var parser = Parser.CreateParser(new StringReader(subExprStr.TrimStart(' ')), _langVersion, new ParserOptions() {
                ErrorSink = _errors,
                InitialSourceLocation = initialSourceLocation
            });
            var expr = parser.ParseFStrSubExpr();
            if (expr is null) {
                // Should not happen but just in case
                ReportSyntaxError(Resources.InvalidExpressionFStringErrorMsg);
                return new ErrorExpression(subExprStr, null);
            }
            return expr;
        }

        private bool Read(char nextChar) {
            if (EndOfFString()) {
                ReportSyntaxError(Resources.ExpectingCharFStringErrorMsg.FormatInvariant(nextChar));
                return false;
            }
            char ch = CurrentChar();
            NextChar();

            if (ch != nextChar) {
                ReportSyntaxError(Resources.ExpectingCharButFoundFStringErrorMsg.FormatInvariant(nextChar, CurrentChar()));
                return false;
            }
            return true;
        }

        private void AddBufferedSubstring() {
            if (_buffer.Length == 0) {
                return;
            }
            var s = _buffer.ToString();
            _builder.Append(s);
            _buffer.Clear();
        }

        private char NextChar() {
            var prev = CurrentChar();
            _position++;
            _currentColNumber++;
            if (prev == '\n') {
                _currentColNumber = 1;
                _currentLineNumber++;
            }
            return prev;
        }

        private bool HasBackslash(char ch) {
            return "\b\f\n\r\t\v\\".Contains($"{ch}");
        }

        private char CurrentChar() => _fString[_position];

        private bool EndOfFString() => _position >= _fString.Length;

        private void ReportSyntaxError(string message) {
            if (!_hasErrors) {
                _hasErrors = true;

                var span = new SourceSpan(new SourceLocation(_start.Index + _position, _currentLineNumber, _currentColNumber),
                new SourceLocation(_start.Index + _position + 1, _currentLineNumber, _currentColNumber + 1));
                _errors.Add(message, span, ErrorCodes.SyntaxError, Severity.Error);
            }
        }
    }
}
