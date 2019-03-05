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
                    NextChar();
                    _buffer.Append(NextChar());
                } else if (IsNext(doubleClose)) {
                    NextChar();
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

        private Expression ParseFStringExpression(int recursLevel = 0) {
            if (recursLevel >= 2) {
                ReportSyntaxError("");
            }
            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");

            // Called on '{'
            Debug.Assert(CurrentChar() != '{', "Open brace expected");
            NextChar();

            int startExprPosition = _position;
            int startExprLineNumber = _currentLineNumber;
            int startExprColNumber = _currentColNumber;
            Stack<char> nestedParens = new Stack<char>();
            var errorReported = false;
            while (!(EndOfFString())) {
                char ch = CurrentChar();
                if (nestedParens.Count == 0 && (ch == '}' || ch == '!' || ch == ':')) {
                    // check that it's not a != comparison
                    if (ch != '!' || !IsNext(notEqualStringSpan)) {
                        break;
                    }
                }
                errorReported = false;

                if (ch == '#') {
                    /* Error: can't include a comment character, inside parens
                       or not. */
                    ReportSyntaxError("f-string expression part cannot include '#'");
                    errorReported = true;
                } else if (ch == ')' || ch == '}') {
                    _buffer.Append(ch);
                    if (nestedParens.Pop() != ch) {
                        ReportSyntaxError("");
                        errorReported = true;
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
                    ReportSyntaxError("");
                } else {
                    ReportSyntaxError("");
                }
                return new ErrorExpression(_buffer.ToString(), null);
            }
            Expression fStringExpression;
            if (CurrentChar() == '}' || CurrentChar() == '!' || CurrentChar() == ':') {

                string subExprStr = _buffer.ToString();
                _buffer.Clear();
                fStringExpression = CreateExpression(subExprStr,
                        startExprPosition, startExprLineNumber, startExprColNumber);

                if (CurrentChar() == '}') {
                    return fStringExpression;
                }
            } else {
                // try to recover
                //return ErrorExpression();
                return null;
            }
            var conversion = ' ';
            Expression formatSpecifier = null;
            if (CurrentChar() == '!') {
                Read('!');
                conversion = NextChar();
                if (!(conversion == 's' || conversion == 'r' || conversion == 'a')) {
                    ReportSyntaxError($"f-string: invalid conversion character: {conversion} expected 's', 'r', or 'a'");
                }
            }
            if (CurrentChar() == ':') {
                Read(':');
                var formatBuilder = new FStringBuilder();
                /*while(!)
                formatSpecifier = new FStringParser(_builder, )*/

            }
            return new FormattedValue(fStringExpression, conversion, formatSpecifier);
        }

        private Expression CreateExpression(string subExprStr, int startExprPosition, int startExprLineNumber,
            int startExprColNumber) {
            if (subExprStr.IsNullOrEmpty()) {

            }
            var parser = Parser.CreateParser(new StringReader(subExprStr), _langVersion, new ParserOptions() {
                ErrorSink = _errors,
                InitialSourceLocation = new SourceLocation(_start.Index + startExprPosition, startExprLineNumber, startExprColNumber)
            });
            var expr = parser.ParseFStrSubExpr();
            if (expr is null) {
                // Should not happen but just in case
                ReportSyntaxError("Subexpression failed to parse");
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
