﻿// Python Tools for Visual Studio
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Documentation;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server : ServerBase, ILogger, IPythonLanguageServer, IDisposable {
        /// <summary>
        /// Implements ability to execute module reload on the analyzer thread
        /// </summary>
        private sealed class ReloadModulesQueueItem : IAnalyzable {
            private readonly PythonAnalyzer _analyzer;
            private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
            public Task Task => _tcs.Task;

            public ReloadModulesQueueItem(PythonAnalyzer analyzer) {
                _analyzer = analyzer;
            }
            public void Analyze(CancellationToken cancel) {
                if (cancel.IsCancellationRequested) {
                    return;
                }

                var currentTcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource<bool>());
                try {
                    _analyzer.ReloadModulesAsync(cancel).WaitAndUnwrapExceptions();
                    currentTcs.TrySetResult(true);
                } catch (OperationCanceledException oce) {
                    currentTcs.TrySetCanceled(oce.CancellationToken);
                } catch (Exception ex) {
                    currentTcs.TrySetException(ex);
                }
            }
        }

        private readonly Dictionary<IDocument, VolatileCounter> _pendingParse = new Dictionary<IDocument, VolatileCounter>();
        private readonly VolatileCounter _pendingAnalysisEnqueue = new VolatileCounter();
        private readonly ConcurrentDictionary<string, ILanguageServerExtension> _extensions = new ConcurrentDictionary<string, ILanguageServerExtension>();
        private readonly DisposableBag _disposableBag = DisposableBag.Create<Server>();
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

        private readonly EditorFiles _editorFiles;
        private ClientCapabilities _clientCaps;
        private bool _traceLogging;
        private bool _analysisUpdates;
        private ReloadModulesQueueItem _reloadModulesQueueItem;
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

        public Server() {
            AnalysisQueue = new AnalysisQueue();
            AnalysisQueue.UnhandledException += Analysis_UnhandledException;

            ParseQueue = new ParseQueue();
            _editorFiles = new EditorFiles(this);

            _disposableBag
                .Add(() => {
                    foreach (var ext in _extensions.Values) {
                        (ext as IDisposable)?.Dispose();
                    }
                    foreach (var ext in _oldExtensions.Values) {
                        (ext as IDisposable)?.Dispose();
                    }
                })
                .Add(ProjectFiles)
                .Add(() => Analyzer?.Dispose())
                .Add(AnalysisQueue)
                .Add(() => _shutdownCts.Cancel());
        }

        private void Analysis_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            Debug.Fail(e.ExceptionObject.ToString());
            LogMessage(MessageType.Error, e.ExceptionObject.ToString());
        }

        internal AnalysisQueue AnalysisQueue { get; }
        internal ParseQueue ParseQueue { get; }

        internal PythonAnalyzer Analyzer { get; private set; }
        internal ServerSettings Settings { get; private set; } = new ServerSettings();
        internal ProjectFiles ProjectFiles { get; } = new ProjectFiles();

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
                    moreTriggerCharacter = new[] { ";" }
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
                entry = await AddFileAsync(@params.textDocument.uri, null, cookie);
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
            LogMessage(MessageType._General, "Reloading modules...");

            // Make sure reload modules is executed on the analyzer thread.
            var task = _reloadModulesQueueItem.Task;
            AnalysisQueue.Enqueue(_reloadModulesQueueItem, AnalysisPriority.Normal);
            await task;

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var entry in Analyzer.ModulesByFilename) {
                AnalysisQueue.Enqueue(entry.Value.ProjectEntry, AnalysisPriority.Normal);
            }
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

        public Task<IProjectEntry> LoadFileAsync(Uri documentUri) => AddFileAsync(documentUri, null);
        public Task<IProjectEntry> LoadFileAsync(Uri documentUri, Uri fromSearchPath) => AddFileAsync(documentUri, fromSearchPath);

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

        public void SetSearchPaths(IEnumerable<string> searchPaths) => Analyzer.SetSearchPaths(searchPaths.MaybeEnumerate());
        public void SetTypeStubSearchPaths(IEnumerable<string> typeStubSearchPaths) => Analyzer.SetTypeStubPaths(typeStubSearchPaths.MaybeEnumerate());

        #endregion

        #region IPythonLanguageServer
        public ILogger Logger => this;

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

        private IProjectEntry RemoveEntry(Uri documentUri) {
            var entry = ProjectFiles.RemoveEntry(documentUri);
            _editorFiles.Remove(documentUri);
            return entry;
        }

        private async Task DoInitializeAsync(InitializeParams @params, CancellationToken token) {
            _disposableBag.ThrowIfDisposed();
            Analyzer = await AnalysisQueue.ExecuteInQueueAsync(ct => CreateAnalyzer(@params.initializationOptions.interpreter, token), AnalysisPriority.High);

            _disposableBag.ThrowIfDisposed();
            _clientCaps = @params.capabilities;
            _traceLogging = @params.initializationOptions.traceLogging;
            _analysisUpdates = @params.initializationOptions.analysisUpdates;

            Analyzer.EnableDiagnostics = _clientCaps?.python?.liveLinting ?? false;
            _reloadModulesQueueItem = new ReloadModulesQueueItem(Analyzer);

            if (@params.initializationOptions.displayOptions != null) {
                DisplayOptions = @params.initializationOptions.displayOptions;
            }
            _displayTextBuilder = DocumentationBuilder.Create(DisplayOptions);

            DisplayStartupInfo();

            if (@params.rootUri != null) {
                _rootDir = @params.rootUri.ToAbsolutePath();
            } else if (!string.IsNullOrEmpty(@params.rootPath)) {
                _rootDir = PathUtils.NormalizePath(@params.rootPath);
            }

            SetSearchPaths(@params.initializationOptions.searchPaths);
            SetTypeStubSearchPaths(@params.initializationOptions.typeStubSearchPaths);

            Analyzer.Interpreter.ModuleNamesChanged += Interpreter_ModuleNamesChanged;
        }

        private void DisplayStartupInfo() {
            LogMessage(MessageType._General, Resources.LanguageServerVersion.FormatInvariant(Assembly.GetExecutingAssembly().GetName().Version));
            LogMessage(MessageType._General,
                string.IsNullOrEmpty(Analyzer.InterpreterFactory?.Configuration?.InterpreterPath)
                ? Resources.InitializingForGenericInterpreter
                : Resources.InitializingForPythonInterpreter.FormatInvariant(Analyzer.InterpreterFactory.Configuration.InterpreterPath));
        }

        private void Interpreter_ModuleNamesChanged(object sender, EventArgs e) {
            Analyzer.Modules.ReInit();
            foreach (var entry in Analyzer.ModulesByFilename) {
                AnalysisQueue.Enqueue(entry.Value.ProjectEntry, AnalysisPriority.Normal);
            }
        }

        private T ActivateObject<T>(string assemblyName, string typeName, Dictionary<string, object> properties) {
            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(typeName)) {
                return default(T);
            }
            try {
                var assembly = File.Exists(assemblyName)
                    ? Assembly.LoadFrom(assemblyName)
                    : Assembly.Load(new AssemblyName(assemblyName));

                var type = assembly.GetType(typeName, true);

                return (T)Activator.CreateInstance(
                    type,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    properties == null ? Array.Empty<object>() : new object[] { properties },
                    CultureInfo.CurrentCulture
                );
            } catch (Exception ex) {
                LogMessage(MessageType.Warning, ex.ToString());
            }

            return default(T);
        }

        private async Task<PythonAnalyzer> CreateAnalyzer(PythonInitializationOptions.Interpreter interpreter, CancellationToken token) {
            var factory = ActivateObject<IPythonInterpreterFactory>(interpreter.assembly, interpreter.typeName, interpreter.properties)
                ?? new AstPythonInterpreterFactory(interpreter.properties);

            var interp = factory.CreateInterpreter();
            if (interp == null) {
                throw new InvalidOperationException(Resources.Error_FailedToCreateInterpreter);
            }

            LogMessage(MessageType.Info, $"Created {interp.GetType().FullName} instance from {factory.GetType().FullName}");

            var analyzer = await PythonAnalyzer.CreateAsync(factory, interp, token);
            return analyzer;
        }

        private IEnumerable<ModulePath> GetImportNames(Uri document) {
            var filePath = GetLocalPath(document);

            if (!string.IsNullOrEmpty(filePath)) {
                if (!string.IsNullOrEmpty(_rootDir) && ModulePath.FromBasePathAndFile_NoThrow(_rootDir, filePath, out var mp)) {
                    yield return mp;
                }

                foreach (var sp in Analyzer.GetSearchPaths()) {
                    if (ModulePath.FromBasePathAndFile_NoThrow(sp, filePath, out mp)) {
                        yield return mp;
                    }
                }
            }

            if (document.Scheme == "python") {
                var path = Path.Combine(document.Host, document.AbsolutePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar));
                if (ModulePath.FromBasePathAndFile_NoThrow("", path, p => true, out var mp, out _, out _)) {
                    yield return mp;
                }
            }
        }

        private string GetLocalPath(Uri uri) {
            if (uri == null) {
                return null;
            }
            if (uri.IsFile) {
                return uri.ToAbsolutePath();
            }
            var scheme = uri.Scheme;
            var path = uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var bits = new List<string>(path.Length + 2);
            bits.Add("_:"); // Non-existent root
            bits.Add(scheme.ToUpperInvariant());
            bits.AddRange(path);
            return string.Join(Path.DirectorySeparatorChar.ToString(), bits);
        }

        private async Task<IProjectEntry> AddFileAsync(Uri documentUri, Uri fromSearchPath, IAnalysisCookie cookie = null) {
            var item = ProjectFiles.GetEntry(documentUri, throwIfMissing: false);
            if (item != null) {
                return item;
            }

            string[] aliases = null;
            var path = GetLocalPath(documentUri);
            if (fromSearchPath != null) {
                if (ModulePath.FromBasePathAndFile_NoThrow(GetLocalPath(fromSearchPath), path, out var mp)) {
                    aliases = new[] { mp.ModuleName };
                }
            } else {
                aliases = GetImportNames(documentUri).Select(mp => mp.ModuleName).ToArray();
            }

            if (aliases.IsNullOrEmpty()) {
                aliases = new[] { Path.GetFileNameWithoutExtension(path) };
            }

            var reanalyzeEntries = aliases.SelectMany(a => Analyzer.GetEntriesThatImportModule(a, true)).ToArray();
            var first = aliases.FirstOrDefault();

            var pyItem = Analyzer.AddModule(first, path, documentUri, cookie);
            item = pyItem;
            foreach (var a in aliases.Skip(1)) {
                Analyzer.AddModuleAlias(first, a);
            }

            var actualItem = ProjectFiles.GetOrAddEntry(documentUri, item);
            if (actualItem != item) {
                return actualItem;
            }

            pyItem.NewAnalysis += OnProjectEntryNewAnalysis;

            if (item is IDocument doc) {
                await EnqueueItemAsync(doc);
            }

            foreach (var entryRef in reanalyzeEntries) {
                await EnqueueItemAsync(entryRef as IDocument, AnalysisPriority.Low, parse: false);
            }

            return item;
        }

        private void OnProjectEntryNewAnalysis(object sender, EventArgs e) {
            // Events are fired from a background thread and may come
            // after items were actually already disposed.
            if (_disposableBag.IsDisposed) {
                return;
            }

            var pythonProjectEntry = (IPythonProjectEntry)sender;
            TraceMessage($"Received new analysis for {pythonProjectEntry.DocumentUri}");

            var version = 0;
            var parse = pythonProjectEntry.GetCurrentParse();
            if (_analysisUpdates) {
                if (parse?.Cookie is VersionCookie vc && vc.Versions.Count > 0) {
                    foreach (var kv in vc.GetAllParts(pythonProjectEntry.DocumentUri)) {
                        AnalysisComplete(kv.Key, kv.Value.Version);
                        if (kv.Value.Version > version) {
                            version = kv.Value.Version;
                        }
                    }
                } else {
                    AnalysisComplete(pythonProjectEntry.DocumentUri, 0);
                }
            }

            _editorFiles.GetDocument(pythonProjectEntry.DocumentUri).UpdateAnalysisDiagnostics(pythonProjectEntry, version);
        }

        private void RemoveDocumentParseCounter(Task t, IDocument doc, VolatileCounter counter) {
            if (t.IsCompleted) {
                lock (_pendingParse) {
                    if (counter.IsZero) {
                        if (_pendingParse.TryGetValue(doc, out var existing) && existing == counter) {
                            _pendingParse.Remove(doc);
                        }
                        return;
                    }
                }
            }
            counter.WaitForChangeToZeroAsync().ContinueWith(t2 => RemoveDocumentParseCounter(t, doc, counter));
        }

        private IDisposable GetDocumentParseCounter(IDocument doc, out int count) {
            lock (_pendingParse) {
                if (!_pendingParse.TryGetValue(doc, out var counter)) {
                    _pendingParse[doc] = counter = new VolatileCounter();
                    // Automatically remove counter from the dictionary when it reaches zero.
                    counter.WaitForChangeToZeroAsync().ContinueWith(t => RemoveDocumentParseCounter(t, doc, counter));
                }
                var res = counter.Incremented();
                count = counter.Count;
                return res;
            }
        }

        internal Task EnqueueItemAsync(IDocument doc, AnalysisPriority priority = AnalysisPriority.Normal, bool parse = true, bool enqueueForAnalysis = true) {
            _disposableBag.ThrowIfDisposed();
            var pending = _pendingAnalysisEnqueue.Incremented();
            try {
                var entry = doc as ProjectEntry;
                entry?.ResetCompleteAnalysis();

                // If we don't need to parse, use null cookie
                var cookieTask = Task.FromResult<IAnalysisCookie>(null);
                if (parse) {
                    using (GetDocumentParseCounter(doc, out var count)) {
                        if (count > 3) {
                            // Rough check to prevent unbounded queueing. If we have
                            // multiple parses in queue, we will get the latest doc
                            // version in one of the ones to come.
                            return Task.CompletedTask;
                        }
                        TraceMessage($"Parsing document {doc.DocumentUri}");
                        cookieTask = ParseQueue.EnqueueAsync(doc, Analyzer.LanguageVersion);
                    }
                }

                // The call must be fire and forget, but should not be yielding.
                // It is called from DidChangeTextDocument which must fully finish
                // since otherwise Complete() may come before the change is enqueued
                // for processing and the completion list will be driven off the stale data.
                return cookieTask.ContinueWith(t => {
                    if (t.IsFaulted) {
                        // Happens when file got deleted before processing 
                        pending.Dispose();
                        LogMessage(MessageType.Error, t.Exception.Message);
                        return;
                    }
                    OnDocumentChangeProcessingComplete(doc, t.Result as VersionCookie, enqueueForAnalysis, priority, pending);
                });
            } catch {
                pending?.Dispose();
                throw;
            }
        }

        private void OnDocumentChangeProcessingComplete(IDocument doc, VersionCookie vc, bool enqueueForAnalysis, AnalysisPriority priority, IDisposable disposeWhenEnqueued) {
            try {
                _shutdownCts.Token.ThrowIfCancellationRequested();
                if (vc != null) {
                    foreach (var kv in vc.GetAllParts(doc.DocumentUri)) {
                        ParseComplete(kv.Key, kv.Value.Version);
                    }
                } else {
                    ParseComplete(doc.DocumentUri, 0);
                }

                if (doc is IAnalyzable analyzable && enqueueForAnalysis && !_shutdownCts.Token.IsCancellationRequested) {
                    AnalysisQueued(doc.DocumentUri);
                    AnalysisQueue.Enqueue(analyzable, priority);
                }

                disposeWhenEnqueued?.Dispose();
                disposeWhenEnqueued = null;
                if (vc != null) {
                    _editorFiles.GetDocument(doc.DocumentUri).UpdateParseDiagnostics(vc, doc.DocumentUri);
                }
            } catch (BadSourceException) {
            } catch (OperationCanceledException ex) {
                LogMessage(MessageType.Warning, $"Parsing {doc.DocumentUri} cancelled");
                TraceMessage($"{ex}");
            } catch (Exception ex) {
                LogMessage(MessageType.Error, ex.ToString());
            } finally {
                disposeWhenEnqueued?.Dispose();
            }
        }

        private PythonAst GetParseTree(IPythonProjectEntry entry, Uri documentUri, CancellationToken token, out BufferVersion bufferVersion) {
            PythonAst tree = null;
            bufferVersion = null;
            var parse = entry.WaitForCurrentParse(Timeout.Infinite, token);
            if (parse != null) {
                tree = parse.Tree;
                if (parse.Cookie is VersionCookie vc) {
                    if (vc.Versions.TryGetValue(ProjectFiles.GetPart(documentUri), out bufferVersion)) {
                        tree = bufferVersion.Ast ?? tree;
                    }
                }
            }
            return tree;
        }

        private bool HandleConfigurationChanges(ServerSettings newSettings) {
            var oldSettings = Settings;
            Settings = newSettings;

            _symbolHierarchyDepthLimit = Settings.analysis.symbolsHierarchyDepthLimit;

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

        internal Task WaitForCompleteAnalysisAsync(CancellationToken cancellationToken)
            => Task.WhenAny(WaitForCompleteAnalysisWorker(cancellationToken), Task.Delay(Timeout.Infinite, cancellationToken)).Unwrap();

        private async Task WaitForCompleteAnalysisWorker(CancellationToken cancellationToken) {
            // Wait for all current parsing to complete
            TraceMessage($"Waiting for parsing to complete.");
            await ParseQueue.WaitForAllAsync();
            TraceMessage($"Parsing complete. Waiting for analysis entries to enqueue.");
            await _pendingAnalysisEnqueue.WaitForZeroAsync();
            TraceMessage($"Enqueue complete. Waiting for analysis to complete.");
            await AnalysisQueue.WaitForCompleteAsync();
            foreach (var pf in ProjectFiles) {
                TraceMessage($" Waiting for analysis of {pf.DocumentUri} to complete.");
                await GetAnalysisAsync(pf.DocumentUri, cancellationToken);
            }
            TraceMessage($"Analysis complete.");
        }
    }
}
