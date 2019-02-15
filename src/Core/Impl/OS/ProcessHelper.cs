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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Core.OS {
    public sealed class ProcessHelper : IDisposable {
        private Process _process;
        private int? _exitCode;
        private readonly AsyncManualResetEvent _seenNullOutput, _seenNullError;

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

            _seenNullOutput = new AsyncManualResetEvent();
            _seenNullError = new AsyncManualResetEvent();
        }

        public ProcessStartInfo StartInfo { get; }
        public string FileName => StartInfo.FileName;
        public string Arguments => StartInfo.Arguments;

        public Action<string> OnOutputLine { get; set; }
        public Action<string> OnErrorLine { get; set; }

        public void Dispose() {
            _seenNullOutput.Set();
            _seenNullError.Set();
            _process?.Dispose();
        }

        public void Start() {
            _seenNullOutput.Reset();
            _seenNullError.Reset();

            var p = new Process {
                StartInfo = StartInfo
            };

            p.Exited += Process_Exited;
            p.OutputDataReceived += Process_OutputDataReceived;
            p.ErrorDataReceived += Process_ErrorDataReceived;

            try {
                p.Start();
            } catch (Exception ex) {
                // Capture the error as stderr and exit code, then
                // clean up.
                _exitCode = ex.HResult;
                OnErrorLine?.Invoke(ex.ToString());
                _seenNullOutput.Set();
                _seenNullError.Set();
                p.OutputDataReceived -= Process_OutputDataReceived;
                p.ErrorDataReceived -= Process_ErrorDataReceived;
                p.Exited -= Process_Exited;
                return;
            }

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.EnableRaisingEvents = true;

            // Close stdin so that if the process tries to read it will exit
            p.StandardInput.Close();

            _process = p;
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            try {
                if (e.Data == null) {
                    _seenNullError.Set();
                } else {
                    OnErrorLine?.Invoke(e.Data.TrimEnd());
                }
            } catch (ObjectDisposedException) {
                ((Process)sender).ErrorDataReceived -= Process_ErrorDataReceived;
            }
        }

        private void Process_Exited(object sender, EventArgs eventArgs) {
            _seenNullOutput.Set();
            _seenNullError.Set();
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            try {
                if (e.Data == null) {
                    _seenNullOutput.Set();
                } else {
                    OnOutputLine?.Invoke(e.Data.TrimEnd());
                }
            } catch (ObjectDisposedException) {
                ((Process)sender).OutputDataReceived -= Process_OutputDataReceived;
            }
        }

        public void Kill() {
            try {
                if (_process != null && !_process.HasExited) {
                    _process.Kill();
                }
            } catch (SystemException) {
            }
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

            await _seenNullOutput.WaitAsync(cancellationToken).ConfigureAwait(false);
            await _seenNullError.WaitAsync(cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < 5 && !_process.HasExited; i++) {
                await Task.Delay(100, cancellationToken);
            }

            Debug.Assert(_process.HasExited, "Process still has not exited.");
            return _process.ExitCode;
        }
    }
}
