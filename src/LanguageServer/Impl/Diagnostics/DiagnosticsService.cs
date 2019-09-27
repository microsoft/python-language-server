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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.Services;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Diagnostics {
    internal sealed class DiagnosticsService : IDiagnosticsService, IDisposable {
        private sealed class DocumentDiagnostics {
            private readonly Dictionary<DiagnosticSource, IReadOnlyList<DiagnosticsEntry>> _entries
                = new Dictionary<DiagnosticSource, IReadOnlyList<DiagnosticsEntry>>();

            public IReadOnlyDictionary<DiagnosticSource, IReadOnlyList<DiagnosticsEntry>> Entries => _entries;

            public bool Changed { get; set; }

            public void Clear(DiagnosticSource source) {
                if (!_entries.TryGetValue(source, out _)) {
                    return;
                }

                Changed = true;
                _entries[source] = Array.Empty<DiagnosticsEntry>();
            }

            public void ClearAll() {
                Changed = true;
                _entries.Clear();
            }

            public void SetDiagnostics(DiagnosticSource source, IReadOnlyList<DiagnosticsEntry> entries) {
                if (_entries.TryGetValue(source, out var existing)) {
                    if (existing.SetEquals(entries)) {
                        return;
                    }
                } else if (entries.Count == 0) {
                    return;
                }
                _entries[source] = entries;
                Changed = true;
            }
        }

        private readonly Dictionary<Uri, DocumentDiagnostics> _diagnostics = new Dictionary<Uri, DocumentDiagnostics>();
        private readonly DisposableBag _disposables = DisposableBag.Create<DiagnosticsService>();
        private readonly IServiceContainer _services;
        private readonly IClientApplication _clientApp;
        private readonly object _lock = new object();
        private DiagnosticsSeverityMap _severityMap = new DiagnosticsSeverityMap();
        private IRunningDocumentTable _rdt;
        private DateTime _lastChangeTime;

        private IRunningDocumentTable Rdt {
            get {
                ConnectToRdt();
                return _rdt;
            }
        }

        public DiagnosticsService(IServiceContainer services) {
            _services = services;
            _clientApp = services.GetService<IClientApplication>();

            var idleTimeService = services.GetService<IIdleTimeService>();
            if (idleTimeService != null) {
                idleTimeService.Idle += OnIdle;
                idleTimeService.Closing += OnClosing;

                _disposables
                    .Add(() => idleTimeService.Idle -= OnIdle)
                    .Add(() => idleTimeService.Idle -= OnClosing);
            }
        }

        #region IDiagnosticsService
        public IReadOnlyDictionary<Uri, IReadOnlyList<DiagnosticsEntry>> Diagnostics {
            get {
                lock (_lock) {
                    return _diagnostics.ToDictionary(
                        kvp => kvp.Key,
                        kvp => Rdt.GetDocument(kvp.Key)?.IsOpen == true
                            ? FilterBySeverityMap(kvp.Value).ToList() as IReadOnlyList<DiagnosticsEntry>
                            : Array.Empty<DiagnosticsEntry>()
                    );
                }
            }
        }

        public void Replace(Uri documentUri, IEnumerable<DiagnosticsEntry> entries, DiagnosticSource source) {
            lock (_lock) {
                if (!_diagnostics.TryGetValue(documentUri, out var documentDiagnostics)) {
                    documentDiagnostics = new DocumentDiagnostics();
                    _diagnostics[documentUri] = documentDiagnostics;
                }
                documentDiagnostics.SetDiagnostics(source, entries.ToArray());
                _lastChangeTime = DateTime.Now;
            }
        }

        public void Remove(Uri documentUri) {
            lock (_lock) {
                // Before removing the document, make sure we clear its diagnostics.
                if (_diagnostics.TryGetValue(documentUri, out var d)) {
                    d.ClearAll();
                    PublishDiagnostics();
                    _diagnostics.Remove(documentUri);
                }
            }
        }

        public int PublishingDelay { get; set; } = 1000;

        public DiagnosticsSeverityMap DiagnosticsSeverityMap {
            get => _severityMap;
            set {
                lock (_lock) {
                    _severityMap = value;
                    foreach (var d in _diagnostics) {
                        d.Value.Changed = true;
                    }

                    PublishDiagnostics();

                    _lastChangeTime = DateTime.Now;
                }
            }
        }
        #endregion

        public void Dispose() {
            _disposables.TryDispose();
            ClearAllDiagnostics();
        }

        private void OnClosing(object sender, EventArgs e) => Dispose();

        private void OnIdle(object sender, EventArgs e) {
            if ((DateTime.Now - _lastChangeTime).TotalMilliseconds > PublishingDelay) {
                ConnectToRdt();
                PublishDiagnostics();
            }
        }

        private void PublishDiagnostics() {
            var diagnostics = new Dictionary<Uri, DocumentDiagnostics>();
            lock (_lock) {
                foreach (var d in _diagnostics) {
                    if (d.Value.Changed) {
                        diagnostics[d.Key] = d.Value;
                        d.Value.Changed = false;
                    }
                }

                foreach (var kvp in diagnostics) {
                    var parameters = new PublishDiagnosticsParams {
                        uri = kvp.Key,
                        diagnostics = Rdt.GetDocument(kvp.Key)?.IsOpen == true
                                ? FilterBySeverityMap(kvp.Value).Select(ToDiagnostic).ToArray()
                                : Array.Empty<Diagnostic>()
                    };
                    _clientApp.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", parameters).DoNotWait();
                }
            }
        }

        private void ClearAllDiagnostics() {
            lock (_lock) {
                _diagnostics.Clear();
            }
        }

        private static Diagnostic ToDiagnostic(DiagnosticsEntry e) {
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

        private IEnumerable<DiagnosticsEntry> FilterBySeverityMap(DocumentDiagnostics d)
            => d.Entries
                .SelectMany(kvp => kvp.Value)
                .Where(e => DiagnosticsSeverityMap.GetEffectiveSeverity(e.ErrorCode, e.Severity) != Severity.Suppressed)
                .Select(e => new DiagnosticsEntry(
                    e.Message,
                    e.SourceSpan,
                    e.ErrorCode,
                    DiagnosticsSeverityMap.GetEffectiveSeverity(e.ErrorCode, e.Severity),
                    e.Source)
                ).ToArray();

        private void ConnectToRdt() {
            lock (_lock) {
                if (_rdt == null) {
                    _rdt = _services.GetService<IRunningDocumentTable>();
                    if (_rdt != null) {
                        _rdt.Opened += OnOpenDocument;
                        _rdt.Closed += OnCloseDocument;
                        _rdt.Removed += OnRemoveDocument;

                        _disposables
                            .Add(() => _rdt.Opened -= OnOpenDocument)
                            .Add(() => _rdt.Closed -= OnCloseDocument)
                            .Add(() => _rdt.Removed -= OnRemoveDocument);
                    }
                }
            }
        }

        private void OnOpenDocument(object sender, DocumentEventArgs e) {
            lock (_lock) {
                if (_diagnostics.TryGetValue(e.Document.Uri, out var d)) {
                    d.Changed = d.Entries.Count > 0;
                }
            }
        }

        private void OnCloseDocument(object sender, DocumentEventArgs e) => ClearDiagnostics(e.Document.Uri, remove: false);
        private void OnRemoveDocument(object sender, DocumentEventArgs e) => ClearDiagnostics(e.Document.Uri, remove: true);

        private void ClearDiagnostics(Uri uri, bool remove) {
            lock (_lock) {
                if (_diagnostics.ContainsKey(uri)) {
                    PublishDiagnostics();
                    if (remove) {
                        _diagnostics.Remove(uri);
                    }
                }
            }
        }
    }
}
