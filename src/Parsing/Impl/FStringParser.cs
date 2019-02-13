using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringParser {
        private readonly string _fString;
        private readonly StringBuilder _buffer;
        private readonly List<Expression> _children;
        private int _position;

        public FStringParser(string fString) {
            _fString = fString;
            _position = 0;
            _buffer = new StringBuilder();
            _children = new List<Expression>();
        }

        public FStringExpression Parse() {
            while (HasNextChar()) {
                if (PeekChar() != '{') {
                    _buffer.Append(NextChar());
                } else {
                    AddBufferedSubstring();
                    ParseInnerExpression();
                }
            }

            AddBufferedSubstring();
            return new FStringExpression(_children, _fString);
        }

        private void ParseInnerExpression() {
            Read('{');
            while (HasNextChar() && PeekChar() != '}') {
                _buffer.Append(NextChar());
            }
            if (!HasNextChar()) {
                throw new Exception();
            }

            AddStringToChildren(_buffer.ToString());
            _buffer.Clear();

            Read('}');
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

            var openQuote = "'";
            var x = $"{openQuote}{_buffer.ToString()}{openQuote}";
            AddStringToChildren(x);
            _buffer.Clear();
        }

        private void AddStringToChildren(string x) {
            var parser = Parser.CreateParser(new MemoryStream(Encoding.UTF8.GetBytes(x)), PythonLanguageVersion.V36);
            var expr = Statement.GetExpression(parser.ParseTopExpression().Body);
            if (expr is null) {
                throw new Exception();
            }
            _children.Add(expr);
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
