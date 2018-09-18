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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.Python.LanguageServer.Implementation {
    sealed class AnalysisProgressReporter : IDisposable {
        private readonly DisposableBag _disposables = new DisposableBag(nameof(AnalysisProgressReporter));
        private readonly IProgressService _progressService;
        private readonly ILogger _logger;
        private readonly Server _server;
        private readonly object _lock = new object();
        private readonly CancellationToken _cancellationToken;

        private IProgress _progress;
        private Task _queueMonitoringTask;

        public AnalysisProgressReporter(Server server, IProgressService progressService, ILogger logger, CancellationToken cancellationToken) {
            _progressService = progressService;
            _logger = logger;
            _cancellationToken = cancellationToken;

            _server = server;
            _server.OnAnalysisQueued += OnAnalysisQueued;
            _server.OnAnalysisComplete += OnAnalysisComplete;
            _disposables
                .Add(() => _server.OnAnalysisQueued -= OnAnalysisQueued)
                .Add(() => _server.OnAnalysisComplete -= OnAnalysisComplete)
                .Add(() => _progress?.Dispose());
        }

        public void Dispose() => _disposables.TryDispose();

        private void OnAnalysisQueued(object sender, AnalysisQueuedEventArgs e) {
            lock (_lock) {
                UpdateProgressMessage();
                _queueMonitoringTask = _queueMonitoringTask ?? QueueMonitoringTask();
            }
        }
        private void OnAnalysisComplete(object sender, AnalysisCompleteEventArgs e) {
            lock (_lock) {
                UpdateProgressMessage();
            }
        }

        private void UpdateProgressMessage() {
            var count = _server.EstimateRemainingWork();
            if (count > 0) {
                _progress = _progress ?? _progressService.BeginProgress();
                _progress.Report(count == 1
                    ? Resources.AnalysisProgress_SingleItemRemaining
                    : Resources.AnalysisProgress_MultipleItemsRemaining.FormatInvariant(count)).DoNotWait();
            } else {
                EndProgress();
            }
        }

        private async Task QueueMonitoringTask() {
            try {
                await _server.WaitForCompleteAnalysisAsync(_cancellationToken);
            } finally {
                EndProgress();
            }
        }

        private void EndProgress() {
            lock (_lock) {
                _progress?.Dispose();
                _progress = null;
                _queueMonitoringTask = null;
            }
        }
    }
}
