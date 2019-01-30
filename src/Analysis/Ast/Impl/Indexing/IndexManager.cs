using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Diagnostics;
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
        private readonly CancellationTokenSource _allIndexCts = new CancellationTokenSource();

        public IndexManager(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version, string rootPath, string[] includeFiles,
            string[] excludeFiles) {
            Check.ArgumentNotNull(nameof(fileSystem), fileSystem);
            Check.ArgumentNotNull(nameof(rootPath), rootPath);
            Check.ArgumentNotNull(nameof(includeFiles), includeFiles);
            Check.ArgumentNotNull(nameof(excludeFiles), excludeFiles);

            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem;
            _indexParser = new IndexParser(symbolIndex, fileSystem, version);
            _workspaceRootPath = rootPath;
            _includeFiles = includeFiles;
            _excludeFiles = excludeFiles;
        }

        public Task AddRootDirectoryAsync(CancellationToken workspaceCancellationToken = default) {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(workspaceCancellationToken, _allIndexCts.Token);
            var parseTasks = new List<Task>();
            foreach (var fileInfo in WorkspaceFiles()) {
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    parseTasks.Add(_indexParser.ParseAsync(fileInfo.FullName, linkedCts.Token));
                }
            }
            return Task.WhenAll(parseTasks.ToArray());
        }

        private IEnumerable<IFileSystemInfo> WorkspaceFiles() {
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
        }

        private bool IsFileIndexed(string path) => _symbolIndex.IsIndexed(path);

        public Task ProcessClosedFileAsync(string path, CancellationToken fileCancellationToken = default) {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(fileCancellationToken, _allIndexCts.Token);
            // If path is on workspace
            if (IsFileOnWorkspace(path)) {
                // updates index and ignores previous AST
                return _indexParser.ParseAsync(path, linkedCts.Token);
            } else {
                // remove file from index
                _symbolIndex.Delete(path);
                return Task.CompletedTask;
            }
        }

        private bool IsFileOnWorkspace(string path) {
            return _fileSystem.IsPathUnderRoot(_workspaceRootPath, path);
        }

        public void ProcessNewFile(string path, IDocument doc) {
            _symbolIndex.UpdateIndex(path, doc.GetAnyAst());
        }

        public void ReIndexFile(string path, IDocument doc) {
            if (IsFileIndexed(path)) {
                ProcessNewFile(path, doc);
            }
        }

        public void Dispose() {
            _allIndexCts.Cancel();
            _allIndexCts.Dispose();
        }
    }
}
