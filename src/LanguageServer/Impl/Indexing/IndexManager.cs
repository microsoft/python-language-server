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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal class IndexManager : IIndexManager {
        private const int DefaultReIndexDelay = 350;
        private readonly PythonLanguageVersion _version;
        private readonly ISymbolIndex _userCodeSymbolIndex;
        private readonly ISymbolIndex _libraryCodeSymbolIndex;
        private readonly IFileSystem _fileSystem;
        private readonly string _workspaceRootPath;
        private readonly string[] _includeFiles;
        private readonly string[] _excludeFiles;
        private readonly DisposableBag _disposables = new DisposableBag(nameof(IndexManager));
        private readonly ConcurrentDictionary<IDocument, DateTime> _pendingDocs = new ConcurrentDictionary<IDocument, DateTime>(UriDocumentComparer.Instance);
        private readonly DisposeToken _disposeToken = DisposeToken.Create<IndexManager>();

        public IndexManager(IFileSystem fileSystem, PythonLanguageVersion version, string rootPath, string[] includeFiles,
            string[] excludeFiles, IIdleTimeService idleTimeService) {
            Check.ArgumentNotNull(nameof(fileSystem), fileSystem);
            Check.ArgumentNotNull(nameof(includeFiles), includeFiles);
            Check.ArgumentNotNull(nameof(excludeFiles), excludeFiles);
            Check.ArgumentNotNull(nameof(idleTimeService), idleTimeService);

            _version = version;
            _fileSystem = fileSystem;
            _workspaceRootPath = rootPath;
            _includeFiles = includeFiles;
            _excludeFiles = excludeFiles;

            _userCodeSymbolIndex = new SymbolIndex(_fileSystem, version);
            _libraryCodeSymbolIndex = new SymbolIndex(_fileSystem, version, libraryMode: true);

            idleTimeService.Idle += OnIdle;

            _disposables
                .Add(_userCodeSymbolIndex)
                .Add(_libraryCodeSymbolIndex)
                .Add(() => _disposeToken.TryMarkDisposed())
                .Add(() => idleTimeService.Idle -= OnIdle);
        }

        public int ReIndexingDelay { get; set; } = DefaultReIndexDelay;

        public Task IndexWorkspace(PathResolverSnapshot snapshot = null, CancellationToken ct = default) {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeToken.CancellationToken);
            var linkedCt = linkedCts.Token;
            return Task.Run(() => {
                var userFiles = WorkspaceFiles();
                CreateIndices(userFiles, _userCodeSymbolIndex, linkedCt);

                // index library files if asked
                // CreateIndices(LibraryFiles(snapshot).Except(userFiles, FileSystemInfoComparer.Instance), _libraryCodeSymbolIndex, linkedCt);
            }, linkedCt).ContinueWith(_ => linkedCts.Dispose());
        }

        private void CreateIndices(IEnumerable<IFileSystemInfo> files, ISymbolIndex symbolIndex, CancellationToken cancellationToken) {
            foreach (var fileInfo in files) {
                cancellationToken.ThrowIfCancellationRequested();
                if (ModulePath.IsPythonSourceFile(fileInfo.FullName)) {
                    symbolIndex.Parse(fileInfo.FullName);
                }
            }
        }

        private IEnumerable<IFileSystemInfo> WorkspaceFiles() {
            if (string.IsNullOrEmpty(_workspaceRootPath)) {
                return Enumerable.Empty<IFileSystemInfo>();
            }
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath).EnumerateFileSystemInfos(_includeFiles, _excludeFiles);
        }

        private IEnumerable<IFileSystemInfo> LibraryFiles(PathResolverSnapshot snapshot) {
            if (snapshot == null) {
                return Enumerable.Empty<IFileSystemInfo>();
            }

            var includeImplicit = !ModulePath.PythonVersionRequiresInitPyFiles(_version.ToVersion());
            return snapshot.GetAllImportableModuleFilePaths(includeImplicit).Select(p => new FileInfoProxy(new FileInfo(p)));
        }

        public void ProcessClosedFile(string path) {
            if (IsFileOnWorkspace(path)) {
                _userCodeSymbolIndex.Parse(path);
            } else {
                _userCodeSymbolIndex.Delete(path);
            }
        }

        private bool IsFileOnWorkspace(string path) {
            if (string.IsNullOrEmpty(_workspaceRootPath)) {
                return false;
            }
            return _fileSystem.GetDirectoryInfo(_workspaceRootPath)
                .Match(path, _includeFiles, _excludeFiles);
        }

        public void ProcessNewFile(string path, IDocument doc) {
            _userCodeSymbolIndex.Add(path, doc);
        }

        public void ReIndexFile(string path, IDocument doc) {
            _userCodeSymbolIndex.ReIndex(path, doc);
        }

        public void Dispose() {
            _disposables.TryDispose();
        }

        public async Task<IReadOnlyList<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path, CancellationToken cancellationToken = default) {
            var result = await _userCodeSymbolIndex.HierarchicalDocumentSymbolsAsync(path, cancellationToken);
            if (result.Count > 0) {
                return result;
            }

            return await _libraryCodeSymbolIndex.HierarchicalDocumentSymbolsAsync(path, cancellationToken);
        }

        public Task<IReadOnlyList<FlatSymbol>> WorkspaceSymbolsAsync(string query, int maxLength, CancellationToken cancellationToken = default) {
            return WorkspaceSymbolsAsync(query, maxLength, includeLibraries: false, cancellationToken);
        }

        public async Task<IReadOnlyList<FlatSymbol>> WorkspaceSymbolsAsync(string query, int maxLength, bool includeLibraries, CancellationToken cancellationToken = default) {
            var userCodeResult = await _userCodeSymbolIndex.WorkspaceSymbolsAsync(query, maxLength, cancellationToken);
            if (includeLibraries == false) {
                return userCodeResult;
            }

            var libraryCodeResult = await _libraryCodeSymbolIndex.WorkspaceSymbolsAsync(query, maxLength, cancellationToken);
            return userCodeResult.Concat(libraryCodeResult).ToList();
        }

        public void AddPendingDoc(IDocument doc) {
            _pendingDocs.TryAdd(doc, DateTime.Now);
            _userCodeSymbolIndex.MarkAsPending(doc.Uri.AbsolutePath);
        }

        private void OnIdle(object sender, EventArgs _) {
            ReIndexPendingDocsAsync();
        }

        private void ReIndexPendingDocsAsync() {
            foreach (var (doc, lastTime) in _pendingDocs) {
                if ((DateTime.Now - lastTime).TotalMilliseconds > ReIndexingDelay) {
                    ReIndexFile(doc.Uri.AbsolutePath, doc);
                    _pendingDocs.TryRemove(doc, out var _);
                }
            }
        }

        private class UriDocumentComparer : IEqualityComparer<IDocument> {
            public static readonly UriDocumentComparer Instance = new UriDocumentComparer();

            private UriDocumentComparer() { }
            public bool Equals(IDocument x, IDocument y) => x.Uri.Equals(y.Uri);

            public int GetHashCode(IDocument obj) => obj.Uri.GetHashCode();
        }

        private class FileSystemInfoComparer : IEqualityComparer<IFileSystemInfo> {
            public static readonly FileSystemInfoComparer Instance = new FileSystemInfoComparer();

            private FileSystemInfoComparer() { }
            public bool Equals(IFileSystemInfo x, IFileSystemInfo y) => x?.FullName == y?.FullName;
            public int GetHashCode(IFileSystemInfo obj) => obj.FullName.GetHashCode();
        }
    }

}
