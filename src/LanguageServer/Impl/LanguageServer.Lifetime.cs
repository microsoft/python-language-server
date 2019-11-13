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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Optimization;
using Microsoft.Python.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Implementation {
    public partial class LanguageServer {
        private InitializeParams _initParams;
        private bool _shutdown;
        private bool _initialized;

        private Task<IDisposable> _initializedPriorityTask;

        [JsonRpcMethod("initialize")]
        public async Task<InitializeResult> Initialize(JToken token, CancellationToken cancellationToken) {
            _initParams = token.ToObject<InitializeParams>();
            MonitorParentProcess(_initParams);
            RegisterServices(_initParams);

            using (await _prioritizer.InitializePriorityAsync(cancellationToken)) {
                Debug.Assert(!_initialized);
                // Force the next handled request to be "initialized", where the work actually happens.
                _initializedPriorityTask = _prioritizer.InitializedPriorityAsync();
                var result = await _server.InitializeAsync(_initParams, cancellationToken);
                return result;
            }
        }

        [JsonRpcMethod("initialized")]
        public async Task Initialized(JToken token, CancellationToken cancellationToken) {
            _services.GetService<IProfileOptimizationService>()?.Profile("Initialized");

            using (await _initializedPriorityTask) {
                Debug.Assert(!_initialized);
                var pythonSection = await GetPythonConfigurationAsync(cancellationToken, 200);
                var userConfiguredPaths = GetUserConfiguredPaths(pythonSection);

                await _server.InitializedAsync(ToObject<InitializedParams>(token), cancellationToken, userConfiguredPaths);
                await _rpc.NotifyAsync("python/languageServerStarted");
                _initialized = true;
            }
        }

        private async Task<JToken> GetPythonConfigurationAsync(CancellationToken cancellationToken = default, int? cancelAfterMilli = null) {
            if (_initParams?.capabilities?.workspace?.configuration != true) {
                return null;
            }

            try {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                    if (cancelAfterMilli.HasValue) {
                        cts.CancelAfter(cancelAfterMilli.Value);
                    }
                    var args = new ConfigurationParams {
                        items = new[] {
                            new ConfigurationItem {
                                scopeUri = _initParams.rootUri,
                                section = "python"
                            }
                        }
                    };
                    var configs = await _rpc.InvokeWithParameterObjectAsync<JToken>("workspace/configuration", args, cancellationToken);
                    return configs?[0];
                }
            } catch (Exception) { }

            // The cancellation of this token could have been caught above instead of the timeout, so rethrow it.
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        [JsonRpcMethod("shutdown")]
        public async Task Shutdown() {
            // Shutdown, but do not exit.
            // https://microsoft.github.io/language-server-protocol/specification#shutdown
            await _server.Shutdown();
            _shutdown = true;
        }

        [JsonRpcMethod("exit")]
        public void Exit() {
            _server.Dispose();
            _sessionTokenSource.Cancel();
            // Per https://microsoft.github.io/language-server-protocol/specification#exit
            Environment.Exit(_shutdown ? 0 : 1);
        }

        private void MonitorParentProcess(InitializeParams p) {
            // Monitor parent process
            Process parentProcess = null;
            if (p.processId.HasValue) {
                try {
                    parentProcess = Process.GetProcessById(p.processId.Value);
                } catch (ArgumentException) { }

                Debug.Assert(parentProcess != null, "Parent process does not exist");
                if (parentProcess != null) {
                    parentProcess.Exited += (s, e) => _sessionTokenSource.Cancel();
                }
            }

            if (parentProcess != null) {
                Task.Run(async () => {
                    while (!_sessionTokenSource.IsCancellationRequested) {
                        await Task.Delay(2000);
                        if (parentProcess.HasExited) {
                            _sessionTokenSource.Cancel();
                        }
                    }
                }).DoNotWait();
            }
        }

        private void RegisterServices(InitializeParams initParams) {
            // we need to register cache service first.
            // optimization service consumes the cache info.
            CacheService.Register(_services, initParams?.initializationOptions?.cacheFolderPath);
            _services.AddService(new ProfileOptimizationService(_services));
        }
    }
}
