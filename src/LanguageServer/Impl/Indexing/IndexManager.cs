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
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<string, MostRecentDocumentSymbols> _files;
        private HashSet<IDocument> _pendingDocs;
        private DateTime _lastPendingDocAddedTime;

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
            _idleTimeService.Idle += OnIdle;
            _pendingDocs = new HashSet<IDocument>(new UriDocumentComparer());
            _files = new ConcurrentDictionary<string, MostRecentDocumentSymbols>(PathEqualityComparer.Instance);
            ReIndexingDelay = 1000;
            StartAddRootDir();

        }

        public int ReIndexingDelay { get; set; }

        private void StartAddRootDir() {
            foreach (var fileInfo in WorkspaceFiles()) {
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    _files.GetOrAdd(fileInfo.FullName, MakeMostRecentFileSymbols(fileInfo.FullName));
                    _files[fileInfo.FullName].Parse();
                }
            }
        }

        private IEnumerable<IFileSystemInfo> WorkspaceFiles() {
            if (string.IsNullOrEmpty(_workspaceRootPath)) {
                return Enumerable.Empty<IFileSystemInfo>();
            }
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
        }

        private bool IsFileIndexed(string path) => _symbolIndex.IsIndexed(path);

        public void ProcessClosedFile(string path) {
            if (IsFileOnWorkspace(path)) {
                _files[path].Parse();
            } else if (_files.TryRemove(path, out var fileSymbols)) {
                fileSymbols.Delete();
                fileSymbols.Dispose();
            }
        }

        private bool IsFileOnWorkspace(string path) {
            if (string.IsNullOrEmpty(_workspaceRootPath)) {
                return false;
            }
            return _fileSystem.IsPathUnderRoot(_workspaceRootPath, path);
        }

        public void ProcessNewFile(string path, IDocument doc) {
            _files.GetOrAdd(path, MakeMostRecentFileSymbols(path));
            _files[path].Add(doc);
        }

        public void ReIndexFile(string path, IDocument doc) {
            if (_files.TryGetValue(path, out var fileSymbols)) {
                fileSymbols.ReIndex(doc);
            }
        }

        public void Dispose() {
            foreach (var mostRecentSymbols in _files.Values) {
                mostRecentSymbols.Dispose();
            }
            _indexParser.Dispose();
            _allIndexCts.Cancel();
            _allIndexCts.Dispose();
        }

        public async Task<IReadOnlyList<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path, CancellationToken cancellationToken = default) {
            var s = await _files[path].GetSymbolsAsync();
            return s.ToList();
        }

        public async Task<IReadOnlyList<FlatSymbol>> WorkspaceSymbolsAsync(string query, int maxLength, CancellationToken cancellationToken = default) {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_allIndexCts.Token, cancellationToken);
            await Task.WhenAny(
                Task.WhenAll(_files.Values.Select(mostRecent => mostRecent.GetSymbolsAsync()).ToArray()),
                Task.Delay(Timeout.Infinite, linkedCts.Token));
            linkedCts.Dispose();
            return _symbolIndex.WorkspaceSymbols(query).Take(maxLength).ToList();
        }

        public void AddPendingDoc(IDocument doc) {
            lock (_pendingDocs) {
                _lastPendingDocAddedTime = DateTime.Now;
                _pendingDocs.Add(doc);
            }
        }

        private void OnIdle(object sender, EventArgs _) {
            if (_pendingDocs.Count > 0 && (DateTime.Now - _lastPendingDocAddedTime).TotalMilliseconds > ReIndexingDelay) {
                ReIndexPendingDocsAsync();
            }
        }

        private void ReIndexPendingDocsAsync() {
            IEnumerable<IDocument> pendingDocs;
            lock (_pendingDocs) {
                pendingDocs = _pendingDocs.ToList();
                _pendingDocs.Clear();
            }

            foreach (var doc in pendingDocs.MaybeEnumerate()) {
                ReIndexFile(doc.Uri.AbsolutePath, doc);
            }
        }

        private MostRecentDocumentSymbols MakeMostRecentFileSymbols(string path) {
            return new MostRecentDocumentSymbols(path, _indexParser, _symbolIndex);
        }

        private class UriDocumentComparer : IEqualityComparer<IDocument> {
            public bool Equals(IDocument x, IDocument y) => x.Uri.Equals(y.Uri);

            public int GetHashCode(IDocument obj) => obj.Uri.GetHashCode();
        }
    }

}
