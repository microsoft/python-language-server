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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Modules;
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Documents {
    /// <summary>
    /// Represents set of files either opened in the editor or imported
    /// in order to provide analysis in open file. Rough equivalent of
    /// the running document table in Visual Studio, see
    /// "https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/running-document-table"/>
    /// </summary>
    internal sealed class RunningDocumentTable : IRunningDocumentTable, IDisposable {
        private readonly Dictionary<Uri, IDocument> _documentsByUri = new Dictionary<Uri, IDocument>();
        private readonly Dictionary<string, IDocument> _documentsByName = new Dictionary<string, IDocument>();
        private readonly IServiceContainer _services;
        private readonly IFileSystem _fs;
        private readonly string _workspaceRoot;

        public RunningDocumentTable(string workspaceRoot, IServiceContainer services) {
            _workspaceRoot = workspaceRoot;
            _services = services;
            _fs = services.GetService<IFileSystem>();
        }

        public event EventHandler<DocumentEventArgs> Opened;
        public event EventHandler<DocumentEventArgs> Closed;

        /// <summary>
        /// Adds file to the list of available documents.
        /// </summary>
        /// <param name="uri">Document URI.</param>
        /// <param name="content">Document content</param>
        public IDocument AddDocument(Uri uri, string content) {
            var document = FindDocument(null, uri);
            return document != null
                ? OpenDocument(document, DocumentCreationOptions.Open)
                : CreateDocument(null, ModuleType.User, null, uri, null, DocumentCreationOptions.Open);
        }

        /// <summary>
        /// Adds library module to the list of available documents.
        /// </summary>
        /// <param name="moduleName">The name of the module; used to associate with imports</param>
        /// <param name="moduleType">Module type (library or stub).</param>
        /// <param name="filePath">The path to the file on disk</param>
        /// <param name="uri">Document URI. Can be null if module is not a user document.</param>
        /// <param name="options">Document creation options.</param>
        public IDocument AddModule(string moduleName, ModuleType moduleType, string filePath, Uri uri, DocumentCreationOptions options) {
            if (uri == null) {
                filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
                if (!Uri.TryCreate(filePath, UriKind.Absolute, out uri)) {
                    throw new ArgumentException("Unable to determine URI from the file path.");
                }
            }
            return FindDocument(filePath, uri) ?? CreateDocument(moduleName, moduleType, filePath, uri, null, options);
        }

        public IDocument GetDocument(Uri documentUri)
            => _documentsByUri.TryGetValue(documentUri, out var document) ? document : null;

        public IDocument GetDocument(string name)
            => _documentsByName.TryGetValue(name, out var document) ? document : null;

        public IEnumerator<IDocument> GetEnumerator() => _documentsByUri.Values.GetEnumerator();

        public void RemoveDocument(Uri documentUri) {
            if (_documentsByUri.TryGetValue(documentUri, out var document)) {
                _documentsByUri.Remove(documentUri);
                _documentsByName.Remove(document.Name);
                document.IsOpen = false;
                Closed?.Invoke(this, new DocumentEventArgs(document));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => _documentsByUri.Values.GetEnumerator();

        public void Dispose() {
            foreach (var d in _documentsByUri.Values.OfType<IDisposable>()) {
                d.Dispose();
            }
        }

        private IDocument FindDocument(string moduleName, Uri uri) {
            if (uri != null && _documentsByUri.TryGetValue(uri, out var document)) {
                return document;
            }
            if (!string.IsNullOrEmpty(moduleName) && _documentsByName.TryGetValue(moduleName, out document)) {
                return document;
            }
            return null;
        }

        private IDocument CreateDocument(string moduleName, ModuleType moduleType, string filePath, Uri uri, string content, DocumentCreationOptions options) {
            IDocument document;
            switch(moduleType) {
                case ModuleType.Stub:
                    document = new StubPythonModule(moduleName, filePath, _services);
                    break;
                case ModuleType.Compiled:
                    document = new CompiledPythonModule(moduleName, ModuleType.Compiled, filePath, _services);
                    break;
                case ModuleType.CompiledBuiltin:
                    document = new CompiledBuiltinPythonModule(moduleName, _services);
                    break;
                case ModuleType.User:
                case ModuleType.Library:
                    document = CreateDocument(moduleName, moduleType, filePath, uri, content, options);
                    break;
                default:
                    throw new InvalidOperationException($"CreateDocument does not suppore module type {moduleType}");
            }

            _documentsByUri[document.Uri] = document;
            _documentsByName[moduleName] = document;

            return OpenDocument(document, options);
        }

        private IDocument OpenDocument(IDocument document, DocumentCreationOptions options) {
            if ((options & DocumentCreationOptions.Open) == DocumentCreationOptions.Open) {
                document.IsOpen = true;
                Opened?.Invoke(this, new DocumentEventArgs(document));
            }
            return document;
        }
    }
}
