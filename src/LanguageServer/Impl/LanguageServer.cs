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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Threading;
using Microsoft.Python.LanguageServer.Extensibility;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Telemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Implementation {
    /// <summary>
    /// VS Code language server protocol implementation to use with StreamJsonRpc
    /// https://github.com/Microsoft/language-server-protocol/blob/gh-pages/specification.md
    /// https://github.com/Microsoft/vs-streamjsonrpc/blob/master/doc/index.md
    /// </summary>
    public sealed partial class LanguageServer : IDisposable {
        private readonly DisposableBag _disposables = new DisposableBag(nameof(LanguageServer));
        private readonly CancellationTokenSource _sessionTokenSource = new CancellationTokenSource();
        private readonly Prioritizer _prioritizer = new Prioritizer();
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();
        private readonly AnalysisOptionsProvider _optionsProvider = new AnalysisOptionsProvider();

        private IServiceManager _services;
        private Server _server;
        private ILogger _logger;
        private ITelemetryService _telemetry;
        private RequestTimer _requestTimer;

        private JsonRpc _rpc;
        private JsonSerializer _jsonSerializer;
        private IIdleTimeTracker _idleTimeTracker;

        public CancellationToken Start(IServiceManager services, JsonRpc rpc) {
            _server = new Server(services);
            _services = services;
            _rpc = rpc;

            _jsonSerializer = services.GetService<JsonSerializer>();
            _idleTimeTracker = services.GetService<IIdleTimeTracker>();
            _logger = services.GetService<ILogger>();
            _telemetry = services.GetService<ITelemetryService>();
            _requestTimer = new RequestTimer(_telemetry);

            var rpcTraceListener = new TelemetryRpcTraceListener(_telemetry);
            _rpc.TraceSource.Listeners.Add(rpcTraceListener);

            _disposables
                .Add(() => _shutdownCts.Cancel())
                .Add(_prioritizer)
                .Add(() => _rpc.TraceSource.Listeners.Remove(rpcTraceListener));

            services.AddService(_optionsProvider);
            return _sessionTokenSource.Token;
        }

        public void Dispose() {
            _disposables.TryDispose();
            _server.Dispose();
        }

        private void OnApplyWorkspaceEdit(object sender, ApplyWorkspaceEditEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("workspace/applyEdit", e.@params).DoNotWait();

        #region Workspace

        [JsonRpcMethod("workspace/didChangeWatchedFiles")]
        public async Task DidChangeWatchedFiles(JToken token, CancellationToken cancellationToken) {
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                _server.DidChangeWatchedFiles(ToObject<DidChangeWatchedFilesParams>(token));
            }
        }

        [JsonRpcMethod("workspace/symbol")]
        public async Task<SymbolInformation[]> WorkspaceSymbols(JToken token, CancellationToken cancellationToken) {
            using (var timer = _requestTimer.Time("workspace/symbol")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                var result = await _server.WorkspaceSymbols(ToObject<WorkspaceSymbolParams>(token), cancellationToken);
                timer.AddMeasure("count", result?.Length ?? 0);
                return result;
            }
        }

        #endregion

        #region Commands
        //[JsonRpcMethod("workspace/executeCommand")]
        //public Task<object> ExecuteCommand(JToken token, CancellationToken cancellationToken)
        //   => _server.ExecuteCommandAsync(ToObject<ExecuteCommandParams>(token), cancellationToken);
        #endregion

        #region TextDocument
        [JsonRpcMethod("textDocument/didOpen")]
        public async Task DidOpenTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                _server.DidOpenTextDocument(ToObject<DidOpenTextDocumentParams>(token));
            }
        }

        [JsonRpcMethod("textDocument/didChange")]
        public async Task DidChangeTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                var @params = ToObject<DidChangeTextDocumentParams>(token);
                _server.DidChangeTextDocument(@params);
            }
        }

        [JsonRpcMethod("textDocument/willSave")]
        public void WillSaveTextDocument(JToken token) { }

        [JsonRpcMethod("textDocument/willSaveWaitUntilTextDocument")]
        public TextEdit[] WillSaveWaitUntilTextDocument(JToken token) => Array.Empty<TextEdit>();

        [JsonRpcMethod("textDocument/didSave")]
        public void DidSaveTextDocument(JToken token) => _idleTimeTracker?.NotifyUserActivity();

        [JsonRpcMethod("textDocument/didClose")]
        public async Task DidCloseTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                _server.DidCloseTextDocument(ToObject<DidCloseTextDocumentParams>(token));
            }
        }
        #endregion

        #region Editor features
        [JsonRpcMethod("textDocument/completion")]
        public async Task<CompletionList> Completion(JToken token, CancellationToken cancellationToken) {
            using (var timer = _requestTimer.Time("textDocument/completion")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                var result = await _server.Completion(ToObject<CompletionParams>(token), GetToken(cancellationToken));
                timer.AddMeasure("count", result?.items?.Length ?? 0);
                return result;
            }
        }

        [JsonRpcMethod("textDocument/hover")]
        public async Task<Hover> Hover(JToken token, CancellationToken cancellationToken) {
            using (_requestTimer.Time("textDocument/hover")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                return await _server.Hover(ToObject<TextDocumentPositionParams>(token), GetToken(cancellationToken));
            }
        }

        [JsonRpcMethod("textDocument/signatureHelp")]
        public async Task<SignatureHelp> SignatureHelp(JToken token, CancellationToken cancellationToken) {
            using (_requestTimer.Time("textDocument/signatureHelp")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                return await _server.SignatureHelp(ToObject<TextDocumentPositionParams>(token), GetToken(cancellationToken));
            }
        }

        [JsonRpcMethod("textDocument/definition")]
        public async Task<Reference[]> GotoDefinition(JToken token, CancellationToken cancellationToken) {
            using (_requestTimer.Time("textDocument/definition")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                return await _server.GotoDefinition(ToObject<TextDocumentPositionParams>(token), GetToken(cancellationToken));
            }
        }

        [JsonRpcMethod("textDocument/declaration")]
        public async Task<Location> GotoDeclaration(JToken token, CancellationToken cancellationToken) {
            using (_requestTimer.Time("textDocument/declaration")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                return await _server.GotoDeclaration(ToObject<TextDocumentPositionParams>(token), GetToken(cancellationToken));
            }
        }

        [JsonRpcMethod("textDocument/references")]
        public async Task<Reference[]> FindReferences(JToken token, CancellationToken cancellationToken) {
            using (_requestTimer.Time("textDocument/references")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                return await _server.FindReferences(ToObject<ReferencesParams>(token), GetToken(cancellationToken));
            }
        }

        //[JsonRpcMethod("textDocument/documentHighlight")]
        //public async Task<DocumentHighlight[]> DocumentHighlight(JToken token, CancellationToken cancellationToken) {
        //    await _prioritizer.DefaultPriorityAsync(cancellationToken);
        //    return await _server.DocumentHighlight(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        //}

        [JsonRpcMethod("textDocument/documentSymbol")]
        public async Task<DocumentSymbol[]> DocumentSymbol(JToken token, CancellationToken cancellationToken) {
            using (_requestTimer.Time("textDocument/documentSymbol")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                // This call is also used by VSC document outline and it needs correct information
                return await _server.HierarchicalDocumentSymbol(ToObject<DocumentSymbolParams>(token), GetToken(cancellationToken));
            }
        }

        [JsonRpcMethod("textDocument/codeAction")]
        public async Task<CodeAction[]> CodeAction(JToken token, CancellationToken cancellationToken) {
            using (var timer = _requestTimer.Time("textDocument/codeAction")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                var actions = await _server.CodeAction(ToObject<CodeActionParams>(token), cancellationToken);
                timer.AddMeasure("count", actions?.Length ?? 0);
                return actions;
            }
        }

        //[JsonRpcMethod("textDocument/codeLens")]
        //public async Task<CodeLens[]> CodeLens(JToken token, CancellationToken cancellationToken) {
        //    await _prioritizer.DefaultPriorityAsync(cancellationToken);
        //    return await _server.CodeLens(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        //}

        //[JsonRpcMethod("codeLens/resolve")]
        //public Task<CodeLens> CodeLensResolve(JToken token, CancellationToken cancellationToken)
        //   => _server.CodeLensResolve(ToObject<CodeLens>(token), cancellationToken);

        //[JsonRpcMethod("textDocument/documentLink")]
        //public async Task<DocumentLink[]> DocumentLink(JToken token, CancellationToken cancellationToken) {
        //    await _prioritizer.DefaultPriorityAsync(cancellationToken);
        //    return await _server.DocumentLink(ToObject<DocumentLinkParams>(token), cancellationToken);
        //}

        //[JsonRpcMethod("documentLink/resolve")]
        //public Task<DocumentLink> DocumentLinkResolve(JToken token, CancellationToken cancellationToken)
        //   => _server.DocumentLinkResolve(ToObject<DocumentLink>(token), cancellationToken);

        //[JsonRpcMethod("textDocument/formatting")]
        //public async Task<TextEdit[]> DocumentFormatting(JToken token, CancellationToken cancellationToken) {
        //    await _prioritizer.DefaultPriorityAsync(cancellationToken);
        //    return await _server.DocumentFormatting(ToObject<DocumentFormattingParams>(token), cancellationToken);
        //}

        //[JsonRpcMethod("textDocument/rangeFormatting")]
        //public async Task<TextEdit[]> DocumentRangeFormatting(JToken token, CancellationToken cancellationToken) {
        //    await _prioritizer.DefaultPriorityAsync(cancellationToken);
        //    return await _server.DocumentRangeFormatting(ToObject<DocumentRangeFormattingParams>(token), cancellationToken);
        //}

        [JsonRpcMethod("textDocument/onTypeFormatting")]
        public async Task<TextEdit[]> DocumentOnTypeFormatting(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentOnTypeFormatting(ToObject<DocumentOnTypeFormattingParams>(token), GetToken(cancellationToken));
        }

        [JsonRpcMethod("textDocument/rename")]
        public async Task<WorkspaceEdit> Rename(JToken token, CancellationToken cancellationToken) {
            using (var timer = _requestTimer.Time("textDocument/rename")) {
                await _prioritizer.DefaultPriorityAsync(cancellationToken);
                return await _server.Rename(ToObject<RenameParams>(token), GetToken(cancellationToken));
            }
        }
        #endregion

        #region Extensions

        [JsonRpcMethod("python/loadExtension")]
        public async Task LoadExtension(JToken token, CancellationToken cancellationToken) {
            using (var timer = _requestTimer.Time("python/loadExtension")) {
                await _server.LoadExtensionAsync(ToObject<PythonAnalysisExtensionParams>(token), _services, cancellationToken);
            }
        }

        [JsonRpcMethod("python/extensionCommand")]
        public async Task ExtensionCommand(JToken token, CancellationToken cancellationToken) {
            using (var timer = _requestTimer.Time("python/extensionCommand")) {
                await _server.ExtensionCommandAsync(ToObject<ExtensionCommandParams>(token), cancellationToken);
            }
        }
        #endregion

        #region Custom
        [JsonRpcMethod("python/clearAnalysisCache")]
        public async Task ClearAnalysisCache(CancellationToken cancellationToken) {
            using (_requestTimer.Time("python/clearAnalysisCache"))
            using (await _prioritizer.ConfigurationPriorityAsync(cancellationToken)) {
                _server.ClearAnalysisCache();
            }
        }
        #endregion

        private T ToObject<T>(JToken token) => token.ToObject<T>(_jsonSerializer);

        private T GetSetting<T>(JToken section, string settingName, T defaultValue) {
            var value = section?[settingName];
            try {
                return value != null ? value.ToObject<T>() : defaultValue;
            } catch (JsonException ex) {
                _logger?.Log(TraceEventType.Warning, $"Exception retrieving setting '{settingName}': {ex.Message}");
            }
            return defaultValue;
        }

        private MessageType GetLogLevel(JToken analysisKey) {
            var s = GetSetting(analysisKey, "logLevel", "Error");
            if (s.EqualsIgnoreCase("Warning")) {
                return MessageType.Warning;
            }
            if (s.EqualsIgnoreCase("Info") || s.EqualsIgnoreCase("Information")) {
                return MessageType.Info;
            }
            if (s.EqualsIgnoreCase("Trace")) {
                return MessageType.Log;
            }
            return MessageType.Error;
        }

        private static CancellationToken GetToken(CancellationToken original)
                => Debugger.IsAttached ? CancellationToken.None : original;

        private class Prioritizer : IDisposable {
            private const int InitializePriority = 0;
            private const int ConfigurationPriority = 1;
            private const int DocumentChangePriority = 2;
            private const int DefaultPriority = 3;
            private readonly PriorityProducerConsumer<QueueItem> _ppc;

            public Prioritizer() {
                _ppc = new PriorityProducerConsumer<QueueItem>(maxPriority: 4);
                Task.Run(ConsumerLoop).DoNotWait();
            }

            private async Task ConsumerLoop() {
                while (!_ppc.IsDisposed) {
                    try {
                        var item = await _ppc.ConsumeAsync();
                        if (item.IsAwaitable) {
                            var disposable = new PrioritizerDisposable(_ppc.CancellationToken);
                            item.SetResult(disposable);
                            await disposable.Task;
                        } else {
                            item.SetResult(EmptyDisposable.Instance);
                        }
                    } catch (OperationCanceledException) when (_ppc.IsDisposed) {
                        return;
                    }
                }
            }

            public Task<IDisposable> InitializePriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(InitializePriority, isAwaitable: true, cancellationToken);

            public Task<IDisposable> ConfigurationPriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(ConfigurationPriority, isAwaitable: true, cancellationToken);

            public Task<IDisposable> DocumentChangePriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(DocumentChangePriority, isAwaitable: true, cancellationToken);

            public Task DefaultPriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(DefaultPriority, isAwaitable: false, cancellationToken);

            private Task<IDisposable> Enqueue(int priority, bool isAwaitable, CancellationToken cancellationToken = default(CancellationToken)) {
                var item = new QueueItem(isAwaitable, cancellationToken);
                _ppc.Produce(item, priority);
                return item.Task;
            }

            private readonly struct QueueItem {
                private readonly TaskCompletionSource<IDisposable> _tcs;
                public Task<IDisposable> Task => _tcs.Task;
                public bool IsAwaitable { get; }

                public QueueItem(bool isAwaitable, CancellationToken cancellationToken) {
                    _tcs = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
                    IsAwaitable = isAwaitable;
                    _tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(_tcs.Task);
                }

                public void SetResult(IDisposable disposable) => _tcs.TrySetResult(disposable);
            }

            private class PrioritizerDisposable : IDisposable {
                private readonly TaskCompletionSource<int> _tcs;

                public PrioritizerDisposable(CancellationToken cancellationToken) {
                    _tcs = new TaskCompletionSource<int>();
                    _tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(_tcs.Task);
                }

                public Task Task => _tcs.Task;
                public void Dispose() => _tcs.TrySetResult(0);
            }

            public void Dispose() => _ppc.Dispose();
        }

        private class AnalysisOptionsProvider : IAnalysisOptionsProvider {
            public AnalysisOptions Options { get; } = new AnalysisOptions();
        }
    }
}
