using System;
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface IIndexManager {
        void AddRootDirectory();
        void ProcessFile(Uri uri, IDocument doc);
        void ProcessClosedFile(Uri uri);
        void ProcessFileIfIndexed(Uri uri, IDocument doc);
    }
}
