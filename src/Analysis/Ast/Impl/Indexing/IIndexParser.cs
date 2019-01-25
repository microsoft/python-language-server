using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface IIndexParser {
        void ParseFile(Uri uri);
    }
}
