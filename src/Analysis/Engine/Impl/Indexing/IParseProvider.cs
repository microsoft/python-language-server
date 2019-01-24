using Microsoft.PythonTools.Parsing.Ast;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Analysis.Indexing {
    interface IParseProvider {
        void SubscribeAst(Uri uri, IParseObserver observer);
        void RefreshAst(Uri uri);
        void RefreshAstDoc(Uri uri, IDocument doc);
    }
}
