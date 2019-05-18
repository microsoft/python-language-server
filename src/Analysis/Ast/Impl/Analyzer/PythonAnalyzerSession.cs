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
using System.Runtime;
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

        private IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> _walker;
        private readonly PythonAnalyzerEntry _entry;
        private readonly Action<Task> _startNextSession;
        private readonly CancellationToken _analyzerCancellationToken;
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
        public int AffectedEntriesCount { get; }

        public PythonAnalyzerSession(IServiceManager services,
            IProgressReporter progress,
            AsyncManualResetEvent analysisCompleteEvent,
            Action<Task> startNextSession,
            CancellationToken analyzerCancellationToken,
            IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker,
            int version,
            PythonAnalyzerEntry entry) {

            _services = services;
            _analysisCompleteEvent = analysisCompleteEvent;
            _startNextSession = startNextSession;
            _analyzerCancellationToken = analyzerCancellationToken;
            Version = version;
            AffectedEntriesCount = walker?.AffectedValues.Count ?? 1;
            _walker = walker;
            _entry = entry;
            _state = State.NotStarted;

            _diagnosticsService = _services.GetService<IDiagnosticsService>();
            _analyzer = _services.GetService<IPythonAnalyzer>();
            _log = _services.GetService<ILogger>();
            _telemetry = _services.GetService<ITelemetryService>();
            _progress = progress;
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
                Task.Run(() => AnalyzeEntry(), _analyzerCancellationToken).DoNotWait();
            } else {
                StartAsync().ContinueWith(_startNextSession, _analyzerCancellationToken).DoNotWait();
            }
        }

        public void Cancel() {
            lock (_syncObj) {
                _isCanceled = true;
            }
        }

        private async Task StartAsync() {
            _progress.ReportRemaining(_walker.Remaining);

            lock (_syncObj) {
                var notAnalyzed = _walker.AffectedValues.Count(e => e.NotAnalyzed);

                if (_isCanceled && notAnalyzed < _maxTaskRunning) {
                    _state = State.Completed;
                    return;
                }
            }

            var stopWatch = Stopwatch.StartNew();
            foreach (var affectedEntry in _walker.AffectedValues) {
                affectedEntry.Invalidate(Version);
            }

            var originalRemaining = _walker.Remaining;
            var remaining = originalRemaining;
            try {
                _log?.Log(TraceEventType.Verbose, $"Analysis version {Version} of {originalRemaining} entries has started.");
                remaining = await AnalyzeAffectedEntriesAsync(stopWatch);
            } finally {
                stopWatch.Stop();

                bool isCanceled;
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                    _state = State.Completed;
                    _walker = null;
                }

                if (!isCanceled) {
                    _progress.ReportRemaining(remaining);
                }
            }

            var elapsed = stopWatch.Elapsed.TotalMilliseconds;

            SendTelemetry(_telemetry, elapsed, originalRemaining, remaining, Version);
            LogResults(_log, elapsed, originalRemaining, remaining, Version);
            ForceGCIfNeeded(originalRemaining, remaining);
        }

        private static void ForceGCIfNeeded(int originalRemaining, int remaining) {
            if (originalRemaining - remaining > 1000) {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
        }

        private static void SendTelemetry(ITelemetryService telemetry, double elapsed, int originalRemaining, int remaining, int version) {
            if (telemetry == null) {
                return;
            }

            if (remaining != 0 || originalRemaining < 100) {
                return;
            }

            double privateMB;
            double peakPagedMB;
            double workingMB;

            using (var proc = Process.GetCurrentProcess()) {
                privateMB = proc.PrivateMemorySize64 / 1e+6;
                peakPagedMB = proc.PeakPagedMemorySize64 / 1e+6;
                workingMB = proc.WorkingSet64 / 1e+6;
            }

            var e = new TelemetryEvent {
                EventName = "python_language_server/analysis_complete", // TODO: Move this common prefix into Core.
            };

            e.Measurements["privateMB"] = privateMB;
            e.Measurements["peakPagedMB"] = peakPagedMB;
            e.Measurements["workingMB"] = workingMB;
            e.Measurements["elapsedMs"] = elapsed;
            e.Measurements["entries"] = originalRemaining;
            e.Measurements["version"] = version;

            telemetry.SendTelemetryAsync(e).DoNotWait();
        }

        private static void LogResults(ILogger logger, double elapsed, int originalRemaining, int remaining, int version) {
            if (logger == null) {
                return;
            }
            
            if (remaining == 0) {
                logger.Log(TraceEventType.Verbose, $"Analysis version {version} of {originalRemaining} entries has been completed in {elapsed} ms.");
            } else if (remaining < originalRemaining) {
                logger.Log(TraceEventType.Verbose, $"Analysis version {version} has been completed in {elapsed} ms with {originalRemaining - remaining} entries analyzed and {remaining} entries skipped.");
            } else {
                logger.Log(TraceEventType.Verbose, $"Analysis version {version} of {originalRemaining} entries has been canceled after {elapsed}.");
            }
        }

        private async Task<int> AnalyzeAffectedEntriesAsync(Stopwatch stopWatch) {
            IDependencyChainNode<PythonAnalyzerEntry> node;
            var remaining = 0;
            var ace = new AsyncCountdownEvent(0);

            bool isCanceled;
            while ((node = await _walker.GetNextAsync(_analyzerCancellationToken)) != null) {
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                }

                if (isCanceled && !node.Value.NotAnalyzed) {
                    remaining++;
                    node.Skip();
                    continue;
                }

                if (Interlocked.Increment(ref _runningTasks) >= _maxTaskRunning || _walker.Remaining == 1) {
                    Analyze(node, null, stopWatch);
                } else {
                    StartAnalysis(node, ace, stopWatch).DoNotWait();
                }
            }

            await ace.WaitAsync(_analyzerCancellationToken);

            lock (_syncObj) {
                isCanceled = _isCanceled;
            }

            if (_walker.MissingKeys.Count == 0 || _walker.MissingKeys.All(k => k.IsTypeshed)) {
                Interlocked.Exchange(ref _runningTasks, 0);
                
                if (!isCanceled) {
                    _analysisCompleteEvent.Set();
                }
            } else if (!isCanceled && _log != null && _log.LogLevel >= TraceEventType.Verbose) {
                _log?.Log(TraceEventType.Verbose, $"Missing keys: {string.Join(", ", _walker.MissingKeys)}");
            }

            return remaining;
        }


        private Task StartAnalysis(IDependencyChainNode<PythonAnalyzerEntry> node, AsyncCountdownEvent ace, Stopwatch stopWatch)
            => Task.Run(() => Analyze(node, ace, stopWatch));

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        private void Analyze(IDependencyChainNode<PythonAnalyzerEntry> node, AsyncCountdownEvent ace, Stopwatch stopWatch) {
            IPythonModule module;
            try {
                ace?.AddOne();
                var entry = node.Value;
                if (!entry.IsValidVersion(_walker.Version, out module, out var ast)) {
                    if (ast == null) {
                        // Entry doesn't have ast yet. There should be at least one more session.
                        Cancel();
                    }

                    _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
                    node.Skip();
                    return;
                }
                var startTime = stopWatch.Elapsed;
                AnalyzeEntry(entry, module, _walker.Version, node.IsComplete);
                node.Commit();

                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) completed in {(stopWatch.Elapsed - startTime).TotalMilliseconds} ms.");
            } catch (OperationCanceledException oce) {
                node.Value.TryCancel(oce, _walker.Version);
                node.Skip();
                module = node.Value.Module;
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
            } catch (Exception exception) {
                module = node.Value.Module;
                node.Value.TrySetException(exception, _walker.Version);
                node.Commit();
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) failed. Exception message: {exception.Message}.");
            } finally {
                bool isCanceled;
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                }

                if (!isCanceled) {
                    _progress.ReportRemaining(_walker.Remaining);
                }

                Interlocked.Decrement(ref _runningTasks);
                ace?.Signal();
            }
        }

        private void AnalyzeEntry() {
            var stopWatch = Stopwatch.StartNew();
            try {
                if (!_entry.IsValidVersion(Version, out var module, out var ast)) {
                    if (ast == null) {
                        // Entry doesn't have ast yet. There should be at least one more session.
                        Cancel();
                    }
                    _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
                    return;
                }

                var startTime = stopWatch.Elapsed;
                AnalyzeEntry(_entry, module, Version, true);

                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) completed in {(stopWatch.Elapsed - startTime).TotalMilliseconds} ms.");
            } catch (OperationCanceledException oce) {
                _entry.TryCancel(oce, Version);
                var module = _entry.Module;
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
            } catch (Exception exception) {
                var module = _entry.Module;
                _entry.TrySetException(exception, Version);
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) failed. Exception message: {exception.Message}.");
            } finally {
                stopWatch.Stop();
                Interlocked.Decrement(ref _runningTasks);
            }
        }

        private void AnalyzeEntry(PythonAnalyzerEntry entry, IPythonModule module, int version, bool isFinalPass) {
            // Now run the analysis.
            var analyzable = module as IAnalyzable;
            analyzable?.NotifyAnalysisBegins();

            var ast = module.GetAst();
            var walker = new ModuleWalker(_services, module);
            ast.Walk(walker);

            _analyzerCancellationToken.ThrowIfCancellationRequested();

            walker.Complete();
            _analyzerCancellationToken.ThrowIfCancellationRequested();

            var analysis = CreateAnalysis((IDocument)module, version, walker, isFinalPass);

            analyzable?.NotifyAnalysisComplete(analysis);
            entry.TrySetAnalysis(analysis, version);

            if (module.ModuleType == ModuleType.User) {
                var linterDiagnostics = _analyzer.LintModule(module);
                _diagnosticsService?.Replace(entry.Module.Uri, linterDiagnostics, DiagnosticSource.Linter);
            }
        }

        private enum State {
            NotStarted = 0,
            Started = 1,
            Completed = 2
        }

        private IDocumentAnalysis CreateAnalysis(IDocument document, int version, ModuleWalker walker, bool isFinalPass)
            => document.ModuleType == ModuleType.User || !isFinalPass
                ? (IDocumentAnalysis)new DocumentAnalysis(document, version, walker.GlobalScope, walker.Eval, walker.StarImportMemberNames)
                : new LibraryAnalysis(document, version, walker.Eval.Services, walker.GlobalScope, walker.StarImportMemberNames);
    }
}
