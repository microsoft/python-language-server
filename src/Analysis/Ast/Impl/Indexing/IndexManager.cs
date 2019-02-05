// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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

        public const int DefaultWorkspaceSymbolsLimit = 50;

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

        public async Task<IReadOnlyList<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path) {
            await AddRootDirectoryAsync();
            return _symbolIndex.HierarchicalDocumentSymbols(path).ToList();
        }

        public async Task<IReadOnlyList<FlatSymbol>> WorkspaceSymbolsAsync(string query, int maxLength = DefaultWorkspaceSymbolsLimit) {
            await AddRootDirectoryAsync();
            return _symbolIndex.WorkspaceSymbols(query).Take(maxLength).ToList();
        }
    }
}
