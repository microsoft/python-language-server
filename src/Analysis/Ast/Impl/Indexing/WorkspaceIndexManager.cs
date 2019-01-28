using System;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Indexing {
    internal class WorkspaceIndexManager : IWorkspaceIndexManager {
        private readonly ISymbolIndex _symbolIndex;
        private readonly IFileSystem _fileSystem;
        private readonly IndexParser _indexParser;
        private readonly string _rootPath;
        private readonly string[] _includeFiles;
        private readonly string[] _excludeFiles;

        public WorkspaceIndexManager(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version, string rootPath, string[] includeFiles,
            string[] excludeFiles) {
            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _indexParser = new IndexParser(symbolIndex, fileSystem, version);
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _includeFiles = includeFiles ?? throw new ArgumentNullException(nameof(rootPath));
            _excludeFiles = excludeFiles ?? throw new ArgumentNullException(nameof(excludeFiles));
        }


        public void AddRootDirectory() {
            var files = _fileSystem.GetDirectoryInfo(_rootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
            foreach (var fileInfo in files) {
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    _indexParser.ParseFile(new Uri(fileInfo.FullName));
                }
            }
        }
    }
}
