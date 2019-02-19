using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    class MostRecentDocumentSymbols : IDisposable {
        private readonly object _syncObj = new object();
        private readonly IIndexParser _indexParser;
        private readonly ISymbolIndex _symbolIndex;
        private readonly string _path;

        private CancellationTokenSource _fileCts = new CancellationTokenSource();
        private TaskCompletionSource<IEnumerable<HierarchicalSymbol>> _fileTcs = new TaskCompletionSource<IEnumerable<HierarchicalSymbol>>();
        private bool wasLastTaskDisposed = true;

        public MostRecentDocumentSymbols(string path, ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version) {
            _path = path;
            _symbolIndex = symbolIndex;
            _indexParser = new IndexParser(_symbolIndex, fileSystem, version);
        }

        public void Parse() {
            CancellationToken currentCt;
            TaskCompletionSource<IEnumerable<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                CancelExistingTask();
                currentCt = _fileCts.Token;
                currentTcs = _fileTcs;
                wasLastTaskDisposed = false;
            }
            ParseAsync(currentCt).SetCompletionResultTo(currentTcs);
        }

        public void Add(IDocument doc) {
            CancellationToken currentCt;
            TaskCompletionSource<IEnumerable<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                CancelExistingTask();
                currentCt = _fileCts.Token;
                currentTcs = _fileTcs;
                wasLastTaskDisposed = false;
            }
            AddAsync(doc, currentCt).SetCompletionResultTo(currentTcs);
        }

        public void ReIndex(IDocument doc) {
            CancellationToken currentCt;
            TaskCompletionSource<IEnumerable<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                CancelExistingTask();
                currentCt = _fileCts.Token;
                currentTcs = _fileTcs;
                wasLastTaskDisposed = false;
            }
            ReIndexAsync(doc, currentCt).SetCompletionResultTo(currentTcs);
        }

        public Task<IEnumerable<HierarchicalSymbol>> GetSymbolsAsync() => _fileTcs.Task;

        public void Delete() {
            lock (_syncObj) {
                CancelExistingTask();
                _symbolIndex.Delete(_path);
            }
        }

        public void MarkAsPending() {
            lock (_syncObj) {
                CancelExistingTask();
            }
        }

        public void Dispose() {
            lock (_syncObj) {
                if (!wasLastTaskDisposed) {
                    _fileCts?.Cancel();
                    _fileCts?.Dispose();
                    _fileCts = null;

                    wasLastTaskDisposed = true;
                    _fileTcs.TrySetCanceled();
                }
                _indexParser.Dispose();
            }
        }

        private async Task<IEnumerable<HierarchicalSymbol>> AddAsync(IDocument doc, CancellationToken addCancellationToken) {
            var ast = await doc.GetAstAsync(addCancellationToken);
            lock (_syncObj) {
                addCancellationToken.ThrowIfCancellationRequested();
                _symbolIndex.Add(_path, ast);
                return _symbolIndex.HierarchicalDocumentSymbols(_path);
            }
        }

        private async Task<IEnumerable<HierarchicalSymbol>> ReIndexAsync(IDocument doc, CancellationToken reIndexCancellationToken) {
            var ast = await doc.GetAstAsync(reIndexCancellationToken);
            lock (_syncObj) {
                reIndexCancellationToken.ThrowIfCancellationRequested();
                _symbolIndex.Update(_path, ast);
                return _symbolIndex.HierarchicalDocumentSymbols(_path);
            }
        }

        private async Task<IEnumerable<HierarchicalSymbol>> ParseAsync(CancellationToken parseCancellationToken) {
            await _indexParser.ParseAsync(_path, parseCancellationToken);
            parseCancellationToken.ThrowIfCancellationRequested();
            lock (_syncObj) {
                return _symbolIndex.HierarchicalDocumentSymbols(_path);
            }
        }

        private void CancelExistingTask() {
            Check.InvalidOperation(Monitor.IsEntered(_syncObj));

            if (!wasLastTaskDisposed) {
                _fileCts.Cancel();
                _fileCts.Dispose();
                _fileCts = new CancellationTokenSource();
                // Re use current tcs if possible
                if (_fileTcs.Task.IsCompleted) {
                    _fileTcs = new TaskCompletionSource<IEnumerable<HierarchicalSymbol>>();
                }
                wasLastTaskDisposed = true;
            }
        }
    }
}

