﻿// Copyright(c) Microsoft Corporation
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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal sealed class MostRecentDocumentSymbols : IMostRecentDocumentSymbols {
        private readonly IIndexParser _indexParser;
        private readonly string _path;
        private readonly bool _library;

        // Only used to cancel all work when this object gets disposed.
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        // Objects for the currently running task.
        private readonly object _lock = new object();
        private TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>> _tcs = new TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>>();
        private CancellationTokenSource _workCts;

        public MostRecentDocumentSymbols(string path, IIndexParser indexParser, bool library) {
            _path = path;
            _indexParser = indexParser;
            _library = library;
        }

        public Task<IReadOnlyList<HierarchicalSymbol>> GetSymbolsAsync(CancellationToken ct = default) {
            lock (_lock) {
                return _tcs.Task.WaitAsync(ct);
            }
        }

        public void Parse() => DoWork(ParseAsync);

        public void Index(IDocument doc) => DoWork(ct => IndexAsync(doc, ct));

        private void DoWork(Func<CancellationToken, Task<IReadOnlyList<HierarchicalSymbol>>> work) {
            lock (_lock) {
                // Invalidate any existing work.
                MarkAsPending();

                // Create a new token for this specific work.
                _workCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

                // Start the task and set the result to _tcs if the task doesn't get canceled.
                UsingCts(_workCts, work).SetCompletionResultTo(_tcs, skipIfCanceled: true).DoNotWait();
            }

            // Because _workCts is created via CreateLinkedTokenSource, disposing of it sooner
            // rather than later is important (as there are real unmanaged resources behind linked CTSs).
            async Task<IReadOnlyList<HierarchicalSymbol>> UsingCts(
                CancellationTokenSource cts,
                Func<CancellationToken, Task<IReadOnlyList<HierarchicalSymbol>>> fn) {
                using (cts) {
                    var result = await fn(cts.Token);

                    lock (_lock) {
                        // Attempt to set _workCts to null if that's the current _workCts
                        // to avoid catching ObjectDisposedException all the time.
                        if (_workCts == cts) {
                            _workCts = null;
                        }
                    }

                    return result;
                }
            }
        }

        public void MarkAsPending() {
            lock (_lock) {
                // Cancel the existing work, if any.
                CancelWork();

                // If the previous task was completed, then every task returned from GetSymbolsAsync
                // will also be completed and it's too late to give them updated data.
                // Create a new _tcs for future calls to GetSymbolsAsync to use.
                //
                // If the previous task wasn't completed, then we want to give the previous calls to
                // GetSymbolsAsync the new result, so keep _tcs the same.
                if (_tcs.Task.IsCompleted) {
                    _tcs = new TaskCompletionSource<IReadOnlyList<HierarchicalSymbol>>();
                }
            }
        }

        public void Dispose() {
            lock (_lock) {
                _tcs.TrySetCanceled();

                try {
                    _workCts?.Dispose();
                } catch (ObjectDisposedException) {
                    // The task with this CTS completed and disposed already, ignore.
                }

                _cts?.Dispose();
            }
        }

        private void CancelWork() {
            if (_workCts != null) {
                try {
                    _workCts.Cancel();
                } catch (ObjectDisposedException) {
                    // The task with this CTS completed and disposed already, ignore.
                }
                _workCts = null;
            }
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> IndexAsync(IDocument doc, CancellationToken cancellationToken) {
            PythonAst ast = null;

            for (var i = 0; i < 5; i++) {
                cancellationToken.ThrowIfCancellationRequested();
                ast = await doc.GetAstAsync(cancellationToken);
                if (ast != null) {
                    break;
                }
                await Task.Delay(100);
            }

            if (ast == null) {
                return ImmutableArray<HierarchicalSymbol>.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var walker = new SymbolIndexWalker(ast, _library, cancellationToken);
            ast.Walk(walker);
            return walker.Symbols;
        }

        private async Task<IReadOnlyList<HierarchicalSymbol>> ParseAsync(CancellationToken cancellationToken) {
            try {
                var ast = await _indexParser.ParseAsync(_path, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var walker = new SymbolIndexWalker(ast, _library, cancellationToken);
                ast.Walk(walker);
                return walker.Symbols;
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                Trace.TraceError(e.Message);
            }

            return ImmutableArray<HierarchicalSymbol>.Empty;
        }
    }
}
