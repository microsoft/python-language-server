using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Python.Core.Diagnostics;
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
            while (HasNextChar()) {
                if (IsDoubleBrace()) {
                    BufferEscapedBracesSubExpr();
                } else if (PeekChar() == '{') {
                    AddBufferedSubstring();
                    ParseInnerExpression();
                } else {
                    _buffer.Append(NextChar());
                }
            }
            AddBufferedSubstring();
            return new FStringExpression(_children, _fString);
        }

        private bool IsDoubleBrace() {
            return IsDoubleChar('{') || IsDoubleChar('}');
        }

        private bool IsDoubleChar(char next) {
            if (_position >= _fString.Length - 1) {
                return false;
            }
            var doubleNextChar = _fString[_position + 1];
            return PeekChar() == next && doubleNextChar == next;
        }

        private void ParseInnerExpression() {
            Read('{');
            while (HasNextChar() && PeekChar() != '}') {
                if (PeekChar() == '{') {
                    ParseInnerExpression();
                } else {
                    _buffer.Append(NextChar());
                }
            }
            if (!HasNextChar()) {
                throw new Exception("Inner expression without closing '}'");
            }

            _children.Add(ParseChildExpression(_buffer.ToString()));
            _buffer.Clear();

            Read('}');
        }

        private void BufferEscapedBracesSubExpr() {
            Check.InvalidOperation(IsDoubleBrace());

            var brace = NextChar();
            Read(brace);
            _buffer.Append(brace);
            while (HasNextChar()) {
                if (IsDoubleChar('}')) {
                    Read('}');
                    Read('}');
                    _buffer.Append('}');
                    return;
                } else if (PeekChar() == '}') {
                    throw new Exception("single '}' is not allowed");
                } else {
                    _buffer.Append(NextChar());
                }
            }
            throw new Exception("End of double '}' not found");
        }

        private void Read(char nextChar) {
            if (PeekChar() != nextChar) {
                throw new Exception();
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

        private Expression ParseChildExpression(string x) {
            var parser = Parser.CreateParser(new StringReader(x), PythonLanguageVersion.V36);
            var expr = Statement.GetExpression(parser.ParseTopExpression().Body);
            if (expr is null) {
                throw new Exception();
            }
            return expr;
        }

        private char NextChar() {
            var peek = PeekChar();
            _position++;
            return peek;
        }

        private char PeekChar() {
            return _fString[_position];
        }

        private bool HasNextChar() {
            return _position < _fString.Length;
        }
    }
}
