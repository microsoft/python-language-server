using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly CancellationTokenSource _allIndexCts = new CancellationTokenSource();

        public IndexManager(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version, string rootPath, string[] includeFiles,
            string[] excludeFiles) {
            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _indexParser = new IndexParser(symbolIndex, fileSystem, version);
            _workspaceRootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _includeFiles = includeFiles ?? throw new ArgumentNullException(nameof(rootPath));
            _excludeFiles = excludeFiles ?? throw new ArgumentNullException(nameof(excludeFiles));
        }

        public Task AddRootDirectory(CancellationToken workspaceCancellationToken = default) {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(workspaceCancellationToken, _allIndexCts.Token);
            var parseTasks = new List<Task>();
            foreach (var fileInfo in WorkspaceFiles()) {
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    Uri uri = new Uri(fileInfo.FullName);
                    parseTasks.Add(_indexParser.ParseAsync(uri, linkedCts.Token).ContinueWith((task) => {
                        linkedCts.Token.ThrowIfCancellationRequested();
                        _indexedFiles[uri] = true;
                    }));
                }
            }
            return Task.WhenAll(parseTasks.ToArray());
        }

        private IEnumerable<IFileSystemInfo> WorkspaceFiles() {
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
        }

        private bool IsFileIndexed(Uri uri) {
            _indexedFiles.TryGetValue(uri, out var val);
            return val;
        }

        public Task ProcessClosedFile(Uri uri, CancellationToken fileCancellationToken = default) {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(fileCancellationToken, _allIndexCts.Token);
            // If path is on workspace
            if (IsFileOnWorkspace(uri)) {
                // updates index and ignores previous AST
                return _indexParser.ParseAsync(uri, linkedCts.Token);
            } else {
                // remove file from index
                _indexedFiles.TryRemove(uri, out _);
                _symbolIndex.Delete(uri);
                return Task.CompletedTask;
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

        public void Dispose() {
            _allIndexCts.Cancel();
            _allIndexCts.Dispose();
        }
    }
}
