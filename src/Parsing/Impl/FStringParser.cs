using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringParser {
        private readonly IFStringBuilder _builder;
        private readonly string _fString;
        private readonly bool _isRaw;
        private readonly ErrorSink _errors;
        private readonly ParserOptions _options;
        private readonly PythonLanguageVersion _langVersion;
        private readonly StringBuilder _buffer = new StringBuilder();
        private int _position = 0;
        private int _currentLineNumber;
        private readonly bool _verbatim;
        private int _currentColNumber;
        private bool _hasErrors = false;
        private readonly Stack<char> _nestedParens = new Stack<char>();
        private readonly SourceLocation _start;
        private static readonly StringSpan DoubleOpen = new StringSpan("{{", 0, 2);
        private static readonly StringSpan DoubleClose = new StringSpan("}}", 0, 2);
        private static readonly StringSpan NotEqualStringSpan = new StringSpan("!=", 0, 2);
        private static readonly StringSpan BackslashN = new StringSpan("\\N", 0, 2);

        public FStringParser(IFStringBuilder builder, string fString, bool isRaw,
            ParserOptions options, PythonLanguageVersion langVersion) {

            _fString = fString;
            _isRaw = isRaw;
            _builder = builder;
            _errors = options.ErrorSink ?? ErrorSink.Null;
            _options = options;
            _langVersion = langVersion;
            _verbatim = options.Verbatim;
            _start = options.InitialSourceLocation ?? SourceLocation.MinValue;
            _currentLineNumber = _start.Line;
            _currentColNumber = _start.Column;
        }

        public void Parse() {
            var bufferStartLoc = CurrentLocation();
            while (!EndOfFString()) {
                if (IsNext(DoubleOpen)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                } else if (IsNext(DoubleClose)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                } else if (!_isRaw && IsNext(BackslashN)) {
                    _buffer.Append(NextChar());
                    _buffer.Append(NextChar());
                    if (CurrentChar() == '{') {
                        Read('{');
                        _buffer.Append('{');
                        while (!EndOfFString() && CurrentChar() != '}') {
                            _buffer.Append(NextChar());
                        }
                        if (EndOfFString()) {
                            ReportSyntaxError(Resources.ExpectingCharFStringErrorMsg.FormatInvariant('}'));
                        }
                        Read('}');
                        _buffer.Append('}');
                    } else {
                        _buffer.Append(NextChar());
                    }
                } else if (CurrentChar() == '{') {
                    AddBufferedSubstring(bufferStartLoc);
                    ParseInnerExpression();
                    bufferStartLoc = CurrentLocation();
                } else if (CurrentChar() == '}') {
                    ReportSyntaxError(Resources.SingleClosedBraceFStringErrorMsg);
                    _buffer.Append(NextChar());
                } else {
                    _buffer.Append(NextChar());
                }
            }
            AddBufferedSubstring(bufferStartLoc);
        }

        private bool IsNext(StringSpan span)
            => _fString.Slice(_position, span.Length).Equals(span);

        private void ParseInnerExpression() {
            _builder.Append(ParseFStringExpression());
        }

        private Node ParseFStringExpression() {
            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");

            var startOfFormattedValue = CurrentLocation().Index;
            Read('{');
            var initialPosition = _position;
            SourceLocation initialSourceLocation = CurrentLocation();

            BufferInnerExpression();
            Expression fStringExpression = null;
            FormattedValue formattedValue;

            if (EndOfFString()) {
                if (_nestedParens.Count > 0) {
                    ReportSyntaxError(Resources.UnmatchedFStringErrorMsg.FormatInvariant(_nestedParens.Peek()));
                    _nestedParens.Clear();
                } else {
                    ReportSyntaxError(Resources.ExpectingCharFStringErrorMsg.FormatInvariant('}'));
                }
                if (_buffer.Length != 0) {
                    fStringExpression = CreateExpression(_buffer.ToString(), initialSourceLocation);
                    _buffer.Clear();
                } else {
                    fStringExpression = Error(initialPosition);
                }
                formattedValue = new FormattedValue(fStringExpression, null, null);
                formattedValue.SetLoc(new IndexSpan(startOfFormattedValue, CurrentLocation().Index - startOfFormattedValue));
                return formattedValue;
            }
            if (!_hasErrors) {
                fStringExpression = CreateExpression(_buffer.ToString(), initialSourceLocation);
                _buffer.Clear();
            } else {
                // Clear and recover
                _buffer.Clear();
            }

            Debug.Assert(CurrentChar() == '}' || CurrentChar() == '!' || CurrentChar() == ':');

            var conversion = MaybeReadConversionChar();
            var formatSpecifier = MaybeReadFormatSpecifier();
            Read('}');

            if (fStringExpression == null) {
                return Error(initialPosition);
            }
            formattedValue = new FormattedValue(fStringExpression, conversion, formatSpecifier);
            formattedValue.SetLoc(new IndexSpan(startOfFormattedValue, CurrentLocation().Index - startOfFormattedValue));

            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");
            return formattedValue;
        }

        private SourceLocation CurrentLocation() {
            return new SourceLocation(_start.Index + _position, _currentLineNumber, _currentColNumber);
        }

        private Expression MaybeReadFormatSpecifier() {
            Debug.Assert(_buffer.Length == 0);

            Expression formatSpecifier = null;
            if (!EndOfFString() && CurrentChar() == ':') {
                Read(':');
                var position = _position;
                /* Ideally we would just call the FStringParser here. But we are relying on 
                 * an already cut of string, so we need to find the end of the format 
                 * specifier. */
                BufferFormatSpecifier();

                // If we got to the end, there will be an error when we try to read '}'
                if (!EndOfFString()) {
                    var formatSpecifierBuilder = new FormatSpecifierBuilder();
                    var options = _options.Clone();
                    options.InitialSourceLocation = new SourceLocation(
                        FStringStartIndex() + position,
                        _currentLineNumber,
                        _currentColNumber
                    );
                    var formatStr = _buffer.ToString();
                    _buffer.Clear();
                    new FStringParser(formatSpecifierBuilder, formatStr, _isRaw, options, _langVersion).Parse();
                    formatSpecifier = formatSpecifierBuilder.Build();
                    formatSpecifier.SetLoc(new IndexSpan(FStringStartIndex() + position, formatStr.Length));
                }
            }

            return formatSpecifier;
        }

        // Adding offset because of f-string start: "f'"
        private int FStringStartIndex() => _start.Index;

        private char? MaybeReadConversionChar() {
            char? conversion = null;
            if (!EndOfFString() && CurrentChar() == '!') {
                Read('!');
                if (EndOfFString()) {
                    ReportSyntaxError(Resources.InvalidConversionCharacterFStringErrorMsg.FormatInvariant(' '));
                    return null;
                }
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
                    if (ch != '!' || !IsNext(NotEqualStringSpan)) {
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
                if (!quoteChar.HasValue && _nestedParens.Count == 0 && (ch == '}')) {
                    // check that it's not a != comparison
                    if (ch != '!' || !IsNext(NotEqualStringSpan)) {
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
                InitialSourceLocation = initialSourceLocation,
                ParseFStringExpression = true
            });
            var expr = parser.ParseFStrSubExpr();
            if (expr is null) {
                // Should not happen but just in case
                ReportSyntaxError(Resources.InvalidExpressionFStringErrorMsg);
                return Error(_position - subExprStr.Length);
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

        private void AddBufferedSubstring(SourceLocation bufferStartLoc) {
            if (_buffer.Length == 0) {
                return;
            }
            var s = _buffer.ToString();
            try {
                _builder.Append(s, _isRaw);
            } catch (DecoderFallbackException e) {
                var span = new SourceSpan(bufferStartLoc, CurrentLocation());
                _errors.Add(e.Message, span, ErrorCodes.SyntaxError, Severity.Error);
            }
            _buffer.Clear();
        }

        private char NextChar() {
            var prev = CurrentChar();
            _position++;
            _currentColNumber++;
            if (IsLineEnding(prev)) {
                _currentColNumber = 1;
                _currentLineNumber++;
            }
            return prev;
        }

        private bool IsLineEnding(char prev) => prev == '\n' || (prev == '\\' && IsNext(new StringSpan("n", 0, 1)));

        private bool HasBackslash(char ch) => ch == '\\';

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

        private ErrorExpression Error(int startPos, string verbatimImage = null, Expression preceding = null) {
            verbatimImage = verbatimImage ?? (_fString.Substring(startPos, _position - startPos));
            var expr = new ErrorExpression(verbatimImage, preceding);
            expr.SetLoc(_start.Index + startPos, _start.Index + _position);
            return expr;
        }
    }
}
