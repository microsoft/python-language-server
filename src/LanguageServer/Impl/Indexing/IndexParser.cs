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

        public IndexParser(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version) {
            Check.ArgumentNotNull(nameof(symbolIndex), symbolIndex);
            Check.ArgumentNotNull(nameof(fileSystem), fileSystem);

            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem;
            _version = version;
        }

        public void Dispose() {
            _allProcessingCts.Cancel();
            _allProcessingCts.Dispose();
        }

        public Task<bool> ParseAsync(string path, CancellationToken parseCancellationToken = default) {
            var linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, parseCancellationToken);
            var linkedParseToken = linkedParseCts.Token;
            return Task<bool>.Run(() => {
                if (!_fileSystem.FileExists(path)) {
                    return false;
                }
                try {
                    linkedParseToken.ThrowIfCancellationRequested();
                    using (var stream = _fileSystem.FileOpen(path, FileMode.Open)) {
                        var parser = Parser.CreateParser(stream, _version);
                        linkedParseToken.ThrowIfCancellationRequested();
                        _symbolIndex.UpdateIndex(path, parser.ParseFile());
                        return true;
                    }
                } catch (FileNotFoundException e) {
                    Trace.TraceError(e.Message);
                    return false;
                }
            }).ContinueWith((task) => {
                linkedParseCts.Dispose();
                return task.Result;
            }, linkedParseToken);
        }
    }
}
