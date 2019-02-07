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
using Microsoft.Python.Core.Services;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Diagnostics {
    internal sealed class DiagnosticsService : IDiagnosticsService, IDisposable {
        private readonly Dictionary<Uri, List<DiagnosticsEntry>> _diagnostics = new Dictionary<Uri, List<DiagnosticsEntry>>();
        private readonly DisposableBag _disposables = DisposableBag.Create<DiagnosticsService>();
        private readonly IClientApplication _clientApp;
        private readonly object _lock = new object();
        private DateTime _lastChangeTime;
        private bool _changed;

        public DiagnosticsService(IServiceContainer services) {
            var idleTimeService = services.GetService<IIdleTimeService>();

            if (idleTimeService != null) {
                idleTimeService.Idle += OnIdle;
                idleTimeService.Closing += OnClosing;

                _disposables
                    .Add(() => idleTimeService.Idle -= OnIdle)
                    .Add(() => idleTimeService.Idle -= OnClosing);
            }
            _clientApp = services.GetService<IClientApplication>();
        }

        #region IDiagnosticsService
        public IReadOnlyDictionary<Uri, IReadOnlyList<DiagnosticsEntry>> Diagnostics {
            get {
                lock (_lock) {
                    return _diagnostics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as IReadOnlyList<DiagnosticsEntry>);
                }
            }
        }

        public void Replace(Uri documentUri, IEnumerable<DiagnosticsEntry> entries) {
            lock (_lock) {
                _diagnostics[documentUri] = entries.ToList();
                _lastChangeTime = DateTime.Now;
                _changed = true;
            }
        }

        public void Remove(Uri documentUri) {
            lock (_lock) {
                // Before removing the document, make sure we clear its diagnostics.
                _diagnostics[documentUri] = new List<DiagnosticsEntry>();
                PublishDiagnostics();
                _diagnostics.Remove(documentUri);
            }
        }

        public int PublishingDelay { get; set; } = 1000;

        public DiagnosticsSeverityMap DiagnosticsSeverityMap { get; set; } = new DiagnosticsSeverityMap();
        #endregion

        public void Dispose() {
            _disposables.TryDispose();
            ClearAllDiagnostics();
        }

        private void OnClosing(object sender, EventArgs e) => Dispose();

        private void OnIdle(object sender, EventArgs e) {
            if (_changed && (DateTime.Now - _lastChangeTime).TotalMilliseconds > PublishingDelay) {
                PublishDiagnostics();
            }
        }

        private void PublishDiagnostics() {
            lock (_lock) {
                foreach (var kvp in _diagnostics) {
                    var parameters = new PublishDiagnosticsParams {
                        uri = kvp.Key,
                        diagnostics = kvp.Value.Select(ToDiagnostic).ToArray()
                    };
                    _clientApp.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", parameters).DoNotWait();
                }
                _changed = false;
            }
        }

        private void ClearAllDiagnostics() {
            lock (_lock) {
                _diagnostics.Clear();
                _changed = false;
            }
        }

        private Diagnostic ToDiagnostic(DiagnosticsEntry e) {
            DiagnosticSeverity s;
            var severity = DiagnosticsSeverityMap.GetEffectiveSeverity(e.ErrorCode, e.Severity);
            switch (severity) {
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
