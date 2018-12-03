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
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Shell;

namespace Microsoft.Python.Analysis.Documents {
    /// <inheritdoc />
    internal sealed class DocumentTable : IDocumentTable {
        private readonly Dictionary<Uri, IDocument> _documentsByUri = new Dictionary<Uri, IDocument>();
        private readonly Dictionary<string, IDocument> _documentsByName = new Dictionary<string, IDocument>();
        private readonly IServiceContainer _services;
        private readonly IFileSystem _fs;
        private readonly string _workspaceRoot;

        public DocumentTable(IServiceManager services, string workspaceRoot) {
            services.AddService(this);
            _workspaceRoot = workspaceRoot;
            _services = services;
            _fs = services.GetService<IFileSystem>();
        }

        public event EventHandler<DocumentEventArgs> Opened;
        public event EventHandler<DocumentEventArgs> Closed;

        public IDocument AddDocument(string moduleName, string filePath, Uri uri = null) {
            if (uri != null && _documentsByUri.TryGetValue(uri, out var document)) {
                return document;
            }
            if (moduleName != null && _documentsByName.TryGetValue(moduleName, out document)) {
                return document;
            }
            if (uri == null && !Uri.TryCreate(filePath, UriKind.Absolute, out uri)) {
                throw new ArgumentException("Unable to determine file path from URI");
            }
            return CreateDocument(moduleName, filePath, uri, null);
        }

        public IDocument AddDocument(Uri uri, string content) {
            if (uri != null && _documentsByUri.TryGetValue(uri, out var document)) {
                return document;
            }
            return CreateDocument(Path.GetFileNameWithoutExtension(uri.LocalPath), uri.LocalPath, uri, content);
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
                Closed?.Invoke(this, new DocumentEventArgs(document));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => _documentsByUri.Values.GetEnumerator();

        private IDocument CreateDocument(string moduleName, string filePath, Uri uri, string content) {
            var document = new Document(moduleName, filePath, uri, _fs.IsPathUnderRoot(_workspaceRoot, filePath), content, _services);

            _documentsByUri[uri] = document;
            _documentsByName[moduleName] = document;

            Opened?.Invoke(this, new DocumentEventArgs(document));
            return document;
        }
    }
}
