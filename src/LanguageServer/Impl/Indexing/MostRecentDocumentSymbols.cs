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

        private TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> _fileTcs =
            new TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>>();

        public MostRecentDocumentSymbols(string path, IFileSystem fileSystem, PythonLanguageVersion version, IIndexParser indexParser) {
            _path = path;
            _indexParser = indexParser;
        }

        public void Parse() {
            CancellationToken currentCt;
            TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                CancelExistingTask();
                currentCt = _fileCts.Token;
                currentTcs = _fileTcs;
            }

            ParseAsync(currentCt).SetCompletionResultTo(currentTcs);
        }

        public void Add(IDocument doc) {
            CancellationToken currentCt;
            TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                CancelExistingTask();
                currentCt = _fileCts.Token;
                currentTcs = _fileTcs;
            }

            AddAsync(doc, currentCt).SetCompletionResultTo(currentTcs);
        }

        public void ReIndex(IDocument doc) {
            CancellationToken currentCt;
            TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> currentTcs;
            lock (_syncObj) {
                CancelExistingTask();
                currentCt = _fileCts.Token;
                currentTcs = _fileTcs;
            }

            ReIndexAsync(doc, currentCt).SetCompletionResultTo(currentTcs);
        }

        public Task<IReadOnlyList<HierarchicalSymbol>> GetSymbolsAsync(CancellationToken ct = default)
            => _fileTcs.Task.ContinueWith(t => t.GetAwaiter().GetResult(), ct);

        public void MarkAsPending() {
            lock (_syncObj) {
                CancelExistingTask();
            }
        }

        public void Dispose() {
            lock (_syncObj) {
                if (_fileCts != null) {
                    _fileCts?.Cancel();
                    _fileCts?.Dispose();
                    _fileCts = null;

                    _fileTcs.TrySetCanceled();
                }

                _indexParser.Dispose();
            }
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> AddAsync(IDocument doc,
            CancellationToken addCancellationToken) {
            var ast = await doc.GetAstAsync(addCancellationToken);
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            addCancellationToken.ThrowIfCancellationRequested();
            return walker.Symbols;
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> ReIndexAsync(IDocument doc,
            CancellationToken reIndexCancellationToken) {
            var ast = await doc.GetAstAsync(reIndexCancellationToken);
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            reIndexCancellationToken.ThrowIfCancellationRequested();
            return walker.Symbols;
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> ParseAsync(CancellationToken parseCancellationToken) {
            try {
                var ast = await _indexParser.ParseAsync(_path, parseCancellationToken);
                var walker = new SymbolIndexWalker(ast);
                ast.Walk(walker);
                parseCancellationToken.ThrowIfCancellationRequested();
                return walker.Symbols;
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                Trace.TraceError(e.Message);
            }

            return new List<HierarchicalSymbol>();
        }

        private void CancelExistingTask() {
            Check.InvalidOperation(Monitor.IsEntered(_syncObj));

            _fileCts.Cancel();
            _fileCts.Dispose();
            _fileCts = new CancellationTokenSource();

            _fileTcs = new TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>>();
        }
    }
}
