using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.PythonTools.Analysis.Indexing;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.Python.LanguageServer.Implementation {
    class IndexingFileParser {
        private SymbolIndex _index;
        private PythonLanguageVersion _pythonLanguageVersion;

        public IndexingFileParser(SymbolIndex index, PythonLanguageVersion pythonLanguageVersion) {
            _index = index;
            _pythonLanguageVersion = pythonLanguageVersion;
        }

        public void ParseForIndex(string path) {
            var uri = new Uri(path);
            using (var stream = new StreamReader(uri.AbsolutePath)) {
                var parser = Parser.CreateParser(stream, _pythonLanguageVersion);
                var pythonAst = parser.ParseFile();
                _index.UpdateParseTree(uri, pythonAst);
            }
        }
    }
}
