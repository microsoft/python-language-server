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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Core.OS {
    public sealed class ProcessHelper : IDisposable {
        private readonly CancellationTokenSource _workerCts = new CancellationTokenSource();
        private readonly EventPipelineHandler _dataHandler;
        private readonly EventPipelineHandler _errorHandler;
        private Process _process;
        private int? _exitCode;

        public ProcessHelper(string filename, IEnumerable<string> arguments, string workingDir = null) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException("Could not launch process", filename);
            }

            StartInfo = new ProcessStartInfo(
                filename,
                arguments.AsQuotedArguments()
            ) {
                WorkingDirectory = workingDir ?? Path.GetDirectoryName(filename),
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _dataHandler = new EventPipelineHandler(s => OnOutputLine?.Invoke(s.TrimEnd()), _workerCts.Token);
            _errorHandler = new EventPipelineHandler(s => OnErrorLine?.Invoke(s.TrimEnd()), _workerCts.Token);
        }

        public ProcessStartInfo StartInfo { get; }
        public string FileName => StartInfo.FileName;
        public string Arguments => StartInfo.Arguments;

        public Action<string> OnOutputLine { get; set; }
        public Action<string> OnErrorLine { get; set; }

        public void Dispose() {
            _workerCts.Cancel();
            _process?.Dispose();
            Disconnect();
        }

        public void Start() {
            _process = new Process {StartInfo = StartInfo};
            _process.OutputDataReceived += Process_OutputDataReceived;
            _process.ErrorDataReceived += Process_ErrorDataReceived;

            try {
                _process.Start();
            } catch (Exception ex) {
                // Capture the error as stderr and exit code, then
                // clean up.
                _exitCode = ex.HResult;
                OnErrorLine?.Invoke(ex.ToString());
                Disconnect();
                _workerCts.Cancel();
                return;
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _process.EnableRaisingEvents = true;

            // Close stdin so that if the process tries to read it will exit
            _process.StandardInput.Close();
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) => _dataHandler.OnDataReceived(e);
        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e) => _errorHandler.OnDataReceived(e);

        public void Kill() {
            try {
                if (_process != null && !_process.HasExited) {
                    _process.Kill();
                }
            } catch (SystemException) { }
        }

        public int? Wait(int milliseconds) {
            if (_exitCode != null) {
                return _exitCode;
            }

            var cts = new CancellationTokenSource(milliseconds);
            try {
                var t = WaitAsync(cts.Token);
                try {
                    t.Wait(cts.Token);
                    return t.Result;
                } catch (AggregateException ae) when (ae.InnerException != null) {
                    throw ae.InnerException;
                }
            } catch (OperationCanceledException) {
                return null;
            }
        }

        public async Task<int> WaitAsync(CancellationToken cancellationToken) {
            if (_exitCode != null) {
                return _exitCode.Value;
            }

            if (_process == null) {
                throw new InvalidOperationException("Process was not started");
            }

            await _dataHandler.Completed.WaitAsync(_workerCts.Token);
            await _errorHandler.Completed.WaitAsync(_workerCts.Token);

            for (var i = 0; i < 5 && !_process.HasExited; i++) {
                await Task.Delay(100, cancellationToken);
            }

            Debug.Assert(_process.HasExited, "Process still has not exited.");
            return _process.ExitCode;
        }

        private void Disconnect() {
            if (_process != null) {
                _process.OutputDataReceived -= Process_OutputDataReceived;
                _process.ErrorDataReceived -= Process_ErrorDataReceived;
            }
        }

        private sealed class EventPipelineHandler {
            private readonly ConcurrentQueue<DataReceivedEventArgs> _events = new ConcurrentQueue<DataReceivedEventArgs>();
            private readonly ManualResetEventSlim _dataAvailable = new ManualResetEventSlim();
            private readonly Action<string> _action;
            private readonly CancellationToken _cancellationToken;
            private readonly object _lock = new object();

            public EventPipelineHandler(Action<string> action, CancellationToken cancellationToken) {
                _cancellationToken = cancellationToken;
                _action = action;
                Task.Run(QueueWorker).DoNotWait();
            }

            public void OnDataReceived(DataReceivedEventArgs e) {
                lock (_lock) {
                    _events.Enqueue(e);
                    _dataAvailable.Set();
                }
            }

            public AsyncManualResetEvent Completed { get; } = new AsyncManualResetEvent();

            private void QueueWorker() {
                while (!_cancellationToken.IsCancellationRequested) {
                    DataReceivedEventArgs e;
                    // Make sure trying to get data and resetting the event is atomic
                    // so we don't get into a situation when caller places item
                    // in the queue, sets the 'available' event and then
                    // thread switches into between `TryDequeue` and `Reset`.
                    lock (_lock) {
                        if (!_events.TryDequeue(out e)) {
                            _dataAvailable.Reset();
                        }
                    }

                    if (e == null) {
                        try {
                            _dataAvailable.Wait(_cancellationToken);
                            continue;
                        } catch (OperationCanceledException) {
                            break;
                        }
                    }

                    try {
                        if (e.Data == null) {
                            Completed.Set();
                            break;
                        }

                        _action?.Invoke(e.Data.TrimEnd());
                    } catch (ObjectDisposedException) {
                        break;
                    }
                }
            }
        }
    }
}
