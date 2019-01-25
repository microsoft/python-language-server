using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Indexing;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Python.LanguageServer.Implementation {
    class RunningDocumentIndexer {
        private SymbolIndex _index;
        private PythonLanguageVersion _pythonLanguageVersion;
        //private WorkspaceManager _workspaceManager;

        public RunningDocumentIndexer(SymbolIndex index, PythonLanguageVersion pythonLanguageVersion) {
            _index = index;
            _pythonLanguageVersion = pythonLanguageVersion;
            //_workspaceManager = workspaceManager;
        }

        public void OpenDocument(Uri uri, IDocument doc) {
            _index.UpdateParseTree(uri, new PythonAst(DocAsts(doc)));
        }

        private IEnumerable<PythonAst> DocAsts(IDocument doc) {
            foreach (int part in doc.DocumentParts) {
                var parser = Parser.CreateParser(doc.ReadDocument(0, out var version), _pythonLanguageVersion);
                yield return parser.ParseFile();
            }
        }

        public void UpdateDocument(Uri uri, IDocument doc) {
            _index.UpdateParseTree(uri, new PythonAst(DocAsts(doc)));
        }

        public void CloseDocument(Uri uri, IDocument doc) {
            //_workspaceManager.ReParseIfOnWorkspace(uri);
        }
    }
}
