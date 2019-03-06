using System.Collections.Generic;
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
                    ReportSyntaxError("f-string: single '}' is not allowed");
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
            _builder.AppendExpression(ParseFStringExpression());
        }

        private Expression ParseFStringExpression() {
            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");
            Read('{');
            var initialSourceLocation = new SourceLocation(0, _currentLineNumber, _currentColNumber);

            var nestedParens = new Stack<char>();
            var errorReported = false;
            while (!EndOfFString()) {
                var ch = CurrentChar();
                if (nestedParens.Count == 0 && (ch == '}' || ch == '!' || ch == ':')) {
                    // check that it's not a != comparison
                    if (ch != '!' || !IsNext(notEqualStringSpan)) {
                        break;
                    }
                }
                if (ch == '\\') {
                    ReportSyntaxError("f-string expression part cannot include a backslash");
                    errorReported = true;
                    _buffer.Append(ch);
                } else if (ch == '#') {
                    /* Error: can't include a comment character, inside parens
                       or not. */
                    ReportSyntaxError("f-string expression part cannot include '#'");
                    errorReported = true;
                    _buffer.Append(ch);
                } else if (ch == ')' || ch == '}') {
                    _buffer.Append(ch);
                    char opening = nestedParens.Pop();
                    if (!((opening == '{' && ch == '}') ||
                        (opening == '(' && ch == ')') ||
                        (opening == '[' && ch == ']'))) {
                        errorReported = true;
                        ReportSyntaxError($"f-string: closing parenthesis '{ch}' does not match opening parenthesis '{opening}'");
                    }
                } else if (ch == '(' || ch == '{') {
                    nestedParens.Push(ch);
                    _buffer.Append(ch);
                } else {
                    _buffer.Append(ch);
                }

                NextChar();
            }
            if (EndOfFString()) {
                if (nestedParens.Count > 0) {
                    ReportSyntaxError($"f-string: unmatched {nestedParens.Peek()}");
                } else {
                    ReportSyntaxError("f-string: expecting '}'");
                }
                return new ErrorExpression(_buffer.ToString(), null);
            }
            if (errorReported) {
                var expr = _buffer.ToString();
                _buffer.Clear();
                Read('}');
                return new ErrorExpression(expr, null);
            }

            Debug.Assert(CurrentChar() == '}' || CurrentChar() == '!' || CurrentChar() == ':');

            var fStringExpression = CreateExpression(_buffer.ToString(), initialSourceLocation);
            _buffer.Clear();
            if (CurrentChar() == '}') {
                Read('}');
                return fStringExpression;
            }

            var conversion = MaybeReadConversionChar();
            var formatSpecifier = MaybeReadFormatSpecifier();
            Read('}');
            return new FormattedValue(fStringExpression, conversion, formatSpecifier);
        }

        private Expression MaybeReadFormatSpecifier() {
            Expression formatSpecifier = null;
            if (CurrentChar() == ':') {
                Read(':');
                var start = new SourceLocation(_start.Index + _position, _currentLineNumber, _currentColNumber);
                var openedBraces = 1;
                Debug.Assert(_buffer.Length == 0);
                while (!EndOfFString()) {
                    if (CurrentChar() == '}') {
                        openedBraces--;
                        if (openedBraces == 0) {
                            break;
                        }
                    } else if (CurrentChar() == '{') {
                        openedBraces++;
                    }
                    _buffer.Append(NextChar());
                }
                if (openedBraces == 0) {
                    var builder = new FStringBuilder();
                    new FStringParser(builder, _buffer.ToString(), _errors, _langVersion, start).Parse();
                    _buffer.Clear();
                    formatSpecifier = builder.Build();
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
                    ReportSyntaxError($"f-string: invalid conversion character: {conversion} expected 's', 'r', or 'a'");
                }
            }

            return conversion;
        }

        private Expression CreateExpression(string subExprStr, SourceLocation initialSourceLocation) {
            if (subExprStr.IsNullOrEmpty()) {
                ReportSyntaxError("f-string: empty expression not allowed");
                return new ErrorExpression(subExprStr, null);
            }
            var parser = Parser.CreateParser(new StringReader(subExprStr), _langVersion, new ParserOptions() {
                ErrorSink = _errors,
                InitialSourceLocation = initialSourceLocation
            });
            var expr = parser.ParseFStrSubExpr();
            if (expr is null) {
                // Should not happen but just in case
                ReportSyntaxError("f-string: invalid expression");
                return new ErrorExpression(subExprStr, null);
            }
            return expr;
        }

        private bool Read(char nextChar) {
            if (EndOfFString()) {
                ReportSyntaxError($"f-string: expecting '{nextChar}'");
                return false;
            }
            if (CurrentChar() != nextChar) {
                ReportSyntaxError($"f-string: expecting '{nextChar}' but found '{CurrentChar()}'");
                return false;
            }
            NextChar();
            return true;
        }

        private void AddBufferedSubstring() {
            if (_buffer.Length == 0) {
                return;
            }
            var s = _buffer.ToString();
            _builder.AppendString(s);
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

        private char CurrentChar() => _fString[_position];

        private bool EndOfFString() => _position >= _fString.Length;

        private void ReportSyntaxError(string message) {
            var span = new SourceSpan(new SourceLocation(_start.Index + _position, _currentLineNumber, _currentColNumber),
                new SourceLocation(_start.Index + _position + 1, _currentLineNumber, _currentColNumber + 1));
            _errors.Add(message, span, ErrorCodes.SyntaxError, Severity.Error);
        }
    }
}
