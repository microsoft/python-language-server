using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringParser {
        private readonly FStringBuilder _builder;
        private readonly string _fString;
        private readonly ErrorSink _errors;
        private readonly PythonLanguageVersion _langVersion;
        private readonly StringBuilder _buffer = new StringBuilder();
        private int _errorCode = 0;
        private int _position = 0;

        private static readonly StringSpan doubleOpen = new StringSpan("{{", 0, 2);
        private static readonly StringSpan doubleClose = new StringSpan("}}", 0, 2);

        public FStringParser(FStringBuilder builder, string fString, ErrorSink errors, PythonLanguageVersion langVersion) {
            _fString = fString;
            _builder = builder;
            _errors = errors;
            _langVersion = langVersion;
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
                    throw new Exception("closing '}' without opening");
                } else {
                    _buffer.Append(NextChar());
                }
            }
            AddBufferedSubstring();
        }

        private bool IsNext(StringSpan span)
            => _fString.Slice(_position, span.Length).Equals(span);

        private void ParseInnerExpression() {
            Debug.Assert(_buffer.Length == 0, "Current buffer is not empty");

            Read('{');
            AppendAllSubExpression();
            Read('}');

            var subExprStr = _buffer.ToString();
            if (!subExprStr.IsNullOrEmpty()) {
                _builder.AppendExpression(CreateExpression(_buffer.ToString()));
            }
            _buffer.Clear();
        }

        private Expression CreateExpression(string subExprStr) {
            var parser = Parser.CreateParser(new StringReader(subExprStr), _langVersion);
            var expr = parser.ParseFStrSubExpr(out var formatExpression, out var conversionExpression);
            if (expr is null) {
                // Should not happen but just in case
                ReportSyntaxError("Subexpression failed to parse");
                return new ConstantExpression("");
            }

            if (formatExpression != null || conversionExpression != null) {
                return new FSubExpressionWithOptions(expr, formatExpression, conversionExpression);
            } else {
                return expr;
            }
        }

        private void AppendAllSubExpression() {
            int openedBraces = 1;
            while (!(CurrentChar() == '}' && openedBraces == 1)) {
                if (EndOfFString()) {
                    ReportSyntaxError("Inner expression without closing '}'");
                }

                if (CurrentChar() == '{') {
                    openedBraces++;
                }
                if (CurrentChar() == '}') {
                    openedBraces--;
                }
                _buffer.Append(NextChar());
            }
        }

        private void Read(char nextChar) {
            if (CurrentChar() != nextChar) {
                ReportSyntaxError($"'{nextChar}' expected but found '{CurrentChar()}'");
            }
            NextChar();
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
            var peek = CurrentChar();
            _position++;
            return peek;
        }

        private char CurrentChar() => _fString[_position];

        private bool EndOfFString() => _position >= _fString.Length;

        private void ReportSyntaxError(string message) { }
        //private void ReportSyntaxError(string message) => ReportSyntaxError(_lookahead.Span.Start, _lookahead.Span.End, message);

        private void ReportSyntaxError(int start, int end, string message) => ReportSyntaxError(start, end, message, ErrorCodes.SyntaxError);

        private void ReportSyntaxError(int start, int end, string message, int errorCode) {
            // save the first one, the next error codes may be induced errors:
            if (_errorCode == 0) {
                _errorCode = errorCode;
            }
            /*
            _errors.Add(
                message,
                _tokenizer.GetLineLocations(),
                start, end,
                errorCode,
                Severity.Error);*/
        }
    }
}
