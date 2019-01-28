using System;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Indexing {
    internal class WorkspaceIndexManager : IWorkspaceIndexManager {
        private readonly ISymbolIndex _symbolIndex;
        private readonly IDirectoryFileReader _rootFileReader;
        private readonly IFileSystem _fileSystem;
        private readonly IndexParser _indexParser;

        public WorkspaceIndexManager(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version, IDirectoryFileReader rootFileReader) {
            _rootFileReader = rootFileReader ?? throw new ArgumentNullException($"rootFileReader is null", nameof(rootFileReader));
            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem;
            _indexParser = new IndexParser(symbolIndex, fileSystem, version);
        }


        public void AddRootDirectory() {
            foreach (var path in _rootFileReader.DirectoryFilePaths()) {
                if (ModulePath.IsPythonSourceFile(path)) {
                    _indexParser.ParseFile(new Uri(path));
                }
            }
        }
    }
}
