using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<Uri, bool> _indexedFiles = new ConcurrentDictionary<Uri, bool>();

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
            var files = WorkspaceFiles();
            foreach (var fileInfo in files) {
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    Uri uri = new Uri(fileInfo.FullName);
                    _indexParser.ParseAsync(uri);
                    _indexedFiles[uri] = true;
                }
            }
        }

        private IEnumerable<IFileSystemInfo> WorkspaceFiles() {
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
        }

        private bool IsFileIndexed(Uri uri) {
            _indexedFiles.TryGetValue(uri, out var val);
            return val;
        }

        public void ProcessClosedFile(Uri uri) {
            // If path is on workspace
            if (IsFileOnWorkspace(uri)) {
                // updates index and ignores previous AST
                _indexParser.ParseAsync(uri);
            } else {
                // remove file from index
                _indexedFiles.TryRemove(uri, out _);
                _symbolIndex.Delete(uri);
            }
        }

        private bool IsFileOnWorkspace(Uri uri) {
            return _fileSystem.IsPathUnderRoot(_workspaceRootPath, uri.AbsolutePath);
        }

        public void ProcessFile(Uri uri, IDocument doc) {
            _indexedFiles[uri] = true;
            _symbolIndex.UpdateIndex(uri, doc.GetAnyAst());
        }

        public void ProcessFileIfIndexed(Uri uri, IDocument doc) {
            if (IsFileIndexed(uri)) {
                ProcessFile(uri, doc);
            }
        }
    }
}
