// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal sealed class IndexParser : IIndexParser {
        private readonly ISymbolIndex _symbolIndex;
        private readonly IFileSystem _fileSystem;
        private readonly PythonLanguageVersion _version;
        private readonly CancellationTokenSource _allProcessingCts = new CancellationTokenSource();
        private readonly object _syncObj = new object();
        private CancellationTokenSource _linkedParseCts;
        private Task _parseTask;
        private TaskCompletionSource<object> _tcs;

        public IndexParser(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version) {
            Check.ArgumentNotNull(nameof(symbolIndex), symbolIndex);
            Check.ArgumentNotNull(nameof(fileSystem), fileSystem);

            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem;
            _version = version;
        }

        public Task ParseAsync(string path, CancellationToken cancellationToken = default) {
            lock (_syncObj) {
                CancelCurrentParse();
                _tcs = new TaskCompletionSource<object>();
                _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, cancellationToken);
                _parseTask = Task.Run(() => Parse(path, _linkedParseCts));
                return _tcs.Task;
            }
        }

        private void CancelCurrentParse() {
            _linkedParseCts?.Cancel();
            _linkedParseCts?.Dispose();
            _linkedParseCts = null;
            _parseTask = null;
        }

        private void Parse(string path, CancellationTokenSource parseCts) {
            if (parseCts.Token.IsCancellationRequested) {
                _tcs.SetCanceled();
                parseCts.Token.ThrowIfCancellationRequested();
            }
            if (_fileSystem.FileExists(path)) {
                try {
                    if (parseCts.Token.IsCancellationRequested) {
                        _tcs.SetCanceled();
                        parseCts.Token.ThrowIfCancellationRequested();
                    }
                    using (var stream = _fileSystem.FileOpen(path, FileMode.Open)) {
                        var parser = Parser.CreateParser(stream, _version);
                        _symbolIndex.Add(path, parser.ParseFile());
                    }
                } catch (FileNotFoundException e) {
                    Trace.TraceError(e.Message);
                }
            }
            lock (_syncObj) {
                if (_linkedParseCts == parseCts) {
                    _linkedParseCts.Dispose();
                    _linkedParseCts = null;
                    _tcs.SetResult(new object());
                }
            }
        }

        public void Dispose() {
            lock (_syncObj) {
                _tcs?.TrySetCanceled();
                _tcs = null;
                _allProcessingCts.Cancel();
                _allProcessingCts.Dispose();

                _linkedParseCts?.Cancel();
                _linkedParseCts?.Dispose();
                _linkedParseCts = null;

                _parseTask = null;
            }
        }
    }
}
