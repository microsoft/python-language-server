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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal sealed class IndexParser : IIndexParser {
        private const int MaxConcurrentParsings = 10;
        private readonly IFileSystem _fileSystem;
        private readonly PythonLanguageVersion _version;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _allProcessingCts = new CancellationTokenSource();
        private readonly object _syncObj = new object();

        public IndexParser(IFileSystem fileSystem, PythonLanguageVersion version) {
            Check.ArgumentNotNull(nameof(fileSystem), fileSystem);

            _fileSystem = fileSystem;
            _version = version;
            _semaphore = new SemaphoreSlim(MaxConcurrentParsings);
        }

        public Task<PythonAst> ParseAsync(string path, CancellationToken cancellationToken = default) {
            var linkedParseCts =
                CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, cancellationToken);
            Task<PythonAst> parseTask;
            parseTask = Task.Run(async () => await Parse(path, linkedParseCts));
            parseTask.ContinueWith(_ => linkedParseCts.Dispose());
            return parseTask;
        }

        private async Task<PythonAst> Parse(string path, CancellationTokenSource parseCts) {
            await _semaphore.WaitAsync();
            PythonAst ast;
            try {
                parseCts.Token.ThrowIfCancellationRequested();
                using (var stream = _fileSystem.FileOpen(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var parser = Parser.CreateParser(stream, _version);
                    ast = parser.ParseFile();
                }
            } finally {
                _semaphore.Release();
            }

            return ast;
        }

        public void Dispose() {
            lock (_syncObj) {
                if (!_allProcessingCts.IsCancellationRequested) {
                    _allProcessingCts.Cancel();
                    _allProcessingCts.Dispose();
                }
            }
            _semaphore.Dispose();
        }
    }
}
