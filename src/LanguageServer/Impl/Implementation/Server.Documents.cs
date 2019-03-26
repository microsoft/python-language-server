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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public void DidOpenTextDocument(DidOpenTextDocumentParams @params) {
            _disposableBag.ThrowIfDisposed();
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Opening document {uri}");

            var doc = _rdt.OpenDocument(uri, @params.textDocument.text);
            _indexManager.ProcessNewFile(uri.AbsolutePath, doc);
        }

        public void DidChangeTextDocument(DidChangeTextDocumentParams @params) {
            _disposableBag.ThrowIfDisposed();
            var uri = @params.textDocument.uri;
            var doc = _rdt.GetDocument(uri);
            if (doc != null) {
                var changes = new List<DocumentChange>();
                foreach (var c in @params.contentChanges) {
                    Debug.Assert(c.range.HasValue);
                    var change = new DocumentChange {
                        InsertedText = c.text,
                        ReplacedSpan = c.range.Value
                    };
                    changes.Add(change);
                }
                doc.Update(changes);
                _indexManager.AddPendingDoc(doc);
            } else {
                _log?.Log(TraceEventType.Warning, $"Unable to find document for {@params.textDocument.uri}");
            }
        }

        public void DidChangeWatchedFiles(DidChangeWatchedFilesParams @params) {
            _disposableBag.ThrowIfDisposed();
            foreach (var c in @params.changes.MaybeEnumerate()) {
                switch (c.type) {
                    case FileChangeType.Deleted:
                        _interpreter.ModuleResolution.CurrentPathResolver.RemoveModulePath(c.uri.ToAbsolutePath());
                        break;
                }
            }
        }

        public void DidCloseTextDocument(DidCloseTextDocumentParams @params) {
            _disposableBag.ThrowIfDisposed();
            var uri = @params.textDocument.uri;
            _rdt.CloseDocument(uri);
            _indexManager.ProcessClosedFile(uri.AbsolutePath);
        }
    }
}
