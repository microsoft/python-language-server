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
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Documents {
    /// <summary>
    /// Represents set of files either opened in the editor or imported
    /// in order to provide analysis in open file. Rough equivalent of
    /// the running document table in Visual Studio, see
    /// "https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/running-document-table"/>
    /// </summary>
    public sealed class RunningDocumentTable : IRunningDocumentTable, IDisposable {
        private readonly Dictionary<Uri, DocumentEntry> _documentsByUri = new Dictionary<Uri, DocumentEntry>();
        private readonly IServiceContainer _services;
        private readonly ILogger _log;
        private readonly object _lock = new object();

        private IModuleManagement _moduleManagement;
        private IModuleManagement ModuleManagement => _moduleManagement ?? (_moduleManagement = _services.GetService<IPythonInterpreter>().ModuleResolution);

        private class DocumentEntry {
            public readonly IDocument Document;
            public int LockCount;

            public DocumentEntry(IDocument document) {
                Document = document;
                LockCount = 0;
            }
        }

        public RunningDocumentTable(IServiceContainer services) {
            _services = services;
            _log = _services.GetService<ILogger>();
        }

        public event EventHandler<DocumentEventArgs> Opened;
        public event EventHandler<DocumentEventArgs> Closed;
        public event EventHandler<DocumentEventArgs> Removed;

        /// <summary>
        /// Returns collection of currently open or loaded modules.
        /// Does not include stubs or compiled/scraped modules.
        /// </summary>
        public IEnumerable<IDocument> GetDocuments() {
            lock (_lock) {
                return _documentsByUri.Values.Select(e => e.Document).ToArray();
            }
        }

        public int DocumentCount {
            get {
                lock (_lock) {
                    return _documentsByUri.Count;
                }
            }
        }

        /// <summary>
        /// Adds file to the list of available documents.
        /// </summary>
        /// <param name="uri">Document URI.</param>
        /// <param name="content">Document content</param>
        /// <param name="filePath">Optional file path, if different from the URI.</param>
        public IDocument OpenDocument(Uri uri, string content, string filePath = null) {
            bool justOpened;
            var created = false;
            IDocument document;
            lock (_lock) {
                var entry = FindDocument(uri);
                if (entry == null) {
                    var resolver = _services.GetService<IPythonInterpreter>().ModuleResolution.CurrentPathResolver;

                    var moduleType = ModuleType.User;
                    var path = uri.ToAbsolutePath();
                    if (Path.IsPathRooted(path)) {
                        moduleType = resolver.IsLibraryFile(uri.ToAbsolutePath()) ? ModuleType.Library : ModuleType.User;
                    }

                    var mco = new ModuleCreationOptions {
                        ModuleName = Path.GetFileNameWithoutExtension(uri.LocalPath),
                        Content = content,
                        FilePath = filePath,
                        Uri = uri,
                        ModuleType = moduleType
                    };
                    entry = CreateDocument(mco);
                    created = true;
                }
                justOpened = TryOpenDocument(entry, content);
                document = entry.Document;
            }

            if (created) {
                _services.GetService<IPythonAnalyzer>().InvalidateAnalysis(document);
            }

            if (justOpened) {
                Opened?.Invoke(this, new DocumentEventArgs(document));
            }

            return document;
        }

        /// <summary>
        /// Adds library module to the list of available documents.
        /// </summary>
        public IDocument AddModule(ModuleCreationOptions mco) {
            IDocument document;

            lock (_lock) {
                if (mco.Uri == null) {
                    mco.FilePath = mco.FilePath ?? throw new ArgumentNullException(nameof(mco.FilePath));
                    if (!Uri.TryCreate(mco.FilePath, UriKind.Absolute, out var uri)) {
                        var message = $"Unable to determine URI from the file path {mco.FilePath}";
                        _log?.Log(TraceEventType.Warning, message);
                        throw new OperationCanceledException(message);
                    }

                    mco.Uri = uri;
                }

                var entry = FindDocument(mco.Uri) ?? CreateDocument(mco);
                entry.LockCount++;
                document = entry.Document;
            }

            _services.GetService<IPythonAnalyzer>().InvalidateAnalysis(document);
            return document;
        }

        public IDocument GetDocument(Uri documentUri) {
            lock (_lock) {
                return _documentsByUri.TryGetValue(documentUri, out var entry) ? entry.Document : null;
            }
        }

        public int LockDocument(Uri uri) {
            lock (_lock) {
                if (_documentsByUri.TryGetValue(uri, out var entry)) {
                    return ++entry.LockCount;
                }
                return -1;
            }
        }

        public int UnlockDocument(Uri uri) {
            lock (_lock) {
                if (_documentsByUri.TryGetValue(uri, out var entry)) {
                    return --entry.LockCount;
                }
                return -1;
            }
        }

        public void CloseDocument(Uri documentUri) {
            var closed = false;
            var removed = false;
            DocumentEntry entry;
            lock (_lock) {
                if (_documentsByUri.TryGetValue(documentUri, out entry)) {
                    Debug.Assert(entry.LockCount >= 1);

                    if (entry.Document.IsOpen) {
                        entry.Document.IsOpen = false;
                        closed = true;
                    }

                    entry.LockCount--;

                    if (entry.LockCount == 0) {
                        _documentsByUri.Remove(documentUri);
                        removed = true;
                        entry.Document.Dispose();
                    }
                }
            }
            if (closed) {
                Closed?.Invoke(this, new DocumentEventArgs(entry.Document));
            }
            if (removed) {
                Removed?.Invoke(this, new DocumentEventArgs(entry.Document));
            }
        }

        public void ReloadAll() {
            ImmutableArray<KeyValuePair<Uri, DocumentEntry>> opened;
            ImmutableArray<KeyValuePair<Uri, DocumentEntry>> closed;

            lock (_lock) {
                _documentsByUri.Split(kvp => kvp.Value.Document.IsOpen, out opened, out closed);

                foreach (var (uri, entry) in closed) {
                    _documentsByUri.Remove(uri);
                    entry.Document.Dispose();
                }
            }

            foreach (var (_, entry) in closed) {
                Closed?.Invoke(this, new DocumentEventArgs(entry.Document));
                Removed?.Invoke(this, new DocumentEventArgs(entry.Document));
            }

            foreach (var (_, entry) in opened) {
                entry.Document.Invalidate();
            }
        }

        public void Dispose() {
            lock (_lock) {
                foreach (var d in _documentsByUri.Values.OfType<IDisposable>()) {
                    d.Dispose();
                }
            }
        }

        private DocumentEntry FindDocument(Uri uri) {
            if (uri != null && _documentsByUri.TryGetValue(uri, out var entry)) {
                return entry;
            }

            return null;
        }

        private DocumentEntry CreateDocument(ModuleCreationOptions mco) {
            IDocument document;
            switch (mco.ModuleType) {
                case ModuleType.Compiled when TryAddModulePath(mco):
                    document = new CompiledPythonModule(mco.ModuleName, ModuleType.Compiled, mco.FilePath, mco.Stub, mco.IsPersistent, mco.IsTypeshed, _services);
                    break;
                case ModuleType.CompiledBuiltin:
                    document = new CompiledBuiltinPythonModule(mco.ModuleName, mco.Stub, mco.IsPersistent, _services);
                    break;
                case ModuleType.User:
                    TryAddModulePath(mco);
                    document = new PythonModule(mco, _services);
                    break;
                case ModuleType.Library when TryAddModulePath(mco):
                    document = new PythonModule(mco, _services);
                    break;
                default:
                    throw new InvalidOperationException($"CreateDocument does not support module type {mco.ModuleType}");
            }

            var entry = new DocumentEntry(document);
            _documentsByUri[document.Uri] = entry;
            return entry;
        }

        private bool TryAddModulePath(ModuleCreationOptions mco) {
            var filePath = mco.FilePath ?? mco.Uri?.ToAbsolutePath();
            if (filePath == null) {
                throw new InvalidOperationException("Can't create document with no file path or URI specified");
            }

            if (!ModuleManagement.TryAddModulePath(filePath, 0, true, out var fullName)) {
                return false;
            }

            mco.FilePath = filePath;
            mco.ModuleName = fullName;
            return true;
        }

        private bool TryOpenDocument(DocumentEntry entry, string content) {
            if (!entry.Document.IsOpen) {
                entry.Document.IsOpen = true;
                entry.LockCount++;
                return true;
            }
            return false;
        }
    }
}
