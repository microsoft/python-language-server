using System;
using System.IO;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Indexing {
    internal sealed class IndexParser : IIndexParser {
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
                throw new FileNotFoundException($"{uri.AbsolutePath} does not exist", uri.AbsolutePath);
            }
            using (var stream = _fileSystem.FileOpen(uri.AbsolutePath, FileMode.Open)) {
                var parser = Parser.CreateParser(stream, _version);
                _symbolIndex.UpdateIndex(uri, parser.ParseFile());
            }
        }
    }
}
