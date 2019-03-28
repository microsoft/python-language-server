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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly AsyncAutoResetEvent _analysisRunningEvent = new AsyncAutoResetEvent();
        private readonly ProgressReporter _progress;
        private readonly ILogger _log;
        private readonly int _maxTaskRunning = Environment.ProcessorCount;
        private int _runningTasks;
        private int _version;

        public PythonAnalyzer(IServiceManager services) {
            _services = services;
            _log = services.GetService<ILogger>();
            _dependencyResolver = new DependencyResolver<AnalysisModuleKey, PythonAnalyzerEntry>();
            _analysisCompleteEvent.Set();
            _analysisRunningEvent.Set();

            _progress = new ProgressReporter(services.GetService<IProgressService>());
            Task.Run(() => Worker()).DoNotWait();
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

            var optionsProvider = _services.GetService<IAnalysisOptionsProvider>();
            if (optionsProvider?.Options?.LintingEnabled == false) {
                return Array.Empty<DiagnosticsEntry>();
            }

            var linter = new LinterAggregator();
            return linter.Lint(module.Analysis, _services);
        }

        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private readonly ManualResetEventSlim _workAvailable = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _workerAvailable = new ManualResetEventSlim(true);
        private readonly object _lock = new object();
        private int _workerCount;

        private void AnalyzeDocument(AnalysisModuleKey key, PythonAnalyzerEntry entry, ImmutableArray<AnalysisModuleKey> dependencies, CancellationToken cancellationToken) {
            lock (_lock) {
                _queue.Enqueue(() => DoAnalyzeDocument(key, entry, dependencies, cancellationToken));
                _workAvailable.Set();
            }
        }

        private void Worker() {
            var maxConcurrent = Math.Max(Environment.ProcessorCount / 2, 4);
            while (true) {
                _workAvailable.Wait();
                if (_queue.TryDequeue(out var action)) {
                    _workerAvailable.Wait();
                    lock (_lock) {
                        _workerCount++;
                        if (_workerCount >= maxConcurrent) {
                            _workerAvailable.Reset();
                        }
                    }

                    Task.Run(action).ContinueWith(t => {
                        lock (_lock) {
                            _workerCount--;
                            _workerAvailable.Set();
                        }
                    });
                } else {
                    _workAvailable.Reset();
                }
            }
        }

        private void DoAnalyzeDocument(AnalysisModuleKey key, PythonAnalyzerEntry entry, ImmutableArray<AnalysisModuleKey> dependencies, CancellationToken cancellationToken) {
            _analysisCompleteEvent.Reset();
            _log?.Log(TraceEventType.Verbose, $"Analysis of {entry.Module.Name}({entry.Module.ModuleType}) queued");

            var walker = _dependencyResolver.NotifyChanges(key, entry, dependencies);
            if (_version + 1 == walker.Version) {
                _progress.ReportRemaining(walker.Remaining);
            }

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.CancellationToken, cancellationToken)) {
                var analysisToken = cts.Token;
                lock (_syncObj) {
                    if (_version > walker.Version) {
                        return;
                    }

                    _version = walker.Version;
                }

                if (walker.MissingKeys.Count > 0) {
                    LoadMissingDocuments(entry.Module.Interpreter, walker.MissingKeys);
                }

                lock (_syncObj) {
                    if (_version > walker.Version && walker.AffectedValues.Count(e => e.NotAnalyzed) < _maxTaskRunning) {
                        return;
                    }
                }

                var waitForAnalysisTask = _analysisRunningEvent.WaitAsync(cancellationToken);
                if (!waitForAnalysisTask.IsCompleted) {
                    if (entry.IsUserModule) {
                        StartAnalysis(entry, walker.Version, cancellationToken);
                    }

                    waitForAnalysisTask.Wait(cancellationToken);
                }

                int version;
                int notAnalyzed;
                lock (_syncObj) {
                    version = _version;
                    notAnalyzed = walker.AffectedValues.Count(e => e.NotAnalyzed);
                }

                var stopWatch = Stopwatch.StartNew();
                if (version > walker.Version) {
                    if (notAnalyzed < _maxTaskRunning) {
                        _analysisRunningEvent.Set();
                        return;
                    }
                }

                foreach (var affectedEntry in walker.AffectedValues) {
                    affectedEntry.Invalidate(version);
                }

                var originalRemaining = walker.Remaining;
                var remaining = originalRemaining;
                try {
                    _log?.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} of {originalRemaining} entries has started.");
                    remaining = AnalyzeAffectedEntriesAsync(walker, stopWatch, analysisToken).GetAwaiter().GetResult();
                } finally {
                    _analysisRunningEvent.Set();
                    stopWatch.Stop();

                    if (_version == walker.Version) {
                        _progress.ReportRemaining(walker.Remaining);
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
        }

        private async Task<int> AnalyzeAffectedEntriesAsync(IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, Stopwatch stopWatch, CancellationToken cancellationToken) {
            IDependencyChainNode<PythonAnalyzerEntry> node;
            var remaining = 0;
            while ((node = await walker.GetNextAsync(cancellationToken)) != null) {
                int version;
                lock (_syncObj) {
                    version = _version;
                }

                if (version > walker.Version && !node.Value.NotAnalyzed) {
                    remaining++;
                    node.Skip();
                    continue;
                }

                if (Interlocked.Increment(ref _runningTasks) >= _maxTaskRunning || walker.Remaining == 1) {
                    Analyze(node, walker, stopWatch, cancellationToken);
                } else {
                    StartAnalysis(node, walker, stopWatch, cancellationToken).DoNotWait();
                }
            }

            if (walker.MissingKeys.All(k => k.IsTypeshed)) {
                Interlocked.Exchange(ref _runningTasks, 0);
                int version;
                lock (_syncObj) {
                    version = _version;
                }

                if (version == walker.Version) {
                    _analysisCompleteEvent.Set();
                }
            }

            return remaining;
        }

        private void LoadMissingDocuments(IPythonInterpreter interpreter, ImmutableArray<AnalysisModuleKey> missingKeys) {
            foreach (var (moduleName, _, isTypeshed) in missingKeys) {
                var moduleResolution = isTypeshed ? interpreter.TypeshedResolution : interpreter.ModuleResolution;
                var m = moduleResolution.GetOrLoadModule(moduleName);
            }
        }

        private Task StartAnalysis(IDependencyChainNode<PythonAnalyzerEntry> node, IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, Stopwatch stopWatch, CancellationToken cancellationToken)
            => Task.Run(() => Analyze(node, walker, stopWatch, cancellationToken), cancellationToken);

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        private void Analyze(IDependencyChainNode<PythonAnalyzerEntry> node, IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker, Stopwatch stopWatch, CancellationToken cancellationToken) {
            IPythonModule module = null;
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

        private void StartAnalysis(PythonAnalyzerEntry entry, int version, CancellationToken cancellationToken)
            => Task.Run(() => Analyze(entry, version, cancellationToken), cancellationToken).DoNotWait();

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
                var linterDiagnostics = LintModule(module);
                var ds = _services.GetService<IDiagnosticsService>();
                ds.Replace(entry.Module.Uri, linterDiagnostics, DiagnosticSource.Linter);
            }
        }
    }

    [DebuggerDisplay("{Name} : {FilePath}")]
    internal struct AnalysisModuleKey : IEquatable<AnalysisModuleKey> {
        public string Name { get; }
        public string FilePath { get; }
        public bool IsTypeshed { get; }

        public AnalysisModuleKey(IPythonModule module) {
            Name = module.Name;
            FilePath = module.ModuleType == ModuleType.CompiledBuiltin ? null : module.FilePath;
            IsTypeshed = module is StubPythonModule stub && stub.IsTypeshed;
        }

        public AnalysisModuleKey(string name, string filePath, bool isTypeshed) {
            Name = name;
            FilePath = filePath;
            IsTypeshed = isTypeshed;
        }

        public bool Equals(AnalysisModuleKey other)
            => Name.EqualsOrdinal(other.Name) && FilePath.PathEquals(other.FilePath) && IsTypeshed == other.IsTypeshed;

        public override bool Equals(object obj) => obj is AnalysisModuleKey other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (FilePath != null ? FilePath.GetPathHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsTypeshed.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(AnalysisModuleKey left, AnalysisModuleKey right) => left.Equals(right);

        public static bool operator !=(AnalysisModuleKey left, AnalysisModuleKey right) => !left.Equals(right);

        public void Deconstruct(out string moduleName, out string filePath, out bool isTypeshed) {
            moduleName = Name;
            filePath = FilePath;
            isTypeshed = IsTypeshed;
        }

        public override string ToString() => $"{Name}({FilePath})";
    }
}
