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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Diagnostics {
    internal sealed class DiagnosticsService : IDiagnosticsService, IDisposable {
        private readonly Dictionary<Uri, List<DiagnosticsEntry>> _pendingDiagnostics = new Dictionary<Uri, List<DiagnosticsEntry>>();
        private readonly DisposableBag _disposables = DisposableBag.Create<DiagnosticsService>();
        private readonly JsonRpc _rpc;
        private readonly object _lock = new object();
        private DateTime _lastChangeTime;
        private bool _changed;

        public DiagnosticsService(IServiceContainer services) {
            var idleTimeService = services.GetService<IIdleTimeService>();
            idleTimeService.Idle += OnIdle;
            idleTimeService.Closing += OnClosing;

            _rpc = services.GetService<JsonRpc>();

            _disposables
                .Add(() => idleTimeService.Idle -= OnIdle)
                .Add(() => idleTimeService.Idle -= OnClosing);
        }

        #region IDiagnosticsService
        public IReadOnlyList<DiagnosticsEntry> Diagnostics {
            get {
                lock (_lock) {
                    return _pendingDiagnostics.Values.SelectMany().ToArray();
                }
            }
        }

        public void Add(Uri documentUri, DiagnosticsEntry entry) {
            lock (_lock) {
                if (!_pendingDiagnostics.TryGetValue(documentUri, out var list)) {
                    _pendingDiagnostics[documentUri] = list = new List<DiagnosticsEntry>();
                }
                list.Add(entry);
                _lastChangeTime = DateTime.Now;
                _changed = true;
            }
        }

        public void Clear(Uri documentUri) {
            lock (_lock) {
                ClearPendingDiagnostics(documentUri);
                _pendingDiagnostics.Remove(documentUri);
            }
        }

        public int PublishingDelay { get; set; }
        #endregion

        public void Dispose() {
            _disposables.TryDispose();
            ClearAllPendingDiagnostics();
        }

        private void OnClosing(object sender, EventArgs e) => Dispose();

        private void OnIdle(object sender, EventArgs e) {
            if (_changed && (DateTime.Now - _lastChangeTime).TotalMilliseconds > PublishingDelay) {
                PublishPendingDiagnostics();
            }
        }

        private void PublishPendingDiagnostics() {
            lock (_lock) {
                foreach (var kvp in _pendingDiagnostics) {
                    var parameters = new PublishDiagnosticsParams {
                        uri = kvp.Key,
                        diagnostics = kvp.Value.Select(x => ToDiagnostic(kvp.Key, x)).ToArray()
                    };
                    _rpc.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", parameters).DoNotWait();
                }
                _pendingDiagnostics.Clear();
                _changed = false;
            }
        }

        private void ClearAllPendingDiagnostics() {
            lock (_lock) {
                foreach (var uri in _pendingDiagnostics.Keys) {
                    ClearPendingDiagnostics(uri);
                }
                _changed = false;
            }
        }

        private void ClearPendingDiagnostics(Uri uri) {
            var parameters = new PublishDiagnosticsParams {
                uri = uri,
                diagnostics = Array.Empty<Diagnostic>()
            };
            _rpc.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", parameters).DoNotWait();
        }

        private static Diagnostic ToDiagnostic(Uri uri, DiagnosticsEntry e) {
            DiagnosticSeverity s;
            switch (e.Severity) {
                case Severity.Warning:
                    s = DiagnosticSeverity.Warning;
                    break;
                case Severity.Information:
                    s = DiagnosticSeverity.Information;
                    break;
                case Severity.Hint:
                    s = DiagnosticSeverity.Hint;
                    break;
                default:
                    s = DiagnosticSeverity.Error;
                    break;
            }

            return new Diagnostic {
                range = e.SourceSpan,
                severity = s,
                source = "Python",
                code = e.ErrorCode,
                message = e.Message,
            };
        }
    }
}
