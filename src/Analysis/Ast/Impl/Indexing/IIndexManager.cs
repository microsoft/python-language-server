using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface IIndexManager : IDisposable {
        Task AddRootDirectoryAsync(CancellationToken workspaceCancellationToken = default);
        void ProcessNewFile(Uri uri, IDocument doc);
        Task ProcessClosedFileAsync(Uri uri, CancellationToken fileCancellationToken = default);
        void ReIndexFile(Uri uri, IDocument doc);
    }
}
