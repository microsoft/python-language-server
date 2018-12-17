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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Shell;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Core.Threading;
using Microsoft.Python.LanguageServer.Diagnostics;
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
        private readonly object _lock = new object();
        private readonly Prioritizer _prioritizer = new Prioritizer();
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

        private IServiceContainer _services;
        private IUIService _ui;
        private ITelemetryService2 _telemetry;

        private JsonRpc _rpc;
        private JsonSerializer _jsonSerializer;
        private bool _filesLoaded;
        private PathsWatcher _pathsWatcher;
        private IIdleTimeTracker _idleTimeTracker;
        private DiagnosticsPublisher _diagnosticsPublisher;

        private bool _watchSearchPaths;
        private string[] _searchPaths = Array.Empty<string>();

        public CancellationToken Start(IServiceContainer services, JsonRpc rpc) {
            _server = new Server(services);
            _services = services;
            _rpc = rpc;

            _jsonSerializer = services.GetService<JsonSerializer>();
            _telemetry = services.GetService<ITelemetryService2>();

            var progress = services.GetService<IProgressService>();

            var rpcTraceListener = new TelemetryRpcTraceListener(_telemetry);
            _rpc.TraceSource.Listeners.Add(rpcTraceListener);

            _diagnosticsPublisher = new DiagnosticsPublisher(_server, services);
            _server.OnApplyWorkspaceEdit += OnApplyWorkspaceEdit;
            _server.OnRegisterCapability += OnRegisterCapability;
            _server.OnUnregisterCapability += OnUnregisterCapability;
            _server.AnalysisQueue.UnhandledException += OnAnalysisQueueUnhandledException;

            _disposables
                .Add(() => _server.OnApplyWorkspaceEdit -= OnApplyWorkspaceEdit)
                .Add(() => _server.OnRegisterCapability -= OnRegisterCapability)
                .Add(() => _server.OnUnregisterCapability -= OnUnregisterCapability)
                .Add(() => _server.AnalysisQueue.UnhandledException -= OnAnalysisQueueUnhandledException)
                .Add(() => _shutdownCts.Cancel())
                .Add(_prioritizer)
                .Add(() => _pathsWatcher?.Dispose())
                .Add(() => _rpc.TraceSource.Listeners.Remove(rpcTraceListener))
                .Add(_diagnosticsPublisher);

            return _sessionTokenSource.Token;
        }

        public void Dispose() {
            _disposables.TryDispose();
            _server.Dispose();
        }

        #region Events
        private void OnApplyWorkspaceEdit(object sender, ApplyWorkspaceEditEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("workspace/applyEdit", e.@params).DoNotWait();
        private void OnRegisterCapability(object sender, RegisterCapabilityEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("client/registerCapability", e.@params).DoNotWait();
        private void OnUnregisterCapability(object sender, UnregisterCapabilityEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("client/unregisterCapability", e.@params).DoNotWait();
        #endregion

        #region Workspace
        [JsonRpcMethod("workspace/didChangeConfiguration")]
        public async Task DidChangeConfiguration(JToken token, CancellationToken cancellationToken) {
            using (await _prioritizer.ConfigurationPriorityAsync(cancellationToken)) {
                var settings = new LanguageServerSettings();

                var rootSection = token["settings"];
                var pythonSection = rootSection?["python"];
                if (pythonSection == null) {
                    return;
                }

                var autoComplete = pythonSection["autoComplete"];
                settings.completion.showAdvancedMembers = GetSetting(autoComplete, "showAdvancedMembers", true);
                settings.completion.addBrackets = GetSetting(autoComplete, "addBrackets", false);

                var analysis = pythonSection["analysis"];
                settings.analysis.openFilesOnly = GetSetting(analysis, "openFilesOnly", false);
                settings.diagnosticPublishDelay = GetSetting(analysis, "diagnosticPublishDelay", 1000);
                settings.symbolsHierarchyDepthLimit = GetSetting(analysis, "symbolsHierarchyDepthLimit", 10);
                settings.symbolsHierarchyMaxSymbols = GetSetting(analysis, "symbolsHierarchyMaxSymbols", 1000);

                _logger.LogLevel = GetLogLevel(analysis).ToTraceEventType();
                _diagnosticsPublisher.PublishingDelay = settings.diagnosticPublishDelay;

                HandlePathWatchChange(token);

                var errors = GetSetting(analysis, "errors", Array.Empty<string>());
                var warnings = GetSetting(analysis, "warnings", Array.Empty<string>());
                var information = GetSetting(analysis, "information", Array.Empty<string>());
                var disabled = GetSetting(analysis, "disabled", Array.Empty<string>());
                settings.analysis.SetErrorSeverityOptions(errors, warnings, information, disabled);

                await _server.DidChangeConfiguration(new DidChangeConfigurationParams { settings = settings }, cancellationToken);

                if (!_filesLoaded) {
                    await LoadDirectoryFiles();
                }
                _filesLoaded = true;
            }
        }

        [JsonRpcMethod("workspace/didChangeWatchedFiles")]
        public async Task DidChangeWatchedFiles(JToken token, CancellationToken cancellationToken) {
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                await _server.DidChangeWatchedFiles(ToObject<DidChangeWatchedFilesParams>(token), cancellationToken);
            }
        }

        [JsonRpcMethod("workspace/symbol")]
        public async Task<SymbolInformation[]> WorkspaceSymbols(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.WorkspaceSymbols(ToObject<WorkspaceSymbolParams>(token), cancellationToken);
        }

        #endregion

        #region Commands
        [JsonRpcMethod("workspace/executeCommand")]
        public Task<object> ExecuteCommand(JToken token, CancellationToken cancellationToken)
           => _server.ExecuteCommand(ToObject<ExecuteCommandParams>(token), cancellationToken);
        #endregion

        #region TextDocument
        [JsonRpcMethod("textDocument/didOpen")]
        public async Task DidOpenTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                await _server.DidOpenTextDocument(ToObject<DidOpenTextDocumentParams>(token), cancellationToken);
            }
        }

        [JsonRpcMethod("textDocument/didChange")]
        public async Task DidChangeTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync()) {
                var @params = ToObject<DidChangeTextDocumentParams>(token);
                var version = @params.textDocument.version;
                if (version == null || @params.contentChanges.IsNullOrEmpty()) {
                    await _server.DidChangeTextDocument(@params, cancellationToken);
                    return;
                }

                // _server.DidChangeTextDocument can handle change buckets with decreasing version and without overlaping 
                // Split change into buckets that will be properly handled
                var changes = SplitDidChangeTextDocumentParams(@params, version.Value);

                foreach (var change in changes) {
                    await _server.DidChangeTextDocument(change, cancellationToken);
                }
            }
        }

        private static IEnumerable<DidChangeTextDocumentParams> SplitDidChangeTextDocumentParams(DidChangeTextDocumentParams @params, int version) {
            var changes = new Stack<DidChangeTextDocumentParams>();
            var contentChanges = new Stack<TextDocumentContentChangedEvent>();
            var previousRange = new Range();

            for (var i = @params.contentChanges.Length - 1; i >= 0; i--) {
                var contentChange = @params.contentChanges[i];
                var range = contentChange.range.GetValueOrDefault();
                if (previousRange.end > range.start) {
                    changes.Push(CreateDidChangeTextDocumentParams(@params, version, contentChanges));
                    contentChanges = new Stack<TextDocumentContentChangedEvent>();
                    version--;
                }

                contentChanges.Push(contentChange);
                previousRange = range;
            }

            if (contentChanges.Count > 0) {
                changes.Push(CreateDidChangeTextDocumentParams(@params, version, contentChanges));
            }

            return changes;
        }

        private static DidChangeTextDocumentParams CreateDidChangeTextDocumentParams(DidChangeTextDocumentParams @params, int version, Stack<TextDocumentContentChangedEvent> contentChanges)
            => new DidChangeTextDocumentParams {
                _enqueueForAnalysis = @params._enqueueForAnalysis,
                contentChanges = contentChanges.ToArray(),
                textDocument = new VersionedTextDocumentIdentifier {
                    uri = @params.textDocument.uri,
                    version = version
                }
            };

        [JsonRpcMethod("textDocument/willSave")]
        public Task WillSaveTextDocument(JToken token, CancellationToken cancellationToken)
           => _server.WillSaveTextDocument(ToObject<WillSaveTextDocumentParams>(token), cancellationToken);

        public Task<TextEdit[]> WillSaveWaitUntilTextDocument(JToken token, CancellationToken cancellationToken)
           => _server.WillSaveWaitUntilTextDocument(ToObject<WillSaveTextDocumentParams>(token), cancellationToken);

        [JsonRpcMethod("textDocument/didSave")]
        public async Task DidSaveTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                await _server.DidSaveTextDocument(ToObject<DidSaveTextDocumentParams>(token), cancellationToken);
            }
        }

        [JsonRpcMethod("textDocument/didClose")]
        public async Task DidCloseTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                await _server.DidCloseTextDocument(ToObject<DidCloseTextDocumentParams>(token), cancellationToken);
            }
        }
        #endregion

        #region Editor features
        [JsonRpcMethod("textDocument/completion")]
        public async Task<CompletionList> Completion(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.Completion(ToObject<CompletionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("completionItem/resolve")]
        public Task<CompletionItem> CompletionItemResolve(JToken token, CancellationToken cancellationToken)
           => _server.CompletionItemResolve(ToObject<CompletionItem>(token), cancellationToken);

        [JsonRpcMethod("textDocument/hover")]
        public async Task<Hover> Hover(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.Hover(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/signatureHelp")]
        public async Task<SignatureHelp> SignatureHelp(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.SignatureHelp(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/definition")]
        public async Task<Reference[]> GotoDefinition(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.GotoDefinition(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/references")]
        public async Task<Reference[]> FindReferences(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.FindReferences(ToObject<ReferencesParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/documentHighlight")]
        public async Task<DocumentHighlight[]> DocumentHighlight(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentHighlight(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/documentSymbol")]
        public async Task<DocumentSymbol[]> DocumentSymbol(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            // This call is also used by VSC document outline and it needs correct information
            return await _server.HierarchicalDocumentSymbol(ToObject<DocumentSymbolParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/codeAction")]
        public async Task<Command[]> CodeAction(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.CodeAction(ToObject<CodeActionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/codeLens")]
        public async Task<CodeLens[]> CodeLens(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.CodeLens(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("codeLens/resolve")]
        public Task<CodeLens> CodeLensResolve(JToken token, CancellationToken cancellationToken)
           => _server.CodeLensResolve(ToObject<CodeLens>(token), cancellationToken);

        [JsonRpcMethod("textDocument/documentLink")]
        public async Task<DocumentLink[]> DocumentLink(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentLink(ToObject<DocumentLinkParams>(token), cancellationToken);
        }

        [JsonRpcMethod("documentLink/resolve")]
        public Task<DocumentLink> DocumentLinkResolve(JToken token, CancellationToken cancellationToken)
           => _server.DocumentLinkResolve(ToObject<DocumentLink>(token), cancellationToken);

        [JsonRpcMethod("textDocument/formatting")]
        public async Task<TextEdit[]> DocumentFormatting(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentFormatting(ToObject<DocumentFormattingParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/rangeFormatting")]
        public async Task<TextEdit[]> DocumentRangeFormatting(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentRangeFormatting(ToObject<DocumentRangeFormattingParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/onTypeFormatting")]
        public async Task<TextEdit[]> DocumentOnTypeFormatting(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentOnTypeFormatting(ToObject<DocumentOnTypeFormattingParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/rename")]
        public async Task<WorkspaceEdit> Rename(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.Rename(ToObject<RenameParams>(token), cancellationToken);
        }
        #endregion

        #region Extensions
        [JsonRpcMethod("python/loadExtension")]
        public Task LoadExtension(JToken token, CancellationToken cancellationToken)
            => _server.LoadExtensionAsync(ToObject<PythonAnalysisExtensionParams>(token), _services, cancellationToken);

        #endregion

        private T ToObject<T>(JToken token) => token.ToObject<T>(_jsonSerializer);

        private T GetSetting<T>(JToken section, string settingName, T defaultValue) {
            var value = section?[settingName];
            try {
                return value != null ? value.ToObject<T>() : defaultValue;
            } catch (JsonException ex) {
                _server.LogMessage(MessageType.Warning, $"Exception retrieving setting '{settingName}': {ex.Message}");
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

        private void HandlePathWatchChange(JToken section) {
            var watchSearchPaths = GetSetting(section, "watchSearchPaths", true);
            if (!watchSearchPaths) {
                // No longer watching.
                _pathsWatcher?.Dispose();
                _searchPaths = Array.Empty<string>();
                _watchSearchPaths = false;
                return;
            }

            // Now watching.
            if (!_watchSearchPaths || (_watchSearchPaths && _searchPaths.SetEquals(_initParams.initializationOptions.searchPaths))) {
                // Were not watching OR were watching but paths have changed. Recreate the watcher.
                _pathsWatcher?.Dispose();
                _pathsWatcher = new PathsWatcher(
                    _initParams.initializationOptions.searchPaths,
                    () => _server.ReloadModulesAsync(CancellationToken.None).DoNotWait(),
                    _services.GetService<ILogger>()
                 );
            }

            _watchSearchPaths = watchSearchPaths;
            _searchPaths = _initParams.initializationOptions.searchPaths;
        }

        private void OnAnalysisQueueUnhandledException(object sender, UnhandledExceptionEventArgs e) {
            if (!(e.ExceptionObject is Exception ex)) {
                Debug.Fail($"ExceptionObject was {e.ExceptionObject.GetType()}, not Exception");
                return;
            }

            var te = Telemetry.CreateEventWithException("analysis_queue.unhandled_exception", ex);
            _telemetry.SendTelemetry(te).DoNotWait();
        }

        private class Prioritizer : IDisposable {
            private const int InitializePriority = 0;
            private const int ConfigurationPriority = 1;
            private const int DocumentChangePriority = 2;
            private const int DefaultPriority = 3;
            private readonly PriorityProducerConsumer<QueueItem> _ppc;

            public Prioritizer() {
                _ppc = new PriorityProducerConsumer<QueueItem>(4);
                Task.Run(ConsumerLoop);
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
                => Enqueue(InitializePriority, true, cancellationToken);

            public Task<IDisposable> ConfigurationPriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(ConfigurationPriority, true, cancellationToken);

            public Task<IDisposable> DocumentChangePriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(DocumentChangePriority, true, cancellationToken);

            public Task DefaultPriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(DefaultPriority, false, cancellationToken);

            private Task<IDisposable> Enqueue(int priority, bool isAwaitable, CancellationToken cancellationToken = default(CancellationToken)) {
                var item = new QueueItem(isAwaitable, cancellationToken);
                _ppc.Produce(item, priority);
                return item.Task;
            }

            private struct QueueItem {
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
    }
}
