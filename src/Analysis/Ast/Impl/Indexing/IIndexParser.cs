using System;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface IIndexParser {
        void ParseFile(Uri uri);
    }
}
