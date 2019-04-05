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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class PythonAnalyzerSession {
        private readonly int _maxTaskRunning = Environment.ProcessorCount;
        private readonly object _syncObj = new object();

        private readonly IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> _walker;
        private readonly PythonAnalyzerEntry _entry;
        private readonly CancellationTokenSource _cts;
        private readonly Action<Task> _startNextSession;
        private readonly IServiceManager _services;
        private readonly AsyncManualResetEvent _analysisCompleteEvent;
        private readonly IDiagnosticsService _diagnosticsService;
        private readonly IProgressReporter _progress;
        private readonly IPythonAnalyzer _analyzer;
        private readonly ILogger _log;
        private readonly ITelemetryService _telemetry;

        private State _state;
        private bool _isCanceled;
        private int _runningTasks;

        public bool IsCompleted {
            get {
                lock (_syncObj) {
                    return _state == State.Completed;
                }
            }
        }

        public int Version { get; }

        public PythonAnalyzerSession(IServiceManager services,
            IProgressReporter progress,
            AsyncManualResetEvent analysisCompleteEvent,
            Action<Task> startNextSession,
            CancellationToken analyzerToken,
            CancellationToken sessionToken,
            IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker,
            int version,
            PythonAnalyzerEntry entry) {

            _services = services;
            _analysisCompleteEvent = analysisCompleteEvent;
            _startNextSession = startNextSession;
            Version = version;
            _walker = walker;
            _entry = entry;
            _state = State.NotStarted;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(analyzerToken, sessionToken);

            _diagnosticsService = _services.GetService<IDiagnosticsService>();
            _progress = progress;
            _analyzer = _services.GetService<IPythonAnalyzer>();
            _log = _services.GetService<ILogger>();
            _telemetry = _services.GetService<ITelemetryService>();
        }

        public void Start(bool analyzeEntry) {
            lock (_syncObj) {
                if (_state != State.NotStarted) {
                    analyzeEntry = false;
                } else if (_state == State.Completed) {
                    return;
                } else {
                    _state = State.Started;
                }
            }

            if (analyzeEntry && _entry != null) {
                Task.Run(() => Analyze(_entry, Version, _cts.Token), _cts.Token).DoNotWait();
            } else {
                StartAsync(_walker).ContinueWith(_startNextSession).DoNotWait();
            }
        }

        public void Cancel() {
            lock (_syncObj) {
                _isCanceled = true;
            }
        }

        private async Task StartAsync(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker) {
            _progress.ReportRemaining(walker.Remaining);

            lock (_syncObj) {
                var notAnalyzed = walker.AffectedValues.Count(e => e.NotAnalyzed);

                if (_isCanceled && notAnalyzed < _maxTaskRunning) {
                    _state = State.Completed;
                    _cts.Dispose();
                    return;
                }
            }

            var cancellationToken = _cts.Token;
            var stopWatch = Stopwatch.StartNew();
            foreach (var affectedEntry in walker.AffectedValues) {
                affectedEntry.Invalidate(Version);
            }

            var originalRemaining = walker.Remaining;
            var remaining = originalRemaining;
            try {
                _log?.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} of {originalRemaining} entries has started.");
                remaining = await AnalyzeAffectedEntriesAsync(walker, stopWatch, cancellationToken);
            } finally {
                _cts.Dispose();
                stopWatch.Stop();

                bool isCanceled;
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                    _state = State.Completed;
                }

                if (!isCanceled) {
                    _progress.ReportRemaining(remaining);
                }
            }

            var elapsed = stopWatch.Elapsed.TotalMilliseconds;

            SendTelemetry(elapsed, originalRemaining, remaining, walker.Version);
            LogResults(elapsed, originalRemaining, remaining, walker.Version);
        }

        private void SendTelemetry(double elapsed, int originalRemaining, int remaining, int version) {
            if (_telemetry == null) {
                return;
            }

            if (remaining == 0 && originalRemaining > 100) {
                return;
            }

            double privateMB;
            double peakPagedMB;

            using (var proc = Process.GetCurrentProcess()) {
                privateMB = proc.PrivateMemorySize64 / 1e+6;
                peakPagedMB = proc.PeakPagedMemorySize64 / 1e+6;
            }

            var e = new TelemetryEvent {
                EventName = "analysis_complete",
            };

            e.Measurements["privateMB"] = privateMB;
            e.Measurements["peakPagedMB"] = peakPagedMB;
            e.Measurements["elapsedMs"] = elapsed;
            e.Measurements["entries"] = originalRemaining;
            e.Measurements["version"] = version;

            _telemetry.SendTelemetryAsync(e).DoNotWait();
        }

        private void LogResults(double elapsed, int originalRemaining, int remaining, int version) {
            if (_log == null) {
                return;
            }

            if (remaining == 0) {
                _log.Log(TraceEventType.Verbose, $"Analysis version {version} of {originalRemaining} entries has been completed in {elapsed} ms.");
            } else if (remaining < originalRemaining) {
                _log.Log(TraceEventType.Verbose, $"Analysis version {version} has been completed in {elapsed} ms with {originalRemaining - remaining} entries analyzed and {remaining} entries skipped.");
            } else {
                _log.Log(TraceEventType.Verbose, $"Analysis version {version} of {originalRemaining} entries has been canceled after {elapsed}.");
            }
        }

        private async Task<int> AnalyzeAffectedEntriesAsync(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, Stopwatch stopWatch, CancellationToken cancellationToken) {
            IDependencyChainNode<PythonAnalyzerEntry> node;
            var remaining = 0;
            while ((node = await walker.GetNextAsync(cancellationToken)) != null) {
                bool isCanceled;
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                }

                if (isCanceled && !node.Value.NotAnalyzed) {
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
                bool isCanceled;
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                }

                if (!isCanceled) {
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
                bool isCanceled;
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                }

                if (!isCanceled) {
                    _progress.ReportRemaining(walker.Remaining);
                }
                Interlocked.Decrement(ref _runningTasks);
            }
        }

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
