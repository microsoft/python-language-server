using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringParser {
        private readonly string _fString;
        private int _position;
        private StringBuilder _buffer;
        private List<PythonAst> _children;

        public FStringParser(FormattedString fString) {
            _fString = fString.Value;
            _position = 0;
            _buffer = new StringBuilder();
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
        }

        private void ParseInnerExpression() {
            Read('{');
            while (HasNextChar() && PeekChar() != '}') {
                _buffer.Append(NextChar());
            }

            if (!HasNextChar()) {
                throw new Exception();
            }

            var subExpressionString = _buffer.ToString();
            _buffer.Clear();
            var parser = Parser.CreateParser(new MemoryStream(Encoding.UTF8.GetBytes(subExpressionString)),
                PythonLanguageVersion.V36);
            _children.Add(parser.ParseTopExpression());
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

            var x = $"'{_buffer.ToString()}'";
            var parser = Parser.CreateParser(new MemoryStream(Encoding.UTF8.GetBytes(x)), PythonLanguageVersion.V36);
            _children.Add(parser.ParseTopExpression());
            _buffer.Clear();
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
