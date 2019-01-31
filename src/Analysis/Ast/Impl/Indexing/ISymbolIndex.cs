using System;
using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface ISymbolIndex {
        IEnumerable<FlatSymbol> WorkspaceSymbols(string query);
        IEnumerable<HierarchicalSymbol> HierarchicalDocumentSymbols(string path);
        void UpdateIndex(string path, PythonAst pythonAst);
        void Delete(string path);
        bool IsIndexed(string path);
    }
}
