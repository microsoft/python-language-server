using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Indexing {
    class MostRecentDocumentSymbols : IDisposable {
        private readonly object _syncObj = new object();
        private readonly IIndexParser _indexParser;
        private readonly ISymbolIndex _symbolIndex;
        private readonly string _path;

        private CancellationTokenSource _fileCts;
        private Task _fileTask;
        private TaskCompletionSource<IEnumerable<HierarchicalSymbol>> _fileTcs;

        public MostRecentDocumentSymbols(string path, IIndexParser indexParser, ISymbolIndex symbolIndex) {
            _path = path;
            _indexParser = indexParser;
            _symbolIndex = symbolIndex;
            _fileCts = new CancellationTokenSource();
            _fileTcs = new TaskCompletionSource<IEnumerable<HierarchicalSymbol>>();
        }

        public void Parse() {
            lock (_syncObj) {
                CancelExistingTask();
                _fileTask = ParseAsync();
            }
        }

        private async Task ParseAsync() {
            var ast = await _indexParser.ParseAsync(_path, _fileCts.Token);
            _fileCts.Token.ThrowIfCancellationRequested();
            lock (_syncObj) {
                SetFileTcsResult();
            }

        }

        public void Delete() {
            lock (_syncObj) {
                _symbolIndex.Delete(_path);
            }
        }

        public void Process(PythonAst ast) {
            lock (_syncObj) {
                CancelExistingTask();
                _symbolIndex.Add(_path, ast);
                SetFileTcsResult();
            }
        }

        public void Add(IDocument doc) {
            lock (_syncObj) {
                CancelExistingTask();
                _fileTask = AddAsync(doc);
            }
        }

        private async Task AddAsync(IDocument doc) {
            var ast = await doc.GetAstAsync(_fileCts.Token);
            _fileCts.Token.ThrowIfCancellationRequested();
            lock (_syncObj) {
                _symbolIndex.Add(_path, ast);
                SetFileTcsResult();
            }
        }

        public Task<IEnumerable<HierarchicalSymbol>> GetSymbolsAsync() => _fileTcs.Task;

        private void CancelExistingTask() {
            if (_fileTask != null) {
                if (_fileTcs.Task.IsCompleted) {
                    _fileTcs.TrySetCanceled();
                    _fileTcs = new TaskCompletionSource<IEnumerable<HierarchicalSymbol>>();
                }

                _fileCts.Cancel();
                _fileCts.Dispose();
                _fileCts = new CancellationTokenSource();

                _fileTask = null;
            }
        }

        public void ReIndex(IDocument doc) {
            lock (_syncObj) {
                CancelExistingTask();
                _fileTask = ReIndexAsync(doc);
            }
        }

        private async Task ReIndexAsync(IDocument doc) {
            var ast = await doc.GetAstAsync(_fileCts.Token);
            _fileCts.Token.ThrowIfCancellationRequested();
            lock (_syncObj) {
                _symbolIndex.Update(_path, ast);
                SetFileTcsResult();
            }
        }

        private void SetFileTcsResult() {
            _fileTcs.TrySetResult(_symbolIndex.HierarchicalDocumentSymbols(_path));
        }

        public void Dispose() {
            lock (_syncObj) {
                _fileCts?.Cancel();
                _fileCts?.Dispose();
                _fileCts = null;
                _fileTask = null;
            }
        }
    }
}

