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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.Shell;
using Microsoft.PythonTools.Analysis;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Diagnostics {
    internal sealed class DiagnosticsPublisher : IDisposable {
        private readonly Dictionary<Uri, Diagnostic[]> _pendingDiagnostic = new Dictionary<Uri, Diagnostic[]>();
        private readonly DisposableBag _disposables = DisposableBag.Create<DiagnosticsPublisher>();
        private readonly JsonRpc _rpc;
        private readonly object _lock = new object();
        private DateTime _lastChangeTime;

        public DiagnosticsPublisher(Implementation.Server server, IServiceContainer services) {
            var s = server;
            s.OnPublishDiagnostics += OnPublishDiagnostics;

            var idleTimeService = services.GetService<IIdleTimeService>();
            idleTimeService.Idle += OnIdle;
            idleTimeService.Closing += OnClosing;

            _rpc = services.GetService<JsonRpc>();

            _disposables
                .Add(() => s.OnPublishDiagnostics -= OnPublishDiagnostics)
                .Add(() => idleTimeService.Idle -= OnIdle)
                .Add(() => idleTimeService.Idle -= OnClosing);
        }

        public int PublishingDelay { get; set; }

        public void Dispose() => _disposables.TryDispose();

        private void OnClosing(object sender, EventArgs e) => Dispose();

        private void OnIdle(object sender, EventArgs e) {
            if (_pendingDiagnostic.Count > 0 && (DateTime.Now - _lastChangeTime).TotalMilliseconds > PublishingDelay) {
                PublishPendingDiagnostics();
            }
        }

        private void OnPublishDiagnostics(object sender, PublishDiagnosticsEventArgs e) {
            lock (_lock) {
                // If list is empty (errors got fixed), publish immediately,
                // otherwise throttle so user does not get spurious squiggles
                // while typing normally.
                var diags = e.diagnostics.ToArray();
                _pendingDiagnostic[e.uri] = diags;
                if (diags.Length == 0) {
                    PublishPendingDiagnostics();
                }
                _lastChangeTime = DateTime.Now;
            }
        }

        private void PublishPendingDiagnostics() {
            List<KeyValuePair<Uri, Diagnostic[]>> list;

            lock (_lock) {
                list = _pendingDiagnostic.ToList();
                _pendingDiagnostic.Clear();
            }

            foreach (var kvp in list) {
                var parameters = new PublishDiagnosticsParams {
                    uri = kvp.Key,
                    diagnostics = kvp.Value.Where(d => d.severity != DiagnosticSeverity.Unspecified).ToArray()
                };
                _rpc.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", parameters).DoNotWait();
            }
        }

        [JsonObject]
        private class PublishDiagnosticsParams {
            [JsonProperty]
            public Uri uri;
            [JsonProperty]
            public Diagnostic[] diagnostics;
        }
    }
}
