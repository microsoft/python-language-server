using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal sealed class MostRecentDocumentSymbols : IMostRecentDocumentSymbols {
        private readonly object _syncObj = new object();
        private readonly IIndexParser _indexParser;
        private readonly string _path;

        private CancellationTokenSource _fileCts = new CancellationTokenSource();

        private TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> _fileTcs = new TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>>();
        private WorkQueueState _state = WorkQueueState.WaitingForWork;

        public MostRecentDocumentSymbols(string path, IIndexParser indexParser) {
            _path = path;
            _indexParser = indexParser;
        }

        public void Parse() => WorkAndSetTcs(ParseAsync).DoNotWait();

        public void Index(IDocument doc) => WorkAndSetTcs(ct => IndexAsync(doc, ct)).DoNotWait();

        public async Task WorkAndSetTcs(Func<CancellationToken, Task<IReadOnlyList<HierarchicalSymbol>>> asyncWork) {
            CancellationTokenSource currentCts;
            TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                switch (_state) {
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
                _state = WorkQueueState.Working;
                currentCts = _fileCts;
                currentTcs = _fileTcs;
            }

            try {
                var result = await asyncWork(currentCts.Token);
                currentTcs.TrySetResult(result);
            } catch (OperationCanceledException) {
                currentTcs.TrySetCanceled();
            } catch (Exception ex) {
                currentTcs.TrySetException(ex);
            } finally {
                lock (_syncObj) {
                    if (!currentCts.Token.IsCancellationRequested) {
                        _state = WorkQueueState.FinishedWork;
                    }
                }
            }
        }

        public Task<IReadOnlyList<HierarchicalSymbol>> GetSymbolsAsync(CancellationToken ct = default) {
            lock (_syncObj) {
                return _fileTcs.Task;
            }
        }

        public void MarkAsPending() {
            lock (_syncObj) {
                switch (_state) {
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
                _state = WorkQueueState.WaitingForWork;
            }
        }

        public void Dispose() {
            lock (_syncObj) {
                switch (_state) {
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
                _state = WorkQueueState.FinishedWork;
            }
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> IndexAsync(IDocument doc, CancellationToken indexCt) {
            PythonAst ast = null;

            for (var i = 0; i < 5; i++) {
                ast = await doc.GetAstAsync(indexCt);
                if (ast != null) {
                    break;
                }
                await Task.Delay(100);
            }

            if (ast == null) {
                return Array.Empty<HierarchicalSymbol>();
            }

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
            _fileCts.Dispose();
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
