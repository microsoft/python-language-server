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
using Microsoft.Python.Core.Collections;
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
        private ILogger _log;
        private IIndexManager _indexManager;
        private string _rootDir;

        private InitializeParams _initParams;

        private bool _watchSearchPaths;
        private PathsWatcher _pathsWatcher;
        private string[] _searchPaths;

        public Server(IServiceManager services) {
            _services = services;

            _disposableBag
                .Add(() => {
                    foreach (var ext in _extensions.Values) {
                        ext.Dispose();
                    }
                })
                .Add(() => _pathsWatcher?.Dispose())
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
                declarationProvider = true,
                documentOnTypeFormattingProvider = new DocumentOnTypeFormattingOptions {
                    firstTriggerCharacter = "\n",
                    moreTriggerCharacter = new[] { ";", ":" }
                },
            }
        };

        public Task<InitializeResult> InitializeAsync(InitializeParams @params, CancellationToken cancellationToken = default) {
            _disposableBag.ThrowIfDisposed();
            _initParams = @params;
            _log = _services.GetService<ILogger>();

            _log?.Log(TraceEventType.Information, Resources.LanguageServerVersion.FormatInvariant(Assembly.GetExecutingAssembly().GetName().Version));

            return Task.FromResult(GetInitializeResult());
        }

        public async Task InitializedAsync(InitializedParams @params, CancellationToken cancellationToken = default, IReadOnlyList<string> userConfiguredPaths = null) {
            var initializationOptions = _initParams?.initializationOptions;

            _services.AddService(new DiagnosticsService(_services));

            var cacheFolderPath = initializationOptions?.cacheFolderPath;
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

            _rootDir = _initParams?.rootUri != null ? _initParams.rootUri.ToAbsolutePath() : _initParams?.rootPath;
            if (_rootDir != null) {
                _rootDir = PathUtils.NormalizePathAndTrim(_rootDir);
            }

            Version.TryParse(initializationOptions?.interpreter.properties?.Version, out var version);

            if (initializationOptions?.enableAnalysisCache != false) {
                _log?.Log(TraceEventType.Information, Resources.AnalysisCacheEnabled);
                _services.AddService(new ModuleDatabase(_services));
            } else {
                _log?.Log(TraceEventType.Information, Resources.AnalysisCacheDisabled);
            }

            var configuration = new InterpreterConfiguration(
                interpreterPath: initializationOptions?.interpreter.properties?.InterpreterPath,
                version: version
            );

            var typeshedPath = initializationOptions?.typeStubSearchPaths.FirstOrDefault();
            userConfiguredPaths = userConfiguredPaths ?? initializationOptions?.searchPaths;
            _interpreter = await PythonInterpreter.CreateAsync(configuration, _rootDir, _services, typeshedPath, userConfiguredPaths.ToImmutableArray(), cancellationToken);
            
            _interpreter = await PythonInterpreter.CreateAsync(configuration, _rootDir, _services, typeshedPath, userConfiguredPaths.ToImmutableArray(), cancellationToken);
            _services.AddService(_interpreter);

            _log?.Log(TraceEventType.Information,
                string.IsNullOrEmpty(_interpreter.Configuration.InterpreterPath)
                ? Resources.InitializingForGenericInterpreter
                : Resources.InitializingForPythonInterpreter.FormatInvariant(_interpreter.Configuration.InterpreterPath));

            var fileSystem = _services.GetService<IFileSystem>();
            _indexManager = new IndexManager(fileSystem, _interpreter.LanguageVersion, _rootDir,
                                            initializationOptions?.includeFiles,
                                            initializationOptions?.excludeFiles,
                                            _services.GetService<IIdleTimeService>());
            _indexManager.IndexWorkspace().DoNotWait();
            _services.AddService(_indexManager);
            _disposableBag.Add(_indexManager);

            var textDocCaps = _initParams?.capabilities?.textDocument;

            _completionSource = new CompletionSource(
                ChooseDocumentationSource(textDocCaps?.completion?.completionItem?.documentationFormat),
                Settings.completion
            );

            _hoverSource = new HoverSource(
                ChooseDocumentationSource(textDocCaps?.hover?.contentFormat)
            );

            var sigInfo = textDocCaps?.signatureHelp?.signatureInformation;
            _signatureSource = new SignatureSource(
                ChooseDocumentationSource(sigInfo?.documentationFormat),
                sigInfo?.parameterInformation?.labelOffsetSupport == true
            );
        }

        public Task Shutdown() {
            _disposableBag.ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public void DidChangeConfiguration(DidChangeConfigurationParams @params, CancellationToken cancellationToken) {
            _disposableBag.ThrowIfDisposed();
            switch (@params.settings) {
                case ServerSettings settings: {
                        Settings = settings;
                        _symbolHierarchyMaxSymbols = Settings.analysis.symbolsHierarchyMaxSymbols;
                        _completionSource.Options = Settings.completion;
                        break;
                    }
                default:
                    _log?.Log(TraceEventType.Error, "change configuration notification sent unsupported settings");
                    break;
            }
        }
        #endregion

        public void HandleWatchPathsChange(bool watchSearchPaths) {
            if (watchSearchPaths == _watchSearchPaths) {
                return;
            }

            _watchSearchPaths = watchSearchPaths;

            if (!_watchSearchPaths) {
                _searchPaths = null;
                _pathsWatcher?.Dispose();
                _pathsWatcher = null;
                return;
            }

            ResetPathWatcher();
        }

        public void HandleUserConfiguredPathsChange(ImmutableArray<string> paths) {
            var changed = _interpreter.ModuleResolution.SetUserConfiguredPaths(paths);
            if (changed) {
                ResetAnalyzer();
            }
        }

        #region Private Helpers
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

        private void ResetPathWatcher() {
            var paths = _interpreter.ModuleResolution.InterpreterPaths.ToArray();

            if (_searchPaths == null || !_searchPaths.SequenceEqual(paths)) {
                _searchPaths = paths;
                _pathsWatcher?.Dispose();
                _pathsWatcher = new PathsWatcher(_searchPaths, ResetAnalyzer, _log);
            }
        }

        private void ResetAnalyzer() {
            _log?.Log(TraceEventType.Information, Resources.ReloadingModules);
            _services.GetService<PythonAnalyzer>().ResetAnalyzer().ContinueWith(t => {
                if (_watchSearchPaths) {
                    ResetPathWatcher();
                }

                _log?.Log(TraceEventType.Information, Resources.Done);
                _log?.Log(TraceEventType.Information, Resources.AnalysisRestarted);
            }).DoNotWait();
        }
        #endregion
    }
}
