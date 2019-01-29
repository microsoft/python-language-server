using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Analysis.Indexing {
    internal interface IIndexParser : IDisposable {
        Task ParseAsync(Uri uri, CancellationToken cancellationToken = default);
    }
}
