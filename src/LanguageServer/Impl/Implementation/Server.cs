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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Diagnostics;
using Microsoft.Python.LanguageServer.Indexing;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server : IDisposable {
        private readonly DisposableBag _disposableBag = DisposableBag.Create<Server>();
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();
        private readonly IServiceManager _services;

        private IPythonInterpreter _interpreter;
        private IRunningDocumentTable _rdt;
        private ClientCapabilities _clientCaps;
        private ILogger _log;
        private IIndexManager _indexManager;
        private string _rootDir;

        public Server(IServiceManager services) {
            _services = services;

            _disposableBag
                .Add(() => {
                    foreach (var ext in _extensions.Values) {
                        ext.Dispose();
                    }
                })
                .Add(() => _shutdownCts.Cancel());
        }

        internal ServerSettings Settings { get; private set; } = new ServerSettings();

        public IServiceContainer Services => _services;
        public void Dispose() => _disposableBag.TryDispose();

        #region Client message handling
        private InitializeResult GetInitializeResult() => new InitializeResult {
            capabilities = new ServerCapabilities {
                textDocumentSync = new TextDocumentSyncOptions {
                    openClose = true,
                    change = TextDocumentSyncKind.Incremental
                },
                completionProvider = new CompletionOptions {
                    triggerCharacters = new[] { "." }
                },
                hoverProvider = true,
                signatureHelpProvider = new SignatureHelpOptions { triggerCharacters = new[] { "(", ",", ")" } },
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

        public async Task<InitializeResult> InitializeAsync(InitializeParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();
            _clientCaps = @params.capabilities;
            _log = _services.GetService<ILogger>();

            _services.AddService(new DiagnosticsService(_services));

            var cacheFolderPath = @params.initializationOptions.cacheFolderPath;
            var fs = _services.GetService<IFileSystem>();
            if (cacheFolderPath != null && !fs.DirectoryExists(cacheFolderPath)) {
                _log?.Log(TraceEventType.Warning, Resources.Error_InvalidCachePath);
                cacheFolderPath = null;
            }

            var analyzer = new PythonAnalyzer(_services, cacheFolderPath);
            _services.AddService(analyzer);

            analyzer.AnalysisComplete += OnAnalysisComplete;
            _disposableBag.Add(() => analyzer.AnalysisComplete -= OnAnalysisComplete);

            _services.AddService(new RunningDocumentTable(_services));
            _rdt = _services.GetService<IRunningDocumentTable>();

            _rootDir = @params.rootUri != null ? @params.rootUri.ToAbsolutePath() : @params.rootPath;
            if (_rootDir != null) {
                _rootDir = PathUtils.NormalizePath(_rootDir);
                _rootDir = PathUtils.TrimEndSeparator(_rootDir);
            }

            Version.TryParse(@params.initializationOptions.interpreter.properties?.Version, out var version);

            var configuration = new InterpreterConfiguration(null, null,
                interpreterPath: @params.initializationOptions.interpreter.properties?.InterpreterPath,
                version: version
            ) {
                // 1) Split on ';' to support older VS Code extension versions which send paths as a single entry separated by ';'. TODO: Eventually remove.
                // 2) Normalize paths.
                // 3) If a path isn't rooted, then root it relative to the workspace root. If _rootDir is null, then accept the path as-is.
                // 4) Trim off any ending separator for a consistent style.
                // 5) Filter out any entries which are the same as the workspace root; they are redundant. Also ignore "/" to work around the extension (for now).
                // 6) Remove duplicates.
                SearchPaths = @params.initializationOptions.searchPaths
                    .Select(p => p.Split(';', StringSplitOptions.RemoveEmptyEntries)).SelectMany()
                    .Select(PathUtils.NormalizePath)
                    .Select(p => _rootDir == null || Path.IsPathRooted(p) ? p : Path.GetFullPath(p, _rootDir))
                    .Select(PathUtils.TrimEndSeparator)
                    .Where(p => !string.IsNullOrWhiteSpace(p) && p != "/" && !p.PathEquals(_rootDir))
                    .Distinct(PathEqualityComparer.Instance)
                    .ToList(),
                TypeshedPath = @params.initializationOptions.typeStubSearchPaths.FirstOrDefault()
            };

            if (@params.initializationOptions.enableAnalysCache != false) {
                _services.AddService(new ModuleDatabase(_services));
            }

            _interpreter = await PythonInterpreter.CreateAsync(configuration, _rootDir, _services, cancellationToken);
            _services.AddService(_interpreter);

            var fileSystem = _services.GetService<IFileSystem>();
            _indexManager = new IndexManager(fileSystem, _interpreter.LanguageVersion, _rootDir,
                                            @params.initializationOptions.includeFiles,
                                            @params.initializationOptions.excludeFiles,
                                            _services.GetService<IIdleTimeService>());
            _indexManager.IndexWorkspace().DoNotWait();
            _services.AddService(_indexManager);
            _disposableBag.Add(_indexManager);

            DisplayStartupInfo();

            _completionSource = new CompletionSource(
                ChooseDocumentationSource(_clientCaps?.textDocument?.completion?.completionItem?.documentationFormat),
                Settings.completion
            );

            _hoverSource = new HoverSource(
                ChooseDocumentationSource(_clientCaps?.textDocument?.hover?.contentFormat)
            );

            var sigInfo = _clientCaps?.textDocument?.signatureHelp?.signatureInformation;
            _signatureSource = new SignatureSource(
                ChooseDocumentationSource(sigInfo?.documentationFormat),
                sigInfo?.parameterInformation?.labelOffsetSupport == true
            );

            return GetInitializeResult();
        }

        public Task Shutdown() {
            _disposableBag.ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public void DidChangeConfiguration(DidChangeConfigurationParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();
            switch (@params.settings) {
                case ServerSettings settings: {
                    if (HandleConfigurationChanges(settings)) {
                        RestartAnalysis();
                    }
                    break;
                }
                default:
                    _log?.Log(TraceEventType.Error, "change configuration notification sent unsupported settings");
                    break;
            }
        }
        #endregion

        #region Private Helpers
        private void DisplayStartupInfo() {
            _log?.Log(TraceEventType.Information, Resources.LanguageServerVersion.FormatInvariant(Assembly.GetExecutingAssembly().GetName().Version));
            _log?.Log(TraceEventType.Information,
                string.IsNullOrEmpty(_interpreter.Configuration.InterpreterPath)
                ? Resources.InitializingForGenericInterpreter
                : Resources.InitializingForPythonInterpreter.FormatInvariant(_interpreter.Configuration.InterpreterPath));
        }

        private bool HandleConfigurationChanges(ServerSettings newSettings) {
            var oldSettings = Settings;
            Settings = newSettings;

            _symbolHierarchyMaxSymbols = Settings.analysis.symbolsHierarchyMaxSymbols;

            if (oldSettings == null) {
                return true;
            }

            if (!newSettings.analysis.errors.SetEquals(oldSettings.analysis.errors) ||
                !newSettings.analysis.warnings.SetEquals(oldSettings.analysis.warnings) ||
                !newSettings.analysis.information.SetEquals(oldSettings.analysis.information) ||
                !newSettings.analysis.disabled.SetEquals(oldSettings.analysis.disabled)) {
                return true;
            }

            return false;
        }

        private IDocumentationSource ChooseDocumentationSource(string[] kinds) {
            if (kinds == null) {
                return new PlainTextDocumentationSource();
            }

            foreach (var k in kinds) {
                switch (k) {
                    case MarkupKind.Markdown:
                        return new MarkdownDocumentationSource();
                    case MarkupKind.PlainText:
                        return new PlainTextDocumentationSource();
                }
            }

            return new PlainTextDocumentationSource();
        }
        #endregion

        public void NotifyPackagesChanged(CancellationToken cancellationToken) {
            var interpreter = _services.GetService<IPythonInterpreter>();
            _log?.Log(TraceEventType.Information, Resources.ReloadingModules);
            // No need to reload typeshed resolution since it is a static storage.
            // User does can add stubs while application is running, but it is
            // by design at this time that the app should be restarted.
            interpreter.ModuleResolution.ReloadAsync(cancellationToken).ContinueWith(t => {
                _log?.Log(TraceEventType.Information, Resources.Done);
                _log?.Log(TraceEventType.Information, Resources.AnalysisRestarted);
                RestartAnalysis();
            }, cancellationToken).DoNotWait();

        }

        private void RestartAnalysis() {
            var analyzer = Services.GetService<IPythonAnalyzer>();;
            analyzer.ResetAnalyzer();
            foreach (var doc in _rdt.GetDocuments()) {
                doc.Reset(null);
            }
        }
    }
}
