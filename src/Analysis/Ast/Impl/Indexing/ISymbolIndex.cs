using System;
using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface ISymbolIndex {
        void UpdateIndex(string path, PythonAst pythonAst);
        IEnumerable<FlatSymbol> WorkspaceSymbols(string query);
        void Delete(string path);
        bool IsIndexed(string path);
    }
}
