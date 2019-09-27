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
using System.Threading;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class ProgressReporter : IProgressReporter, IDisposable {
        private readonly static TimeSpan InitialDelay = TimeSpan.FromMilliseconds(50);
        private readonly static TimeSpan ReportingInterval = TimeSpan.FromMilliseconds(100);

        private readonly IProgressService _progressService;
        private readonly object _lock = new object();
        private int _lastReportedCount;
        private int _lastReportedToClient;
        private IProgress _progress;
        private Timer _reportTimer;
        private bool _running;

        public ProgressReporter(IProgressService progressService) {
            _progressService = progressService;
        }

        public void Dispose() {
            lock (_lock) {
                EndReport();
            }
        }

        public void ReportRemaining(int count) {
            lock (_lock) {
                if (count == 0) {
                    EndReport();
                    return;
                }

                if (!_running) {
                    // Delay reporting a bit in case the analysis is short in order to reduce UI flicker.
                    _running = true;
                    _reportTimer?.Dispose();
                    _reportTimer = new Timer(OnReportTimer, null, InitialDelay, ReportingInterval);
                }
                _lastReportedCount = count;
            }
        }

        private void OnReportTimer(object o) {
            lock (_lock) {
                if (_running && _lastReportedToClient != _lastReportedCount) {
                    _lastReportedToClient = _lastReportedCount;
                    _progress = _progress ?? _progressService?.BeginProgress();
                    if (_lastReportedCount > 0) {
                        _progress?.Report(Resources.AnalysisProgress.FormatUI(_lastReportedCount)).DoNotWait();
                    }
                }
            }
        }

        private void EndReport() {
            if (_running) {
                _running = false;
                _progress?.Dispose();
                _progress = null;
                _reportTimer?.Dispose();
            }
        }
    }
}
