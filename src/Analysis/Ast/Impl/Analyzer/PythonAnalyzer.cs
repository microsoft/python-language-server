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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Caching;
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

        public PythonAnalyzer(IServiceManager services, string cacheFolderPath = null) {
            _services = services;
            _log = services.GetService<ILogger>();
            _dependencyResolver = new DependencyResolver<AnalysisModuleKey, PythonAnalyzerEntry>();
            _analysisCompleteEvent.Set();
            _startNextSession = StartNextSession;

            _progress = new ProgressReporter(services.GetService<IProgressService>());
            _services.AddService(new AnalysisCache(_services, cacheFolderPath));
        }

        public void Dispose() {
            _progress.Dispose();
            _disposeToken.TryMarkDisposed();
        }

        public Task WaitForCompleteAnalysisAsync(CancellationToken cancellationToken = default) {
            return _analysisCompleteEvent.WaitAsync(cancellationToken);
        }

        public async Task<IDocumentAnalysis> GetAnalysisAsync(IPythonModule module, int waitTime, CancellationToken cancellationToken) {
            var key = new AnalysisModuleKey(module);
            PythonAnalyzerEntry entry;
            lock (_syncObj) {
                if (!_analysisEntries.TryGetValue(key, out entry)) {
                    var emptyAnalysis = new EmptyAnalysis(_services, (IDocument)module);
                    entry = new PythonAnalyzerEntry(emptyAnalysis);
                    _analysisEntries[key] = entry;
                }
            }

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
                var key = new AnalysisModuleKey(module);
                if (_analysisEntries.TryGetValue(key, out var entry)) {
                    entry.Invalidate(_version + 1);
                } else {
                    _analysisEntries[key] = new PythonAnalyzerEntry(new EmptyAnalysis(_services, (IDocument)module));
                    _analysisCompleteEvent.Reset();
                }
            }
        }

        public void RemoveAnalysis(IPythonModule module) {
            lock (_syncObj) {
                _analysisEntries.Remove(new AnalysisModuleKey(module));
            }
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
                AnalyzeDocument(key, entry, dependencies, default);
            }
        }

        public void EnqueueDocumentForAnalysis(IPythonModule module, PythonAst ast, int bufferVersion, CancellationToken cancellationToken) {
            var key = new AnalysisModuleKey(module);
            PythonAnalyzerEntry entry;
            int version;
            lock (_syncObj) {
                version = _version + 1;
                if (_analysisEntries.TryGetValue(key, out entry)) {
                    if (entry.BufferVersion >= bufferVersion) {
                        return;
                    }
                } else {
                    entry = new PythonAnalyzerEntry(new EmptyAnalysis(_services, (IDocument)module));
                    _analysisEntries[key] = entry;
                    _analysisCompleteEvent.Reset();
                }
            }

            if (entry.Invalidate(module, ast, bufferVersion, version, out var dependencies)) {
                AnalyzeDocument(key, entry, dependencies, cancellationToken);
            }
        }

        public IReadOnlyList<DiagnosticsEntry> LintModule(IPythonModule module) {
            if (module.ModuleType != ModuleType.User) {
                return Array.Empty<DiagnosticsEntry>();
            }

            // Linter always runs no matter of the option since it looks up variables
            // which also enumerates and updates variable references for find all
            // references and rename operations.
            var result = new LinterAggregator().Lint(module.Analysis, _services);

            var optionsProvider = _services.GetService<IAnalysisOptionsProvider>();
            return optionsProvider?.Options?.LintingEnabled == false ? Array.Empty<DiagnosticsEntry>() : result;
        }

        public void ResetAnalyzer() {
            lock (_syncObj) {
                _analysisEntries.Split(kvp => kvp.Key.IsTypeshed || kvp.Value.Module is IBuiltinsPythonModule, out var entriesToPreserve, out var entriesToRemove);
                _analysisEntries.Clear();
                foreach (var (key, entry) in entriesToPreserve) {
                    _analysisEntries.Add(key, entry);
                }

                _dependencyResolver.RemoveKeys(entriesToRemove.Select(e => e.Key));
            }
        }

        public IReadOnlyList<IPythonModule> LoadedModules 
            => _analysisEntries.Values.ExcludeDefault().Select(v => v.Module).ExcludeDefault().ToArray();

        private void AnalyzeDocument(AnalysisModuleKey key, PythonAnalyzerEntry entry, ImmutableArray<AnalysisModuleKey> dependencies, CancellationToken cancellationToken) {
            _analysisCompleteEvent.Reset();
            _log?.Log(TraceEventType.Verbose, $"Analysis of {entry.Module.Name}({entry.Module.ModuleType}) queued");

            var snapshot = _dependencyResolver.NotifyChanges(key, entry, dependencies);
            
            lock (_syncObj) {
                if (_version > snapshot.Version) {
                    return;
                }

                _version = snapshot.Version;
            }

            if (snapshot.MissingKeys.Count > 0) {
                LoadMissingDocuments(entry.Module.Interpreter, snapshot.MissingKeys);
            }

            if (TryCreateSession(snapshot, entry, cancellationToken, out var session)) {
                session.Start(true);
            }
        }

        private bool TryCreateSession(DependencyGraphSnapshot<AnalysisModuleKey, PythonAnalyzerEntry> snapshot, PythonAnalyzerEntry entry, CancellationToken cancellationToken, out PythonAnalyzerSession session) {
            lock (_syncObj) {
                if (_currentSession != null) {
                    if (_currentSession.Version > snapshot.Version || _nextSession != null && _nextSession.Version > snapshot.Version) {
                        session = null;
                        return false;
                    }

                    if (_version > snapshot.Version && !_currentSession.IsCompleted && entry.IsUserModule) {
                        session = CreateSession(null, entry, cancellationToken);
                        return true;
                    }
                }
            }

            if (!_dependencyResolver.TryCreateWalker(snapshot.Version, out var walker)) {
                session = null;
                return false;
            }

            lock (_syncObj) {
                if (_currentSession == null) {
                    _currentSession = session = CreateSession(walker, null, cancellationToken);
                    return true;
                }

                if (_version > walker.Version && (!_currentSession.IsCompleted || walker.AffectedValues.GetCount(e => e.NotAnalyzed) < _maxTaskRunning)) {
                    session = null;
                    return false;
                }
                
                if (_currentSession.IsCompleted) {
                    _currentSession = session = CreateSession(walker, null, cancellationToken);
                    return true;
                }

                _currentSession.Cancel();
                _nextSession = session = CreateSession(walker, entry.IsUserModule ? entry : null, cancellationToken);
                return entry.IsUserModule;
            }
        }

        private void StartNextSession(Task task) {
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

        private PythonAnalyzerSession CreateSession(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, PythonAnalyzerEntry entry, CancellationToken cancellationToken) 
            => new PythonAnalyzerSession(_services, _progress, _analysisCompleteEvent, _startNextSession, _disposeToken.CancellationToken, cancellationToken, walker, _version, entry);

        private void LoadMissingDocuments(IPythonInterpreter interpreter, ImmutableArray<AnalysisModuleKey> missingKeys) {
            foreach (var (moduleName, _, isTypeshed) in missingKeys) {
                var moduleResolution = isTypeshed ? interpreter.TypeshedResolution : interpreter.ModuleResolution;
                moduleResolution.GetOrLoadModule(moduleName);
            }
        }
    }
}
