﻿using System;
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
        private State state = State.WaitingForWork;

        public MostRecentDocumentSymbols(string path, IFileSystem fileSystem, PythonLanguageVersion version, IIndexParser indexParser) {
            _path = path;
            _indexParser = indexParser;
        }

        public void Parse() {
            WorkAndSetTcs(ParseAsync);
        }

        public void Add(IDocument doc) {
            WorkAndSetTcs(ct => IndexAsync(doc, ct));
        }

        public void Index(IDocument doc) {
            WorkAndSetTcs(ct => IndexAsync(doc, ct));
        }

        public void WorkAndSetTcs(Func<CancellationToken, Task<IReadOnlyList<HierarchicalSymbol>>> asyncFunc) {
            CancellationTokenSource currentCts;
            TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                switch (state) {
                    case State.Working:
                        CancelExistingWork();
                        RenewTcs();
                        state = State.Working;
                        break;
                    case State.WaitingForWork:
                        state = State.Working;
                        break;
                    case State.FinishedWork:
                        RenewTcs();
                        state = State.Working;
                        break;
                    default:
                        break;
                }
                currentCts = _fileCts;
                currentTcs = _fileTcs;
            }

            asyncFunc(currentCts.Token).ContinueWith(t => {
                lock (_syncObj) {
                    currentCts.Dispose();
                    if (_fileCts == currentCts) {
                        state = State.FinishedWork;
                    }
                }
                return t.GetAwaiter().GetResult();
            }, currentCts.Token).SetCompletionResultTo(currentTcs);
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
                    case State.WaitingForWork:
                        state = State.WaitingForWork;
                        break;
                    case State.Working:
                        CancelExistingWork();
                        RenewTcs();
                        state = State.WaitingForWork;
                        break;
                    case State.FinishedWork:
                        RenewTcs();
                        state = State.WaitingForWork;
                        break;
                    default:
                        break;
                }
            }
        }

        public void Dispose() {
            lock (_syncObj) {
                switch (state) {
                    case State.Working:
                        CancelExistingWork();
                        state = State.FinishedWork;
                        break;
                    case State.WaitingForWork:
                        state = State.FinishedWork;
                        break;
                    case State.FinishedWork:
                        break;
                }
                _indexParser.Dispose();
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

        private enum State { WaitingForWork, Working, FinishedWork };
    }
}
