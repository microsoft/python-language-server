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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Documents {
    /// <summary>
    /// Represents set of files either opened in the editor or imported
    /// in order to provide analysis in open file. Rough equivalent of
    /// the running document table in Visual Studio, see
    /// "https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/running-document-table"/>
    /// </summary>
    public sealed class RunningDocumentTable : IRunningDocumentTable, IDisposable {
        private readonly Dictionary<Uri, DocumentEntry> _documentsByUri = new Dictionary<Uri, DocumentEntry>();
        private readonly Dictionary<string, DocumentEntry> _documentsByName = new Dictionary<string, DocumentEntry>();
        private readonly IServiceContainer _services;
        private readonly object _lock = new object();
        private readonly string _workspaceRoot;

        private IModuleManagement _moduleManagement;
        private IModuleManagement ModuleManagement => _moduleManagement ?? (_moduleManagement = _services.GetService<IPythonInterpreter>().ModuleResolution);

        private class DocumentEntry {
            public IDocument Document;
            public int LockCount;
        }

        public RunningDocumentTable(string workspaceRoot, IServiceContainer services) {
            _workspaceRoot = workspaceRoot;
            _services = services;
        }

        public event EventHandler<DocumentEventArgs> Opened;
        public event EventHandler<DocumentEventArgs> Closed;
        public event EventHandler<DocumentEventArgs> Removed;

        /// <summary>
        /// Adds file to the list of available documents.
        /// </summary>
        /// <param name="uri">Document URI.</param>
        /// <param name="content">Document content</param>
        /// <param name="filePath">Optional file path, if different from the URI.</param>
        public IDocument OpenDocument(Uri uri, string content, string filePath = null) {
            var justOpened = false;
            DocumentEntry entry;
            lock (_lock) {
                entry = FindDocument(null, uri);
                if (entry == null) {
                    var mco = new ModuleCreationOptions {
                        ModuleName = Path.GetFileNameWithoutExtension(uri.LocalPath),
                        Content = content,
                        FilePath = filePath,
                        Uri = uri,
                        ModuleType = ModuleType.User
                    };
                    entry = CreateDocument(mco);
                }
                justOpened = TryOpenDocument(entry, content);
            }
            if (justOpened) {
                Opened?.Invoke(this, new DocumentEventArgs(entry.Document));
            }
            return entry.Document;
        }

        /// <summary>
        /// Adds library module to the list of available documents.
        /// </summary>
        public IDocument AddModule(ModuleCreationOptions mco) {
            lock (_lock) {
                if (mco.Uri == null) {
                    mco.FilePath = mco.FilePath ?? throw new ArgumentNullException(nameof(mco.FilePath));
                    if (!Uri.TryCreate(mco.FilePath, UriKind.Absolute, out var uri)) {
                        throw new ArgumentException("Unable to determine URI from the file path.");
                    }

                    mco.Uri = uri;
                }

                var entry = FindDocument(mco.ModuleName, mco.Uri) ?? CreateDocument(mco);
                entry.LockCount++;
                return entry.Document;
            }
        }

        public IDocument GetDocument(Uri documentUri) {
            lock (_lock) {
                return _documentsByUri.TryGetValue(documentUri, out var entry) ? entry.Document : null;
            }
        }

        public IDocument GetDocument(string name) {
            lock (_lock) {
                return _documentsByName.TryGetValue(name, out var entry) ? entry.Document : null;
            }
        }

        public int LockDocument(Uri uri) {
            lock (_lock) {
                if (_documentsByUri.TryGetValue(uri, out var entry)) {
                    entry.LockCount++;
                    return entry.LockCount;
                }
                return -1;
            }
        }

        public int UnlockDocument(Uri uri) {
            lock (_lock) {
                if (_documentsByUri.TryGetValue(uri, out var entry)) {
                    entry.LockCount--;
                    return entry.LockCount;
                }
                return -1;
            }
        }

        public IEnumerator<IDocument> GetEnumerator() => _documentsByUri.Values.Select(e => e.Document).GetEnumerator();

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
                        _documentsByName.Remove(entry.Document.Name);
                        removed = true;
                        entry.Document.Dispose();
                    }
                }
            }
            if(closed) {
                Closed?.Invoke(this, new DocumentEventArgs(entry.Document));
            }
            if (removed) {
                Removed?.Invoke(this, new DocumentEventArgs(entry.Document));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => _documentsByUri.Values.GetEnumerator();

        public void Dispose() {
            lock(_lock) {
                foreach (var d in _documentsByUri.Values.OfType<IDisposable>()) {
                    d.Dispose();
                }
            }
        }

        private DocumentEntry FindDocument(string moduleName, Uri uri) {
            if (uri != null && _documentsByUri.TryGetValue(uri, out var entry)) {
                return entry;
            }
            if (!string.IsNullOrEmpty(moduleName) && _documentsByName.TryGetValue(moduleName, out entry)) {
                return entry;
            }
            return null;
        }

        private DocumentEntry CreateDocument(ModuleCreationOptions mco) {
            IDocument document;
            switch (mco.ModuleType) {
                case ModuleType.Compiled when TryAddModulePath(mco):
                    document = new CompiledPythonModule(mco.ModuleName, ModuleType.Compiled, mco.FilePath, mco.Stub, _services);
                    break;
                case ModuleType.CompiledBuiltin:
                    document = new CompiledBuiltinPythonModule(mco.ModuleName, mco.Stub, _services);
                    break;
                case ModuleType.User when TryAddModulePath(mco):
                case ModuleType.Library when TryAddModulePath(mco):
                    document = new PythonModule(mco, _services);
                    break;
                default:
                    throw new InvalidOperationException($"CreateDocument does not support module type {mco.ModuleType}");
            }

            var entry = new DocumentEntry { Document = document, LockCount = 0 };
            _documentsByUri[document.Uri] = entry;
            _documentsByName[mco.ModuleName] = entry;
            return entry;
        }

        private bool TryAddModulePath(ModuleCreationOptions mco) {
            var filePath = mco.FilePath ?? mco.Uri?.ToAbsolutePath();
            if (filePath == null) {
                throw new InvalidOperationException("Can't create document with no file path or URI specified");
            }

            if (!ModuleManagement.TryAddModulePath(filePath, out var fullName)) {
                return false;
            }

            mco.FilePath = filePath;
            mco.ModuleName = fullName;
            return true;
        }

        private bool TryOpenDocument(DocumentEntry entry, string content) {
            if (!entry.Document.IsOpen) {
                entry.Document.Reset(content);
                entry.Document.IsOpen = true;
                entry.LockCount++;
                return true;
            }
            return false;
        }
    }
}
