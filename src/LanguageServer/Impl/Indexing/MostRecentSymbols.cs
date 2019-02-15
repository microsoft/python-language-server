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
                SetFileTask(_indexParser.ParseAsync(_path, _fileCts.Token));
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
                _symbolIndex.Update(_path, ast);
                SetFileTcsResult();
            }
        }

        public void Process(IDocument doc) {
            lock (_syncObj) {
                CancelExistingTask();
                SetFileTask(doc.GetAstAsync(_fileCts.Token).ContinueWith(t => {
                    lock (_syncObj) {
                        _symbolIndex.Update(_path, t.Result);
                    }
                }, _fileCts.Token));
            }
        }

        public Task<IEnumerable<HierarchicalSymbol>> GetSymbolsAsync() => _fileTcs.Task;

        private void CancelExistingTask() {
            if (_fileTask != null) {
                if (_fileTcs.Task.IsCompleted) {
                    _fileCts.Cancel();
                
                    _fileTcs.TrySetCanceled();
                    _fileTcs = new TaskCompletionSource<IEnumerable<HierarchicalSymbol>>();
                }

                _fileCts.Dispose();
                _fileCts = new CancellationTokenSource();

                _fileTask = null;
            }
        }

        private void SetFileTcsResult() {
            _fileTcs.TrySetResult(_symbolIndex.HierarchicalDocumentSymbols(_path));
        }

        private void SetFileTask(Task task) {
            _fileTask = task;
            _fileTask.ContinueWith(t => {
                if (t.Status.Equals(TaskStatus.RanToCompletion)) {
                    lock (_syncObj) {
                        SetFileTcsResult();
                    }
                }
            });
        }

        public void Dispose() {
            lock (_syncObj) {
                _fileCts.Dispose();
                _fileTask = null;
            }
        }
    }
}
