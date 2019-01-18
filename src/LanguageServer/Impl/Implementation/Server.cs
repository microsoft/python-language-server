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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.IO;
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server : IPythonLanguageServer, IDisposable {
        private readonly ConcurrentDictionary<string, ILanguageServerExtension> _extensions = new ConcurrentDictionary<string, ILanguageServerExtension>();
        private readonly DisposableBag _disposableBag = DisposableBag.Create<Server>();
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

        private ClientCapabilities _clientCaps;
        private bool _traceLogging;
        private bool _analysisUpdates;
        // If null, all files must be added manually
        private string _rootDir;

        public static InformationDisplayOptions DisplayOptions { get; private set; } = new InformationDisplayOptions {
            preferredFormat = MarkupKind.PlainText,
            trimDocumentationLines = true,
            maxDocumentationLineLength = 100,
            trimDocumentationText = true,
            maxDocumentationTextLength = 1024,
            maxDocumentationLines = 100
        };

        public Server(IServiceContainer services = null): base(services) {
            _disposableBag
                .Add(() => {
                    foreach (var ext in _extensions.Values) {
                        ext.Dispose();
                    }
                })
                .Add(() => Analyzer?.Dispose())
                .Add(() => _shutdownCts.Cancel());
        }

        internal ServerSettings Settings { get; private set; } = new ServerSettings();

        public void Dispose()  => _disposableBag.TryDispose();

        #region ILogger
        public void TraceMessage(IFormattable message) {
            if (_traceLogging) {
                LogMessage(MessageType.Log, message.ToString());
            }
        }
        #endregion

        #region Client message handling
        internal InitializeResult GetInitializeResult() => new InitializeResult {
            capabilities = new ServerCapabilities {
                textDocumentSync = new TextDocumentSyncOptions {
                    openClose = true,
                    change = TextDocumentSyncKind.Incremental
                },
                completionProvider = new CompletionOptions {
                    triggerCharacters = new[] { "." }
                },
                hoverProvider = true,
                signatureHelpProvider = new SignatureHelpOptions { triggerCharacters = new[] { "(,)" } },
                definitionProvider = true,
                referencesProvider = true,
                workspaceSymbolProvider = true,
                documentSymbolProvider = true,
                renameProvider = true,
                documentOnTypeFormattingProvider = new DocumentOnTypeFormattingOptions {
                    firstTriggerCharacter = "\n",
                    moreTriggerCharacter = new[] { ";", ":" }
                },
            }
        };

        public override async Task<InitializeResult> Initialize(InitializeParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();
            await DoInitializeAsync(@params, cancellationToken);
            return GetInitializeResult();
        }

        public override Task Shutdown() {
            _disposableBag.ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public override async Task DidOpenTextDocument(DidOpenTextDocumentParams @params, CancellationToken token) {
            _disposableBag.ThrowIfDisposed();
            TraceMessage($"Opening document {@params.textDocument.uri}");

            _editorFiles.Open(@params.textDocument.uri);
            var entry = ProjectFiles.GetEntry(@params.textDocument.uri, throwIfMissing: false);
            var doc = entry as IDocument;
            if (doc != null) {
                if (@params.textDocument.text != null) {
                    doc.ResetDocument(@params.textDocument.version, @params.textDocument.text);
                }
                await EnqueueItemAsync(doc);
            } else if (entry == null) {
                IAnalysisCookie cookie = null;
                if (@params.textDocument.text != null) {
                    cookie = new InitialContentCookie {
                        Content = @params.textDocument.text,
                        Version = @params.textDocument.version
                    };
                }
                entry = await AddFileAsync(@params.textDocument.uri, cookie);
            }
        }

        public override Task DidChangeTextDocument(DidChangeTextDocumentParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();
            var openedFile = _editorFiles.GetDocument(@params.textDocument.uri);
            return openedFile.DidChangeTextDocument(@params, true, cancellationToken);
        }

        public override async Task DidChangeWatchedFiles(DidChangeWatchedFilesParams @params, CancellationToken token) {
            foreach (var c in @params.changes.MaybeEnumerate()) {
                _disposableBag.ThrowIfDisposed();
                IProjectEntry entry;
                switch (c.type) {
                    case FileChangeType.Created:
                        entry = await LoadFileAsync(c.uri);
                        if (entry != null) {
                            TraceMessage($"Saw {c.uri} created and loaded new entry");
                        } else {
                            LogMessage(MessageType.Warning, $"Failed to load {c.uri}");
                        }
                        break;
                    case FileChangeType.Deleted:
                        await UnloadFileAsync(c.uri);
                        break;
                    case FileChangeType.Changed:
                        if ((entry = ProjectFiles.GetEntry(c.uri, false)) is IDocument doc) {
                            // If document version is >=0, it is loaded in memory.
                            if (doc.GetDocumentVersion(0) < 0) {
                                await EnqueueItemAsync(doc, AnalysisPriority.Low);
                            }
                        }
                        break;
                }
            }
        }

        public override async Task DidCloseTextDocument(DidCloseTextDocumentParams @params, CancellationToken token) {
            _disposableBag.ThrowIfDisposed();
            _editorFiles.Close(@params.textDocument.uri);

            if (ProjectFiles.GetEntry(@params.textDocument.uri) is IDocument doc) {
                // No need to keep in-memory buffers now
                doc.ResetDocument(-1, null);
                // Pick up any changes on disk that we didn't know about
                await EnqueueItemAsync(doc, AnalysisPriority.Low);
            }
        }

        public override async Task DidChangeConfiguration(DidChangeConfigurationParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();

            if (Analyzer == null) {
                LogMessage(MessageType.Error, "Change configuration notification sent to uninitialized server");
                return;
            }

            var reanalyze = true;
            if (@params.settings != null) {
                if (@params.settings is ServerSettings settings) {
                    reanalyze = HandleConfigurationChanges(settings);
                } else {
                    LogMessage(MessageType.Error, "change configuration notification sent unsupported settings");
                    return;
                }
            }

            if (reanalyze) {
                await ReloadModulesAsync(cancellationToken);
            }
        }

        public async Task ReloadModulesAsync(CancellationToken token) {
            LogMessage(MessageType._General, Resources.ReloadingModules);

            // Make sure reload modules is executed on the analyzer thread.
            var task = _reloadModulesQueueItem.Task;
            AnalysisQueue.Enqueue(_reloadModulesQueueItem, AnalysisPriority.Normal);
            await task;

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var entry in Analyzer.ModulesByFilename) {
                AnalysisQueue.Enqueue(entry.Value.ProjectEntry, AnalysisPriority.Normal);
            }
            LogMessage(MessageType._General, Resources.Done);
        }

        public override Task<object> ExecuteCommand(ExecuteCommandParams @params, CancellationToken token) {
            _disposableBag.ThrowIfDisposed();
            Command(new CommandEventArgs {
                command = @params.command,
                arguments = @params.arguments
            });
            return Task.FromResult((object)null);
        }
        #endregion

        #region Non-LSP public API
        public IProjectEntry GetEntry(TextDocumentIdentifier document) => ProjectFiles.GetEntry(document.uri);
        public IProjectEntry GetEntry(Uri documentUri, bool throwIfMissing = true) => ProjectFiles.GetEntry(documentUri, throwIfMissing);

        public int GetPart(TextDocumentIdentifier document) => ProjectFiles.GetPart(document.uri);
        public IEnumerable<string> GetLoadedFiles() => ProjectFiles.GetLoadedFiles();

        public Task<IProjectEntry> LoadFileAsync(Uri documentUri) => AddFileAsync(documentUri);

        public Task<bool> UnloadFileAsync(Uri documentUri) {
            var entry = RemoveEntry(documentUri);
            if (entry != null) {
                Analyzer.RemoveModule(entry, e => EnqueueItemAsync(e as IDocument, AnalysisPriority.Normal, parse: false).DoNotWait());
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public int EstimateRemainingWork() {
            return ParseQueue.Count + AnalysisQueue.Count;
        }

        public event EventHandler<ParseCompleteEventArgs> OnParseComplete;
        private void ParseComplete(Uri uri, int version) {
            TraceMessage($"Parse complete for {uri} at version {version}");
            OnParseComplete?.Invoke(this, new ParseCompleteEventArgs { uri = uri, version = version });
        }

        public event EventHandler<AnalysisCompleteEventArgs> OnAnalysisComplete;
        private void AnalysisComplete(Uri uri, int version) {
            if (_analysisUpdates) {
                TraceMessage($"Analysis complete for {uri} at version {version}");
                OnAnalysisComplete?.Invoke(this, new AnalysisCompleteEventArgs { uri = uri, version = version });
            }
        }

        public event EventHandler<AnalysisQueuedEventArgs> OnAnalysisQueued;
        private void AnalysisQueued(Uri uri) {
            if (_analysisUpdates) {
                TraceMessage($"Analysis queued for {uri}");
                OnAnalysisQueued?.Invoke(this, new AnalysisQueuedEventArgs { uri = uri });
            }
        }

        #endregion

        #region IPythonLanguageServer
        public PythonAst GetCurrentAst(Uri documentUri) {
            ProjectFiles.GetEntry(documentUri, null, out var entry, out var tree);
            return entry.GetCurrentParse()?.Tree;
        }

        public Task<PythonAst> GetAstAsync(Uri documentUri, CancellationToken token) {
            ProjectFiles.GetEntry(documentUri, null, out var entry, out var tree);
            entry.WaitForCurrentParse(Timeout.Infinite, token);
            return Task.FromResult(entry.GetCurrentParse()?.Tree);
        }

        public Task<IModuleAnalysis> GetAnalysisAsync(Uri documentUri, CancellationToken token) {
            ProjectFiles.GetEntry(documentUri, null, out var entry, out var tree);
            return entry.GetAnalysisAsync(Timeout.Infinite, token);
        }

        public IProjectEntry GetProjectEntry(Uri documentUri) => ProjectFiles.GetEntry(documentUri);
        #endregion

        #region Private Helpers

        private async Task DoInitializeAsync(InitializeParams @params, CancellationToken token) {
            _disposableBag.ThrowIfDisposed();

            _disposableBag.ThrowIfDisposed();
            _clientCaps = @params.capabilities;
            _traceLogging = @params.initializationOptions.traceLogging;
            _analysisUpdates = @params.initializationOptions.analysisUpdates;

            if (@params.initializationOptions.displayOptions != null) {
                DisplayOptions = @params.initializationOptions.displayOptions;
            }

            DisplayStartupInfo();

            if (@params.rootUri != null) {
                _rootDir = @params.rootUri.ToAbsolutePath();
            } else if (!string.IsNullOrEmpty(@params.rootPath)) {
                _rootDir = PathUtils.NormalizePath(@params.rootPath);
            }
        }

        private void DisplayStartupInfo() {
            LogMessage(MessageType._General, Resources.LanguageServerVersion.FormatInvariant(Assembly.GetExecutingAssembly().GetName().Version));
            LogMessage(MessageType._General,
                string.IsNullOrEmpty(Analyzer.InterpreterFactory?.Configuration?.InterpreterPath)
                ? Resources.InitializingForGenericInterpreter
                : Resources.InitializingForPythonInterpreter.FormatInvariant(Analyzer.InterpreterFactory.Configuration.InterpreterPath));
        }

        private bool HandleConfigurationChanges(ServerSettings newSettings) {
            var oldSettings = Settings;
            Settings = newSettings;

            _symbolHierarchyDepthLimit = Settings.analysis.symbolsHierarchyDepthLimit;
            _symbolHierarchyMaxSymbols = Settings.analysis.symbolsHierarchyMaxSymbols;

            if (oldSettings == null) {
                return true;
            }

            if (newSettings.analysis.openFilesOnly != oldSettings.analysis.openFilesOnly) {
                _editorFiles.UpdateDiagnostics();
                return false;
            }

            if (!newSettings.analysis.errors.SetEquals(oldSettings.analysis.errors) ||
                !newSettings.analysis.warnings.SetEquals(oldSettings.analysis.warnings) ||
                !newSettings.analysis.information.SetEquals(oldSettings.analysis.information) ||
                !newSettings.analysis.disabled.SetEquals(oldSettings.analysis.disabled)) {
                _editorFiles.UpdateDiagnostics();
            }

            return false;
        }
        #endregion
    }
}
