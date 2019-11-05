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
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Linting;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public sealed class PythonAnalyzer : IPythonAnalyzer, IDisposable {
        private readonly IServiceManager _services;
        private readonly IDependencyResolver<AnalysisModuleKey, PythonAnalyzerEntry> _dependencyResolver;
        private readonly Dictionary<AnalysisModuleKey, PythonAnalyzerEntry> _analysisEntries = new Dictionary<AnalysisModuleKey, PythonAnalyzerEntry>();
        private readonly DisposeToken _disposeToken = DisposeToken.Create<PythonAnalyzer>();
        private readonly object _syncObj = new object();
        private readonly AsyncManualResetEvent _analysisCompleteEvent = new AsyncManualResetEvent();
        private readonly Action<Task> _startNextSession;
        private readonly ProgressReporter _progress;
        private readonly ILogger _log;
        private readonly int _maxTaskRunning = Environment.ProcessorCount;
        private int _version;
        private PythonAnalyzerSession _currentSession;
        private PythonAnalyzerSession _nextSession;
        private bool _forceGCOnNextSession;

        public PythonAnalyzer(IServiceManager services, string cacheFolderPath = null) {
            _services = services;
            _log = services.GetService<ILogger>();
            _dependencyResolver = new DependencyResolver<AnalysisModuleKey, PythonAnalyzerEntry>();
            _analysisCompleteEvent.Set();
            _startNextSession = StartNextSession;

            _progress = new ProgressReporter(services.GetService<IProgressService>());

            _services.AddService(new CacheFolderService(_services, cacheFolderPath));
            _services.AddService(new StubCache(_services));
        }

        public void Dispose() {
            _progress.Dispose();
            _disposeToken.TryMarkDisposed();
        }

        public Task WaitForCompleteAnalysisAsync(CancellationToken cancellationToken = default)
            => _analysisCompleteEvent.WaitAsync(cancellationToken);

        public async Task<IDocumentAnalysis> GetAnalysisAsync(IPythonModule module, int waitTime, CancellationToken cancellationToken) {
            var entry = GetOrCreateAnalysisEntry(module, out _);

            if (waitTime < 0 || Debugger.IsAttached) {
                return await GetAnalysisAsync(entry, default, cancellationToken);
            }

            using (var timeoutCts = new CancellationTokenSource(waitTime))
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken)) {
                cts.CancelAfter(waitTime);
                var timeoutToken = timeoutCts.Token;
                return await GetAnalysisAsync(entry, timeoutToken, cts.Token);
            }
        }

        private async Task<IDocumentAnalysis> GetAnalysisAsync(PythonAnalyzerEntry entry, CancellationToken timeoutCt, CancellationToken cancellationToken) {
            while (!timeoutCt.IsCancellationRequested) {
                try {
                    var analysis = await entry.GetAnalysisAsync(cancellationToken);
                    lock (_syncObj) {
                        if (entry.AnalysisVersion == analysis.Version) {
                            return analysis;
                        }
                    }
                } catch (OperationCanceledException) when (timeoutCt.IsCancellationRequested) {
                    return entry.PreviousAnalysis;
                }
            }

            return entry.PreviousAnalysis;
        }

        public void InvalidateAnalysis(IPythonModule module) {
            lock (_syncObj) {
                var entry = GetOrCreateAnalysisEntry(module, out _);
                entry.Invalidate(_version + 1);
            }
        }

        public void RemoveAnalysis(IPythonModule module) {
            AnalysisModuleKey key;
            lock (_syncObj) {
                key = new AnalysisModuleKey(module);
                _analysisEntries.Remove(key);
            }
            _dependencyResolver.Remove(key);
        }

        public void EnqueueDocumentForAnalysis(IPythonModule module, ImmutableArray<IPythonModule> analysisDependencies) {
            var key = new AnalysisModuleKey(module);
            PythonAnalyzerEntry entry;
            int version;
            lock (_syncObj) {
                if (!_analysisEntries.TryGetValue(key, out entry)) {
                    return;
                }
                version = _version + 1;
            }

            if (entry.Invalidate(analysisDependencies, version, out var dependencies)) {
                AnalyzeDocument(key, entry, dependencies);
            }
        }

        public void EnqueueDocumentForAnalysis(IPythonModule module, PythonAst ast, int bufferVersion) {
            PythonAnalyzerEntry entry;
            AnalysisModuleKey key;
            int version;

            lock (_syncObj) {
                entry = GetOrCreateAnalysisEntry(module, out key);
                version = _version + 1;
                if (entry.BufferVersion >= bufferVersion) {
                    return;
                }
                // It is possible that parsing request for the library has been started when document is open,
                // but it is closed at the moment of analysis and then become open again.
                // In this case, we still need to analyze the document, but using correct entry.
                var nonUserAsDocumentKey = key.GetNonUserAsDocumentKey();
                if (entry.PreviousAnalysis is LibraryAnalysis && _analysisEntries.TryGetValue(nonUserAsDocumentKey, out var documentEntry)) {
                    key = nonUserAsDocumentKey;
                    entry = documentEntry;
                }
            }

            if (entry.Invalidate(module, ast, bufferVersion, version, out var dependencies)) {
                AnalyzeDocument(key, entry, dependencies);
            }
        }

        public IReadOnlyList<DiagnosticsEntry> LintModule(IPythonModule module) {
            if (module.ModuleType != ModuleType.User) {
                return Array.Empty<DiagnosticsEntry>();
            }

            // Linter always runs no matter of the option since it looks up variables
            // which also enumerates and updates variable references for find all
            // references and rename operations.
            var result = new LinterAggregator().Lint(module, _services);

            var optionsProvider = _services.GetService<IAnalysisOptionsProvider>();
            return optionsProvider?.Options?.LintingEnabled == false ? Array.Empty<DiagnosticsEntry>() : result;
        }

        public async Task ResetAnalyzer() {
            var interpreter = _services.GetService<IPythonInterpreter>();
            var builtins = interpreter.ModuleResolution.BuiltinsModule;
            builtins.SetAst(builtins.Analysis.Ast);

            await interpreter.TypeshedResolution.ReloadAsync();
            await interpreter.ModuleResolution.ReloadAsync();

            lock (_syncObj) {
                _forceGCOnNextSession = true;

                _analysisEntries.Split(kvp => kvp.Value.Module is IBuiltinsPythonModule, out var entriesToPreserve, out _);
                _analysisEntries.Clear();
                foreach (var (key, entry) in entriesToPreserve) {
                    _analysisEntries.Add(key, entry);
                }

                _dependencyResolver.Reset();
            }

            _services.GetService<IRunningDocumentTable>().ReloadAll();
        }

        public IReadOnlyList<IPythonModule> LoadedModules {
            get {
                lock (_syncObj) {
                    return _analysisEntries.Values.ExcludeDefault().Select(v => v.Module).ExcludeDefault().ToArray();
                }
            }
        }

        public event EventHandler<AnalysisCompleteEventArgs> AnalysisComplete;

        internal void RaiseAnalysisComplete(int moduleCount, double msElapsed) {
            _analysisCompleteEvent.Set();
            AnalysisComplete?.Invoke(this, new AnalysisCompleteEventArgs(moduleCount, msElapsed));
        }

        private void AnalyzeDocument(in AnalysisModuleKey key, in PythonAnalyzerEntry entry, in ImmutableArray<AnalysisModuleKey> dependencies) {
            _analysisCompleteEvent.Reset();
            ActivityTracker.StartTracking();
            _log?.Log(TraceEventType.Verbose, $"Analysis of {entry.Module.Name} ({entry.Module.ModuleType}) queued. Dependencies: {string.Join(", ", dependencies.Select(d => d.IsTypeshed ? $"{d.Name} (stub)" : d.Name))}");

            var graphVersion = _dependencyResolver.ChangeValue(key, entry, entry.IsUserOrBuiltin || key.IsNonUserAsDocument, dependencies);

            lock (_syncObj) {
                if (_version > graphVersion) {
                    return;
                }

                _version = graphVersion;
                _currentSession?.Cancel();
            }

            if (TryCreateSession(graphVersion, entry, out var session)) {
                session.Start(true);
            }
        }

        private bool TryCreateSession(in int graphVersion, in PythonAnalyzerEntry entry, out PythonAnalyzerSession session) {
            var analyzeUserModuleOutOfOrder = false;
            lock (_syncObj) {
                if (_currentSession != null) {
                    if (_currentSession.Version > graphVersion || _nextSession != null && _nextSession.Version > graphVersion) {
                        session = null;
                        return false;
                    }

                    analyzeUserModuleOutOfOrder = !_currentSession.IsCompleted && entry.IsUserModule && _currentSession.AffectedEntriesCount >= _maxTaskRunning;
                    if (_version > graphVersion && analyzeUserModuleOutOfOrder) {
                        session = CreateSession(null, entry);
                        return true;
                    }
                }
            }

            if (!_dependencyResolver.TryCreateWalker(graphVersion, 2, out var walker)) {
                session = null;
                return false;
            }

            LoadMissingDocuments(entry.Module.Interpreter, walker.MissingKeys);

            lock (_syncObj) {
                if (_currentSession == null) {
                    _currentSession = session = CreateSession(walker, null);
                    return true;
                }

                if (_version > walker.Version && (!_currentSession.IsCompleted || walker.AffectedValues.GetCount(e => e.NotAnalyzed) < _maxTaskRunning)) {
                    session = null;
                    return false;
                }

                if (_currentSession.IsCompleted) {
                    _currentSession = session = CreateSession(walker, null);
                    return true;
                }

                _nextSession = session = CreateSession(walker, analyzeUserModuleOutOfOrder ? entry : null);
                return analyzeUserModuleOutOfOrder;
            }
        }

        private void StartNextSession(Task task) {
            if (task.IsFaulted && task.Exception != null) {
                var exception = task.Exception.InnerException;
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            PythonAnalyzerSession session;
            lock (_syncObj) {
                if (_nextSession == null) {
                    return;
                }

                _currentSession = session = _nextSession;
                _nextSession = null;
            }

            session.Start(false);
        }

        private PythonAnalyzerSession CreateSession(in IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, in PythonAnalyzerEntry entry) {
            bool forceGC;
            lock (_syncObj) {
                forceGC = _forceGCOnNextSession;
                _forceGCOnNextSession = false;
            }

            return new PythonAnalyzerSession(_services, _progress, _startNextSession, _disposeToken.CancellationToken, walker, _version, entry, forceGC: forceGC);
        }

        private void LoadMissingDocuments(IPythonInterpreter interpreter, ImmutableArray<AnalysisModuleKey> missingKeys) {
            if (missingKeys.Count == 0) {
                return;
            }

            foreach (var missingKey in missingKeys) {
                lock (_syncObj) {
                    if (_analysisEntries.TryGetValue(missingKey, out _)) {
                        continue;
                    }
                }

                var (moduleName, _, isTypeshed) = missingKey;
                var moduleResolution = isTypeshed ? interpreter.TypeshedResolution : interpreter.ModuleResolution;

                var module = moduleResolution.GetOrLoadModule(moduleName);
                if (module != null && module.ModuleType != ModuleType.Unresolved) {
                    var entry = GetOrCreateAnalysisEntry(module, out _);
                    _dependencyResolver.TryAddValue(missingKey,
                        entry,
                        entry.IsUserModule,
                        module.ModuleType == ModuleType.Specialized,
                        ImmutableArray<AnalysisModuleKey>.Empty);
                }
            }
        }

        private PythonAnalyzerEntry GetOrCreateAnalysisEntry(IPythonModule module, out AnalysisModuleKey key) {
            key = new AnalysisModuleKey(module);
            return GetOrCreateAnalysisEntry(module, key);
        }

        private PythonAnalyzerEntry GetOrCreateAnalysisEntry(IPythonModule module, AnalysisModuleKey key) {
            lock (_syncObj) {
                if (!_analysisEntries.TryGetValue(key, out var entry)) {
                    var emptyAnalysis = new EmptyAnalysis(_services, (IDocument)module);
                    entry = new PythonAnalyzerEntry(emptyAnalysis);
                    _analysisEntries[key] = entry;
                    _analysisCompleteEvent.Reset();
                }
                return entry;
            }
        }
    }
}
