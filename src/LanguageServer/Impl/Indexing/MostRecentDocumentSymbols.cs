using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    class MostRecentDocumentSymbols : IMostRecentDocumentSymbols {
        private readonly object _syncObj = new object();
        private readonly IIndexParser _indexParser;
        private readonly string _path;

        private CancellationTokenSource _fileCts = new CancellationTokenSource();

        private TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> _fileTcs = new TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>>();
        private WorkQueueState state = WorkQueueState.WaitingForWork;

        public MostRecentDocumentSymbols(string path, IFileSystem fileSystem, PythonLanguageVersion version, IIndexParser indexParser) {
            _path = path;
            _indexParser = indexParser;
        }

        public void Parse() {
            WorkAndSetTcs(ParseAsync);
        }

        public void Index(IDocument doc) {
            WorkAndSetTcs(ct => IndexAsync(doc, ct));
        }

        public void WorkAndSetTcs(Func<CancellationToken, Task<IReadOnlyList<HierarchicalSymbol>>> asyncWork) {
            CancellationTokenSource currentCts;
            TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                switch (state) {
                    case WorkQueueState.Working:
                        CancelExistingWork();
                        RenewTcs();
                        break;
                    case WorkQueueState.WaitingForWork:
                        break;
                    case WorkQueueState.FinishedWork:
                        RenewTcs();
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                state = WorkQueueState.Working;
                currentCts = _fileCts;
                currentTcs = _fileTcs;
            }

            DoWork(currentCts, asyncWork).SetCompletionResultTo(currentTcs);
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> DoWork(CancellationTokenSource tcs, Func<CancellationToken, Task<IReadOnlyList<HierarchicalSymbol>>> asyncWork) {
            var token = tcs.Token;

            try {
                return await asyncWork(token);
            } finally {
                lock (_syncObj) {
                    tcs.Dispose();
                    if (!token.IsCancellationRequested) {
                        state = WorkQueueState.FinishedWork;
                    }
                }
            }
        }

        public Task<IReadOnlyList<HierarchicalSymbol>> GetSymbolsAsync(CancellationToken ct = default) {
            TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                currentTcs = _fileTcs;
            }
            return currentTcs.Task.ContinueWith(t => t.GetAwaiter().GetResult(), ct);
        }

        public void MarkAsPending() {
            lock (_syncObj) {
                switch (state) {
                    case WorkQueueState.WaitingForWork:
                        break;
                    case WorkQueueState.Working:
                        CancelExistingWork();
                        RenewTcs();
                        break;
                    case WorkQueueState.FinishedWork:
                        RenewTcs();
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                state = WorkQueueState.WaitingForWork;
            }
        }

        public void Dispose() {
            lock (_syncObj) {
                switch (state) {
                    case WorkQueueState.Working:
                        CancelExistingWork();
                        break;
                    case WorkQueueState.WaitingForWork:
                        CancelExistingWork();
                        // Manually cancel tcs, in case any task is awaiting
                        _fileTcs.TrySetCanceled();
                        break;
                    case WorkQueueState.FinishedWork:
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                state = WorkQueueState.FinishedWork;
            }
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> IndexAsync(IDocument doc,
            CancellationToken indexCt) {
            var ast = await doc.GetAstAsync(indexCt);
            indexCt.ThrowIfCancellationRequested();
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            return walker.Symbols;
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> ParseAsync(CancellationToken parseCancellationToken) {
            try {
                var ast = await _indexParser.ParseAsync(_path, parseCancellationToken);
                parseCancellationToken.ThrowIfCancellationRequested();
                var walker = new SymbolIndexWalker(ast);
                ast.Walk(walker);
                return walker.Symbols;
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                Trace.TraceError(e.Message);
            }

            return new List<HierarchicalSymbol>();
        }

        private void RenewTcs() {
            Check.InvalidOperation(Monitor.IsEntered(_syncObj));
            _fileCts = new CancellationTokenSource();
            _fileTcs = new TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>>();
        }

        private void CancelExistingWork() {
            Check.InvalidOperation(Monitor.IsEntered(_syncObj));
            _fileCts.Cancel();
        }

        /* It's easier to think of it as a queue of work
         * but it maintains only one item at a time in the queue */
        private enum WorkQueueState { WaitingForWork, Working, FinishedWork };
    }
}
