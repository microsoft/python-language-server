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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class PythonAnalyzerSession {
        private readonly int _maxTaskRunning = Environment.ProcessorCount;
        private readonly object _syncObj = new object();
        private readonly Action<Task> _startNextSession;

        private readonly CancellationTokenSource _cts;
        private readonly IServiceManager _services;
        private readonly AsyncManualResetEvent _analysisCompleteEvent;
        private readonly IDiagnosticsService _diagnosticsService;
        private readonly IProgressReporter _progress;
        private readonly IPythonAnalyzer _analyzer;
        private readonly ILogger _log;

        private State _state = State.NotStarted;
        private IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> _walker;
        private int _runningTasks;
        private int _version;
        private PythonAnalyzerSession _nextSession;

        public bool IsCompleted {
            get {
                lock (_syncObj) {
                    return _state == State.Completed;
                }
            }
        }

        public int Version => _version;

        public PythonAnalyzerSession(IServiceManager services,
            IProgressReporter progress,
            AsyncManualResetEvent analysisCompleteEvent,
            CancellationToken analyzerToken,
            CancellationToken sessionToken, 
            IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker,
            int version) {

            _services = services;
            _analysisCompleteEvent = analysisCompleteEvent;
            _startNextSession = StartNextSession;
            _version = version;
            _walker = walker;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(analyzerToken, sessionToken);

            _diagnosticsService = _services.GetService<IDiagnosticsService>();
            _progress = progress;
            _analyzer = _services.GetService<IPythonAnalyzer>();
            _log = _services.GetService<ILogger>();
        }

        public void Start(PythonAnalyzerSession previousSession, PythonAnalyzerEntry entry) {
            IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker;
            State previousSessionState;

            lock (_syncObj) {
                walker = _walker;
                if (_state != State.NotStarted) {
                    _cts.Dispose();
                    return;
                }

                previousSessionState = previousSession?.CancelOrSchedule(this, _version) ?? State.Completed;
                if (previousSessionState == State.Completed) {
                    _state = State.Started;
                    _walker = null;
                }
            }

            switch (previousSessionState) {
                case State.Started when entry.IsUserModule:
                    StartAnalysis(entry, walker.Version);
                    break;
                case State.Completed:
                    Start(walker);
                    break;
            }
        }

        private void StartNextSession(Task task) {
            PythonAnalyzerSession nextSession;
            lock (_syncObj) {
                _walker = null;
                nextSession = _nextSession;
                _nextSession = null;
            }

            nextSession?.TryStart();
        }

        private State CancelOrSchedule(PythonAnalyzerSession nextSession, int version) {
            lock (_syncObj) {
                Check.InvalidOperation(_nextSession == null);
                Check.InvalidOperation(nextSession != null);
                switch (_state) {
                    case State.NotStarted:
                        _state = State.Completed;
                        break;
                    case State.Started:
                        _nextSession = nextSession;
                        Interlocked.Exchange(ref _version, version);
                        break;
                    case State.Completed:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return _state;
            }
        }

        private void TryStart() {
            IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker = default;
            lock (_syncObj) {
                if (_state == State.NotStarted) {
                    _state = State.Started;
                    walker = _walker;
                }

                _walker = null;
            }

            if (walker != default) {
                Start(walker);
            }
        }

        private void Start(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker) => StartAsync(walker).ContinueWith(_startNextSession).DoNotWait();

        private async Task StartAsync(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker) {
            int version;
            lock (_syncObj) {
                version = _version;
                var notAnalyzed = walker.AffectedValues.Count(e => e.NotAnalyzed);

                if (version > walker.Version && notAnalyzed < _maxTaskRunning) {
                    _state = State.Completed;
                    _cts.Dispose();
                    return;
                }
            }
            
            var stopWatch = Stopwatch.StartNew();
            foreach (var affectedEntry in walker.AffectedValues) {
                affectedEntry.Invalidate(version);
            }

            var originalRemaining = walker.Remaining;
            var remaining = originalRemaining;
            try {
                _log?.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} of {originalRemaining} entries has started.");
                remaining = await AnalyzeAffectedEntriesAsync(walker, stopWatch, _cts.Token);
            } finally {
                _cts.Dispose();
                stopWatch.Stop();

                lock (_syncObj) {
                    if (_version == walker.Version) {
                        _progress.ReportRemaining(walker.Remaining);
                    }

                    _state = State.Completed;
                    _cts.Dispose();
                }

                if (_log != null) {
                    if (remaining == 0) {
                        _log.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} of {originalRemaining} entries has been completed in {stopWatch.Elapsed.TotalMilliseconds} ms.");
                    } else if (remaining < originalRemaining) {
                        _log.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} has been completed in {stopWatch.Elapsed.TotalMilliseconds} ms with {originalRemaining - remaining} entries analyzed and {remaining} entries skipped.");
                    } else {
                        _log.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} of {originalRemaining} entries has been canceled after {stopWatch.Elapsed.TotalMilliseconds}.");
                    }
                }
            }
        }

        private async Task<int> AnalyzeAffectedEntriesAsync(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, Stopwatch stopWatch, CancellationToken cancellationToken) {
            IDependencyChainNode<PythonAnalyzerEntry> node;
            var remaining = 0;
            while ((node = await walker.GetNextAsync(cancellationToken)) != null) {
                if (_version > walker.Version && !node.Value.NotAnalyzed) {
                    remaining++;
                    node.Skip();
                    continue;
                }

                if (Interlocked.Increment(ref _runningTasks) >= _maxTaskRunning || walker.Remaining == 1) {
                    Analyze(walker, node, stopWatch, cancellationToken);
                } else {
                    StartAnalysis(walker, node, stopWatch, cancellationToken).DoNotWait();
                }
            }

            if (walker.MissingKeys.All(k => k.IsTypeshed)) {
                Interlocked.Exchange(ref _runningTasks, 0);
                if (_version == walker.Version) {
                    _analysisCompleteEvent.Set();
                }
            }

            return remaining;
        }


        private Task StartAnalysis(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, IDependencyChainNode<PythonAnalyzerEntry> node, Stopwatch stopWatch, CancellationToken cancellationToken)
            => Task.Run(() => Analyze(walker, node, stopWatch, cancellationToken), cancellationToken);

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        private void Analyze(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, IDependencyChainNode<PythonAnalyzerEntry> node, Stopwatch stopWatch, CancellationToken cancellationToken) {
            IPythonModule module;
            try {
                var entry = node.Value;
                if (!entry.IsValidVersion(walker.Version, out module, out var ast)) {
                    _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
                    node.Skip();
                    return;
                }
                var startTime = stopWatch.Elapsed;
                AnalyzeEntry(entry, module, ast, walker.Version, cancellationToken);
                node.Commit();

                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) completed in {(stopWatch.Elapsed - startTime).TotalMilliseconds} ms.");
            } catch (OperationCanceledException oce) {
                node.Value.TryCancel(oce, walker.Version);
                node.Skip();
                module = node.Value.Module;
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
            } catch (Exception exception) {
                module = node.Value.Module;
                node.Value.TrySetException(exception, walker.Version);
                node.Commit();
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) failed.");
            } finally {
                if (_version == walker.Version) {
                    _progress.ReportRemaining(walker.Remaining);
                }
                Interlocked.Decrement(ref _runningTasks);
            }
        }

        private void StartAnalysis(PythonAnalyzerEntry entry, int version)
            => Task.Run(() => Analyze(entry, version, _cts.Token), _cts.Token).DoNotWait();

        private void Analyze(PythonAnalyzerEntry entry, int version, CancellationToken cancellationToken) {
            var stopWatch = Stopwatch.StartNew();
            try {
                if (!entry.IsValidVersion(version, out var module, out var ast)) {
                    _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
                    return;
                }

                var startTime = stopWatch.Elapsed;
                AnalyzeEntry(entry, module, ast, version, cancellationToken);

                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) completed in {(stopWatch.Elapsed - startTime).TotalMilliseconds} ms.");
            } catch (OperationCanceledException oce) {
                entry.TryCancel(oce, version);
                var module = entry.Module;
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
            } catch (Exception exception) {
                var module = entry.Module;
                entry.TrySetException(exception, version);
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) failed.");
            } finally {
                stopWatch.Stop();
                Interlocked.Decrement(ref _runningTasks);
            }
        }

        private void AnalyzeEntry(PythonAnalyzerEntry entry, IPythonModule module, PythonAst ast, int version, CancellationToken cancellationToken) {
            // Now run the analysis.
            var walker = new ModuleWalker(_services, module, ast);

            ast.Walk(walker);
            cancellationToken.ThrowIfCancellationRequested();

            walker.Complete();
            cancellationToken.ThrowIfCancellationRequested();
            var analysis = new DocumentAnalysis((IDocument)module, version, walker.GlobalScope, walker.Eval, walker.ExportedMemberNames);

            (module as IAnalyzable)?.NotifyAnalysisComplete(analysis);
            entry.TrySetAnalysis(analysis, version);

            if (module.ModuleType == ModuleType.User) {
                var linterDiagnostics = _analyzer.LintModule(module);
                _diagnosticsService.Replace(entry.Module.Uri, linterDiagnostics, DiagnosticSource.Linter);
            }
        }

        private enum State {
            NotStarted = 0,
            Started = 1,
            Completed = 2
        }
    }
}
