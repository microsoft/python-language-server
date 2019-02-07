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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Shell;
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

        public static InformationDisplayOptions DisplayOptions { get; private set; } = new InformationDisplayOptions {
            preferredFormat = MarkupKind.PlainText,
            trimDocumentationLines = true,
            maxDocumentationLineLength = 100,
            trimDocumentationText = true,
            maxDocumentationTextLength = 1024,
            maxDocumentationLines = 100
        };

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

        public async Task<InitializeResult> InitializeAsync(InitializeParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();
            _clientCaps = @params.capabilities;
            _log = _services.GetService<ILogger>();

            if (@params.initializationOptions.displayOptions != null) {
                DisplayOptions = @params.initializationOptions.displayOptions;
            }

            _services.AddService(new DiagnosticsService(_services));

            var analyzer = new PythonAnalyzer(_services, @params.rootPath);
            _services.AddService(analyzer);

            _services.AddService(new RunningDocumentTable(@params.rootPath, _services));
            _rdt = _services.GetService<IRunningDocumentTable>();

            // TODO: multi-root workspaces.
            var rootDir = @params.rootUri != null ? @params.rootUri.ToAbsolutePath() : PathUtils.NormalizePath(@params.rootPath);
            var configuration = InterpreterConfiguration.FromDictionary(@params.initializationOptions.interpreter.properties);
            configuration.SearchPaths = @params.initializationOptions.searchPaths;
            configuration.TypeshedPath = @params.initializationOptions.typeStubSearchPaths.FirstOrDefault();

            _interpreter = await PythonInterpreter.CreateAsync(configuration, rootDir, _services, cancellationToken);
            _services.AddService(_interpreter);

            var symbolIndex = new SymbolIndex();
            var fileSystem = _services.GetService<IFileSystem>();
            _indexManager = new IndexManager(symbolIndex, fileSystem, _interpreter.LanguageVersion, rootDir,
                                            @params.initializationOptions.includeFiles,
                                            @params.initializationOptions.excludeFiles);
            _services.AddService(_indexManager);

            DisplayStartupInfo();

            var ds = new PlainTextDocumentationSource();
            _completionSource = new CompletionSource(ds, Settings.completion);
            _hoverSource = new HoverSource(ds);
            _signatureSource = new SignatureSource(ds);

            return GetInitializeResult();
        }

        public Task Shutdown() {
            _disposableBag.ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public async Task DidChangeConfiguration(DidChangeConfigurationParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();

            var reanalyze = true;
            if (@params.settings != null) {
                if (@params.settings is ServerSettings settings) {
                    reanalyze = HandleConfigurationChanges(settings);
                } else {
                    _log?.Log(TraceEventType.Error, "change configuration notification sent unsupported settings");
                    return;
                }
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

            _symbolHierarchyDepthLimit = Settings.analysis.symbolsHierarchyDepthLimit;
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
        #endregion
    }
}
