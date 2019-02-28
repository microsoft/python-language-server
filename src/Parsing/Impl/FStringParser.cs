using System;
using System.IO;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringParser {
        private readonly string _fString;
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly FStringBuilder _builder;
        private readonly ErrorSink _errors;
        private int _errorCode = 0;
        private int _position = 0;

        public FStringParser(FStringBuilder builder, string fString, ErrorSink errors) {
            _fString = fString;
            _builder = builder;
            _errors = errors;
        }

        public void Parse() {
            while (!EndOfFString()) {
                if (IsDoubleBrace()) {
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

        private bool IsDoubleBrace() {
            StringSpan doubleOpen = new StringSpan("{{", 0, 2);
            StringSpan doubleClose = new StringSpan("}}", 0, 2);
            return IsNext(doubleOpen) || IsNext(doubleClose);
        }

        private bool IsNext(StringSpan span) {
            return _fString.Slice(_position, span.Length).Equals(span);
        }

        private void ParseInnerExpression() {
            Check.InvalidOperation(_buffer.Length == 0, "Current buffer is not empty");

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
            Check.ArgumentNotNullOrEmpty(nameof(subExprStr), subExprStr);

            var parser = Parser.CreateParser(new StringReader(subExprStr), PythonLanguageVersion.V36);
            var expr = parser.ParseFStrSubExpr(out var formatExpression, out var conversionExpression);
            if (expr is null) {
                ReportSyntaxError("Subexpression failed to parse");
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
                    throw new Exception("Inner expression without closing '}'");
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
                throw new Exception($"'{nextChar}' expected but found '{CurrentChar()}'");
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

        private char CurrentChar() {
            return _fString[_position];
        }

        private bool EndOfFString() {
            return _position >= _fString.Length;
        }

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
