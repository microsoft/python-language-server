using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Python.Analysis.Indexing {
    internal sealed class IndexParser: IIndexParser {
        private readonly ISymbolIndex _symbolIndex;
        private readonly IFileSystem _fileSystem;
        private readonly PythonLanguageVersion _version;

        public IndexParser(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version) {
            _symbolIndex = symbolIndex ?? throw new ArgumentNullException(nameof(symbolIndex));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _version = version;
        }

        public void ParseFile(Uri uri) {
            if (!_fileSystem.FileExists(uri.AbsolutePath)) {
                throw new ArgumentException($"{uri.AbsolutePath} does not exist", nameof(uri));
            }
            using (var stream = _fileSystem.FileOpen(uri.AbsolutePath, FileMode.Open)) {
                var parser = Parser.CreateParser(stream, _version);
                _symbolIndex.UpdateIndex(uri, parser.ParseFile());
            }
        }
    }
}
