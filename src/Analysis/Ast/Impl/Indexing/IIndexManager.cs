using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface IIndexManager : IDisposable {
        Task AddRootDirectory(CancellationToken workspaceCancellationToken = default);
        void ProcessFile(Uri uri, IDocument doc);
        Task ProcessClosedFile(Uri uri, CancellationToken fileCancellationToken = default);
        void ProcessFileIfIndexed(Uri uri, IDocument doc);
    }
}
