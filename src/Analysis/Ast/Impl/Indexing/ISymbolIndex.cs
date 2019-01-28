using System;
using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface ISymbolIndex {
        void UpdateIndex(Uri uri, PythonAst pythonAst);
        bool isNotEmpty();
        IEnumerable<FlatSymbol> WorkspaceSymbols(string query);
    }
}
