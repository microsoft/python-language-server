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
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Modules;
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
            if (document != null) {
                return OpenDocument(document, ModuleLoadOptions.Open);
            }

            var mco = new ModuleCreationOptions {
                ModuleName = Path.GetFileNameWithoutExtension(uri.LocalPath),
                Content = content,
                Uri = uri,
                ModuleType = ModuleType.User,
                LoadOptions = ModuleLoadOptions.Open
            };
            return CreateDocument(mco);
        }

        /// <summary>
        /// Adds library module to the list of available documents.
        /// </summary>
        public IDocument AddModule(ModuleCreationOptions mco) {
            if (mco.Uri == null) {
                mco.FilePath = mco.FilePath ?? throw new ArgumentNullException(nameof(mco.FilePath));
                if (!Uri.TryCreate(mco.FilePath, UriKind.Absolute, out var uri)) {
                    throw new ArgumentException("Unable to determine URI from the file path.");
                }
                mco.Uri = uri;
            }

            return FindDocument(mco.FilePath, mco.Uri) ?? CreateDocument(mco);
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

        private IDocument CreateDocument(ModuleCreationOptions mco) {
            IDocument document;
            switch (mco.ModuleType) {
                case ModuleType.Stub:
                    document = new StubPythonModule(mco.ModuleName, mco.FilePath, _services);
                    break;
                case ModuleType.Compiled:
                    document = new CompiledPythonModule(mco.ModuleName, ModuleType.Compiled, mco.FilePath, mco.Stub, _services);
                    break;
                case ModuleType.CompiledBuiltin:
                    document = new CompiledBuiltinPythonModule(mco.ModuleName, mco.Stub, _services);
                    break;
                case ModuleType.User:
                case ModuleType.Library:
                    document = new PythonModule(mco, _services);
                    break;
                default:
                    throw new InvalidOperationException($"CreateDocument does not support module type {mco.ModuleType}");
            }

            _documentsByUri[document.Uri] = document;
            _documentsByName[mco.ModuleName] = document;

            return OpenDocument(document, mco.LoadOptions);
        }

        private IDocument OpenDocument(IDocument document, ModuleLoadOptions options) {
            if ((options & ModuleLoadOptions.Open) == ModuleLoadOptions.Open) {
                document.IsOpen = true;
                Opened?.Invoke(this, new DocumentEventArgs(document));
            }
            return document;
        }
    }
}
