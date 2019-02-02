using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly TaskCompletionSource<bool> _addRootTcs;

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
            _addRootTcs = new TaskCompletionSource<bool>();
            StartAddRootDir();
        }

        private void StartAddRootDir() {
            Task.Run(() => {
                try {
                    var parseTasks = new List<Task<bool>>();
                    foreach (var fileInfo in WorkspaceFiles()) {
                        if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                            parseTasks.Add(_indexParser.ParseAsync(fileInfo.FullName, _allIndexCts.Token));
                        }
                    }
                    Task.WaitAll(parseTasks.ToArray(), _allIndexCts.Token);
                    _addRootTcs.SetResult(true);
                } catch (Exception e) {
                    _addRootTcs.SetException(e);
                }
            });
        }

        public Task AddRootDirectoryAsync() {
            return _addRootTcs.Task;
        }


        private IEnumerable<IFileSystemInfo> WorkspaceFiles() {
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
        }

        private bool IsFileIndexed(string path) => _symbolIndex.IsIndexed(path);

        public Task ProcessClosedFileAsync(string path, CancellationToken fileCancellationToken = default) {
            // If path is on workspace
            if (IsFileOnWorkspace(path)) {
                // updates index and ignores previous AST
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(fileCancellationToken, _allIndexCts.Token);
                return _indexParser.ParseAsync(path, linkedCts.Token)
                    .ContinueWith(_ => linkedCts.Dispose());
            } else {
                // remove file from index
                _symbolIndex.Delete(path);
                return Task.CompletedTask;
            }
        }

        private bool IsFileOnWorkspace(string path)
            => _fileSystem.IsPathUnderRoot(_workspaceRootPath, path);

        public void ProcessNewFile(string path, IDocument doc)
            => _symbolIndex.UpdateIndex(path, doc.GetAnyAst());

        public void ReIndexFile(string path, IDocument doc) {
            if (IsFileIndexed(path)) {
                ProcessNewFile(path, doc);
            }
        }

        public void Dispose() {
            _allIndexCts.Cancel();
            _allIndexCts.Dispose();
        }

        public async Task<List<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path) {
            await AddRootDirectoryAsync();
            return _symbolIndex.HierarchicalDocumentSymbols(path).ToList();
        }

        public async Task<List<FlatSymbol>> WorkspaceSymbolsAsync(string query) {
            await AddRootDirectoryAsync();
            return _symbolIndex.WorkspaceSymbols(query).ToList();
        }
    }
}
