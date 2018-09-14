// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.Python.LanguageServer.Implementation {
    sealed class AnalysisProgressReporter : IDisposable {
        private readonly DisposableBag _disposables = new DisposableBag(nameof(AnalysisProgressReporter));
        private readonly Dictionary<Uri, int> _activeAnalysis = new Dictionary<Uri, int>();
        private readonly IProgressService _progressService;
        private readonly ILogger _logger;
        private readonly Server _server;
        private readonly object _lock = new object();
        private IProgress _progress;

        public AnalysisProgressReporter(Server server, IProgressService progressService, ILogger logger) {
            _progressService = progressService;
            _logger = logger;

            _server = server;
            _server.OnAnalysisQueued += OnAnalysisQueued;
            _server.OnAnalysisComplete += OnAnalysisComplete;
            _disposables
                .Add(() => _server.OnAnalysisQueued -= OnAnalysisQueued)
                .Add(() => _server.OnAnalysisComplete -= OnAnalysisComplete)
                .Add(() => _progress?.Dispose());
        }

        public void Dispose() {
            _disposables.TryDispose();
        }

        private void OnAnalysisQueued(object sender, AnalysisQueuedEventArgs e) {
            lock (_lock) {
                if (_activeAnalysis.ContainsKey(e.uri)) {
                    _activeAnalysis[e.uri]++;
                } else {
                    _activeAnalysis[e.uri] = 1;
                }
                UpdateProgressMessage();
            }
        }
        private void OnAnalysisComplete(object sender, AnalysisCompleteEventArgs e) {
            lock (_lock) {
                if (_activeAnalysis.TryGetValue(e.uri, out var count)) {
                    if (count > 1) {
                        _activeAnalysis[e.uri]--;
                    } else {
                        _activeAnalysis.Remove(e.uri);
                    }
                } else {
                    _logger.TraceMessage($"Analysis completed for {e.uri} that is not in the dictionary.");
                }
                UpdateProgressMessage();
            }
        }

        private void UpdateProgressMessage() {
            if(_activeAnalysis.Count > 0) {
                _progress = _progress ?? _progressService.BeginProgress();
                _progress.Report(_activeAnalysis.Count == 1
                    ? Resources.AnalysisProgress_SingleItemRemaining
                    : Resources.AnalysisProgress_MultipleItemsRemaining.FormatInvariant(_activeAnalysis.Count)).DoNotWait();
            } else {
                _progress?.Dispose();
                _progress = null;
            }
        }
    }
}
