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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal class IndexManager : IIndexManager {
        private static int DefaultReIndexDelay = 1000;
        private readonly ISymbolIndex _symbolIndex;
        private readonly IFileSystem _fileSystem;
        private readonly string _workspaceRootPath;
        private readonly string[] _includeFiles;
        private readonly string[] _excludeFiles;
        private readonly IIdleTimeService _idleTimeService;
        private readonly CancellationTokenSource _allIndexCts = new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> _addRootTcs = new TaskCompletionSource<bool>();
        private readonly HashSet<IDocument> _pendingDocs = new HashSet<IDocument>(new UriDocumentComparer());
        private readonly PythonLanguageVersion _version;
        private DateTime _lastPendingDocAddedTime;

        public IndexManager(ISymbolIndex symbolIndex, IFileSystem fileSystem, PythonLanguageVersion version, string rootPath, string[] includeFiles,
            string[] excludeFiles, IIdleTimeService idleTimeService) {
            Check.ArgumentNotNull(nameof(fileSystem), fileSystem);
            Check.ArgumentNotNull(nameof(rootPath), rootPath);
            Check.ArgumentNotNull(nameof(includeFiles), includeFiles);
            Check.ArgumentNotNull(nameof(excludeFiles), excludeFiles);

            _symbolIndex = symbolIndex;
            _fileSystem = fileSystem;
            _workspaceRootPath = rootPath;
            _includeFiles = includeFiles;
            _excludeFiles = excludeFiles;
            _idleTimeService = idleTimeService;
            _idleTimeService.Idle += OnIdle;
            _version = version;
            ReIndexingDelay = DefaultReIndexDelay;
            StartAddRootDir();
        }

        public int ReIndexingDelay { get; set; }

        private void StartAddRootDir() {
            foreach (var fileInfo in WorkspaceFiles()) {
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    _symbolIndex.Parse(fileInfo.FullName);
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
                _symbolIndex.Parse(path);
            } else {
                _symbolIndex.Delete(path);
            }
        }

        private bool IsFileOnWorkspace(string path) {
            if (string.IsNullOrEmpty(_workspaceRootPath)) {
                return false;
            }
            return _fileSystem.IsPathUnderRoot(_workspaceRootPath, path);
        }

        public void ProcessNewFile(string path, IDocument doc) {
            _symbolIndex.Add(path, doc);
        }

        public void ReIndexFile(string path, IDocument doc) {
            _symbolIndex.ReIndex(path, doc);
        }

        public void Dispose() {
            //_symbolIndex.Dispose();
            _allIndexCts.Cancel();
            _allIndexCts.Dispose();
        }

        public Task<IReadOnlyList<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path, CancellationToken cancellationToken = default) {
            return _symbolIndex.HierarchicalDocumentSymbols(path);
        }

        public Task<IReadOnlyList<FlatSymbol>> WorkspaceSymbolsAsync(string query, int maxLength, CancellationToken cancellationToken = default) {
            return _symbolIndex.WorkspaceSymbolsAsync(query, maxLength, cancellationToken);
        }

        public void AddPendingDoc(IDocument doc) {
            lock (_pendingDocs) {
                _lastPendingDocAddedTime = DateTime.Now;
                _pendingDocs.Add(doc);
                _symbolIndex.MarkAsPending(doc.Uri.AbsolutePath);
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
            return new MostRecentDocumentSymbols(path, _fileSystem, _version);
        }

        private class UriDocumentComparer : IEqualityComparer<IDocument> {
            public bool Equals(IDocument x, IDocument y) => x.Uri.Equals(y.Uri);

            public int GetHashCode(IDocument obj) => obj.Uri.GetHashCode();
        }
    }

}
