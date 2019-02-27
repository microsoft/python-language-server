using System;
using System.Collections.Generic;
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
        private readonly List<Expression> _children = new List<Expression>();
        private int _position = 0;

        public FStringParser(string fString) {
            _fString = fString;
        }

        public FStringExpression Parse() {
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
            return new FStringExpression(_children, _fString);
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
                _children.Add(CreateExpression(_buffer.ToString()));
            }
            _buffer.Clear();
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
            _children.Add(new ConstantExpression(s));
            _buffer.Clear();
        }

        private Expression CreateExpression(string subExprStr) {
            Check.ArgumentNotNullOrEmpty(nameof(subExprStr), subExprStr);

            var parser = Parser.CreateParser(new StringReader(subExprStr), PythonLanguageVersion.V36);
            var expr = Statement.GetExpression(parser.ParseTopExpression().Body);
            if (expr is null) {
                throw new Exception("Expression failed to parse");
            }
            return expr;
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
    }
}
