﻿using System;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Indexing {
    internal class IndexManager : IIndexManager {
        private readonly ISymbolIndex _symbolIndex;
        private readonly IFileSystem _fileSystem;
        private readonly IndexParser _indexParser;
        private readonly string _workspaceRootPath;
        private readonly string[] _includeFiles;
        private readonly string[] _excludeFiles;

        public IndexManager(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version, string rootPath, string[] includeFiles,
            string[] excludeFiles) {
            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _indexParser = new IndexParser(symbolIndex, fileSystem, version);
            _workspaceRootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _includeFiles = includeFiles ?? throw new ArgumentNullException(nameof(rootPath));
            _excludeFiles = excludeFiles ?? throw new ArgumentNullException(nameof(excludeFiles));
        }


        public void AddRootDirectory() {
            var files = _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
            foreach (var fileInfo in files) {
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    _indexParser.ParseFile(new Uri(fileInfo.FullName));
                }
            }
        }

        public void ProcessClosedFile(Uri uri) {
            // If path is on workspace
            if (_fileSystem.IsPathUnderRoot(_workspaceRootPath, uri.AbsolutePath)) {
                // updates index and ignores previous AST
                _indexParser.ParseFile(uri);
            } else {
                // remove file from index
                _symbolIndex.Delete(uri);
            }
        }

        public void ProcessFile(Uri uri, IDocument doc) {
            _symbolIndex.UpdateIndex(uri, doc.GetAnyAst());
        }
    }
}
