using Microsoft.PythonTools.Parsing.Ast;
using System;

namespace Microsoft.PythonTools.Analysis.Indexing {
    public interface IParseObserver {
        void UpdateParseTree(Uri uri, PythonAst ast);
    }
}
