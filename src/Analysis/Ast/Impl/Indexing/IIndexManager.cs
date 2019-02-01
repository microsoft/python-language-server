using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface IIndexManager : IDisposable {
        Task AddRootDirectoryAsync(CancellationToken workspaceCancellationToken = default);
        void ProcessNewFile(string path, IDocument doc);
        Task ProcessClosedFileAsync(string path, CancellationToken fileCancellationToken = default);
        void ReIndexFile(string path, IDocument doc);
        Task<IEnumerable<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path);
        Task<IEnumerable<FlatSymbol>> WorkspaceSymbolsAsync(string query);
    }
}
