﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Indexing {
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

        public Task ParseAsync(string path, CancellationToken parseCancellationToken = default) {
            var linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, parseCancellationToken);
            var linkedParseToken = linkedParseCts.Token;
            return Task.Run(() => {
                if (!_fileSystem.FileExists(path)) {
                    throw new FileNotFoundException($"{path} does not exist", path);
                }
                using (var stream = _fileSystem.FileOpen(path, FileMode.Open)) {
                    var parser = Parser.CreateParser(stream, _version);
                    linkedParseToken.ThrowIfCancellationRequested();
                    _symbolIndex.UpdateIndex(path, parser.ParseFile());
                }
            }, linkedParseToken);
        }
    }
}
