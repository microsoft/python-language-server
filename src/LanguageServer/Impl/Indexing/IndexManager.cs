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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal class IndexManager : IIndexManager {
        private readonly ISymbolIndex _symbolIndex;
        private readonly IFileSystem _fileSystem;
        private readonly IndexParser _indexParser;
        private readonly string _workspaceRootPath;
        private readonly string[] _includeFiles;
        private readonly string[] _excludeFiles;
        private readonly CancellationTokenSource _allIndexCts = new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> _addRootTcs;
        private readonly IIdleTimeService _idleTimeService;
        private HashSet<IDocument> _pendingDocs;

        public IndexManager(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version, string rootPath, string[] includeFiles,
            string[] excludeFiles, IIdleTimeService idleTimeService) {
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
            _idleTimeService = idleTimeService;
            _addRootTcs = new TaskCompletionSource<bool>();
            _idleTimeService.Idle += ReIndexPendingDocsAsync;
            _pendingDocs = new HashSet<IDocument>(new UriDocumentComparer());
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
                    Trace.TraceError(e.Message);
                    _addRootTcs.SetException(e);
                }
            }, _allIndexCts.Token);
        }

        public Task AddRootDirectoryAsync(CancellationToken cancellationToken = default) {
            // Add cancellation token around task
            return Task.Run(async () => await _addRootTcs.Task, cancellationToken);
        }


        private IEnumerable<IFileSystemInfo> WorkspaceFiles() {
            if (string.IsNullOrEmpty(_workspaceRootPath)) {
                return Enumerable.Empty<IFileSystemInfo>();
            }
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
        }

        private bool IsFileIndexed(string path) => _symbolIndex.IsIndexed(path);

        public Task ProcessClosedFileAsync(string path) {
            // If path is on workspace
            if (IsFileOnWorkspace(path)) {
                // updates index and ignores previous AST
                return _indexParser.ParseAsync(path, _allIndexCts.Token);
            } else {
                // remove file from index
                _symbolIndex.DeleteIfNewer(path, _symbolIndex.GetNewVersion(path));
                return Task.CompletedTask;
            }
        }

        private bool IsFileOnWorkspace(string path) {
            if (string.IsNullOrEmpty(_workspaceRootPath)) {
                return false;
            }
            return _fileSystem.IsPathUnderRoot(_workspaceRootPath, path);
        }

        public async Task ProcessNewFileAsync(string path, IDocument doc) {
            var ast = await doc.GetAstAsync();
            _symbolIndex.UpdateIndexIfNewer(path, ast, _symbolIndex.GetNewVersion(path));
        }

        public async Task ReIndexFileAsync(string path, IDocument doc) {
            if (IsFileIndexed(path)) {
                await ProcessNewFileAsync(path, doc);
            }
        }

        public void Dispose() {
            _allIndexCts.Cancel();
            _allIndexCts.Dispose();
        }

        public async Task<IReadOnlyList<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path, CancellationToken cancellationToken = default) {
            await AddRootDirectoryAsync(cancellationToken);
            return _symbolIndex.HierarchicalDocumentSymbols(path).ToList();
        }

        public async Task<IReadOnlyList<FlatSymbol>> WorkspaceSymbolsAsync(string query, int maxLength, CancellationToken cancellationToken = default) {
            await AddRootDirectoryAsync(cancellationToken);
            return _symbolIndex.WorkspaceSymbols(query).Take(maxLength).ToList();
        }

        public void AddPendingDoc(IDocument doc) {
            lock (_pendingDocs) {
                _pendingDocs.Add(doc);
            }
        }

        private void ReIndexPendingDocsAsync(object sender, EventArgs _) {
            IEnumerable<IDocument> pendingDocs;
            lock (_pendingDocs) {
                pendingDocs = _pendingDocs.ToList();
                _pendingDocs.Clear();
            }

            // Since its an event handler I have to synchronously wait
            Task.WaitAll(pendingDocs.MaybeEnumerate()
                .Select(doc => ReIndexFileAsync(doc.Uri.AbsolutePath, doc))
                .ToArray());
        }

        private class UriDocumentComparer : IEqualityComparer<IDocument> {
            public bool Equals(IDocument x, IDocument y) => x.Uri.Equals(y.Uri);

            public int GetHashCode(IDocument obj) => obj.Uri.GetHashCode();
        }
    }

}
