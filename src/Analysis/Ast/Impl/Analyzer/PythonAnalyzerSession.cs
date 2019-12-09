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
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Analyzer.Handlers;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Testing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class PythonAnalyzerSession {
        private readonly int _maxTaskRunning = Environment.ProcessorCount;
        private readonly object _syncObj = new object();

        private IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> _walker;
        private readonly PythonAnalyzerEntry _entry;
        private readonly Action<Task> _startNextSession;
        private readonly CancellationToken _analyzerCancellationToken;
        private readonly IServiceContainer _services;
        private readonly IDiagnosticsService _diagnosticsService;
        private readonly IOSPlatform _platformService;
        private readonly IProgressReporter _progress;
        private readonly PythonAnalyzer _analyzer;
        private readonly ILogger _log;
        private readonly bool _forceGC;
        private readonly PathResolverSnapshot _modulesPathResolver;
        private readonly PathResolverSnapshot _typeshedPathResolver;
        private readonly AsyncCountdownEvent _ace = new AsyncCountdownEvent(0);

        private State _state;
        private bool _isCanceled;

        public bool IsCompleted {
            get {
                lock (_syncObj) {
                    return _state == State.Completed;
                }
            }
        }

        public int Version { get; }
        public int AffectedEntriesCount { get; }

        public PythonAnalyzerSession(IServiceContainer services,
            IProgressReporter progress,
            Action<Task> startNextSession,
            CancellationToken analyzerCancellationToken,
            IDependencyChainWalker<AnalysisModuleKey, PythonAnalyzerEntry> walker,
            int version,
            PythonAnalyzerEntry entry,
            bool forceGC = false) {

            _services = services;
            _startNextSession = startNextSession;

            _analyzerCancellationToken = analyzerCancellationToken;
            Version = version;
            AffectedEntriesCount = walker?.AffectedValues.Count ?? 1;
            _walker = walker;
            _entry = entry;
            _state = State.NotStarted;
            _forceGC = forceGC;

            _diagnosticsService = _services.GetService<IDiagnosticsService>();
            _platformService = _services.GetService<IOSPlatform>();
            _analyzer = _services.GetService<PythonAnalyzer>();
            _log = _services.GetService<ILogger>();
            _progress = progress;

            var interpreter = _services.GetService<IPythonInterpreter>();
            _modulesPathResolver = interpreter.ModuleResolution.CurrentPathResolver;
            _typeshedPathResolver = interpreter.TypeshedResolution.CurrentPathResolver;
        }

        public void Start(bool analyzeEntry) {
            lock (_syncObj) {
                if (_state == State.Completed) {
                    return;
                }

                if (_state != State.NotStarted) {
                    analyzeEntry = false;
                } else {
                    _state = State.Started;
                }
            }

            if (analyzeEntry && _entry != null) {
                Task.Run(AnalyzeEntry, _analyzerCancellationToken).DoNotWait();
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
            var originalRemaining = _walker.Remaining;
            var remaining = originalRemaining;
            try {
                _log?.Log(TraceEventType.Verbose, $"Analysis version {Version} of {originalRemaining} entries has started.");
                remaining = await AnalyzeAffectedEntriesAsync(stopWatch);
                Debug.Assert(_ace.Count == 0);
            } finally {
                stopWatch.Stop();

                var isFinal = false;
                lock (_syncObj) {
                    if (!_isCanceled) {
                        _progress.ReportRemaining(remaining);
                    }

                    _state = State.Completed;
                    isFinal = _walker.MissingKeys.Count == 0 && !_isCanceled && remaining == 0;
                    _walker = null;
                }

                if (isFinal) {
                    var (modulesCount, totalMilliseconds) = ActivityTracker.EndTracking();
                    totalMilliseconds = Math.Round(totalMilliseconds, 2);
                    if (await _analyzer.RaiseAnalysisCompleteAsync(modulesCount, totalMilliseconds)) {
                        _log?.Log(TraceEventType.Verbose, $"Analysis complete: {modulesCount} modules in {totalMilliseconds} ms.");
                    }
                }
            }

            var elapsed = stopWatch.Elapsed.TotalMilliseconds;
            LogResults(_log, elapsed, originalRemaining, remaining, Version);
            ForceGCIfNeeded(_log, originalRemaining, remaining, _forceGC);
        }

        private static void ForceGCIfNeeded(ILogger logger, int originalRemaining, int remaining, bool force) {
            if (force || originalRemaining - remaining > 1000) {
                logger?.Log(TraceEventType.Verbose, "Forcing full garbage collection and heap compaction.");
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
        }


        private static void LogResults(ILogger logger, double elapsed, int originalRemaining, int remaining, int version) {
            if (logger == null) {
                return;
            }

            elapsed = Math.Round(elapsed, 2);
            if (remaining == 0) {
                logger.Log(TraceEventType.Verbose, $"Analysis version {version} of {originalRemaining} entries has been completed in {elapsed} ms.");
            } else if (remaining < originalRemaining) {
                logger.Log(TraceEventType.Verbose, $"Analysis version {version} has been completed in {elapsed} ms with {originalRemaining - remaining} entries analyzed and {remaining} entries skipped.");
            } else {
                logger.Log(TraceEventType.Verbose, $"Analysis version {version} of {originalRemaining} entries has been canceled after {elapsed}.");
            }
        }

        private async Task<int> AnalyzeAffectedEntriesAsync(Stopwatch stopWatch) {
            IDependencyChainNode node;
            var remaining = 0;

            while ((node = await _walker.GetNextAsync(_analyzerCancellationToken)) != null) {
                var taskLimitReached = false;
                lock (_syncObj) {
                    if (_isCanceled) {
                        switch (node) {
                            case IDependencyChainLoopNode<PythonAnalyzerEntry> loop:
                                // Loop analysis is not cancellable or else small
                                // inner loops of a larger loop will not be analyzed
                                // correctly as large loop may cancel inner loop pass.
                                break;
                            case IDependencyChainSingleNode<PythonAnalyzerEntry> single when !single.Value.NotAnalyzed:
                                remaining++;
                                node.MoveNext();
                                continue;
                        }
                    }

                    taskLimitReached = _ace.Count >= _maxTaskRunning || _walker.Remaining == 1;
                    _ace.AddOne();
                }

                if (taskLimitReached) {
                    RunAnalysis(node, stopWatch);
                } else {
                    StartAnalysis(node, stopWatch).DoNotWait();
                }
            }

            await _ace.WaitAsync(_analyzerCancellationToken);

            lock (_syncObj) {
                if (_walker.MissingKeys.Count == 0 || _walker.MissingKeys.All(k => k.IsTypeshed)) {
                    Debug.Assert(_ace.Count == 0);
                } else if (!_isCanceled && _log != null && _log.LogLevel >= TraceEventType.Verbose) {
                    _log?.Log(TraceEventType.Verbose, $"Missing keys: {string.Join(", ", _walker.MissingKeys)}");
                }
            }

            return remaining;
        }

        private void RunAnalysis(IDependencyChainNode node, Stopwatch stopWatch)
            => ExecutionContext.Run(ExecutionContext.Capture(), s => Analyze(node, stopWatch), null);

        private Task StartAnalysis(IDependencyChainNode node, Stopwatch stopWatch)
            => Task.Run(() => Analyze(node, stopWatch));

        private void Analyze(IDependencyChainNode node, Stopwatch stopWatch) {
            var loopAnalysis = false;
            try {
                switch (node) {
                    case IDependencyChainSingleNode<PythonAnalyzerEntry> single:
                        try {
                            Analyze(single, stopWatch);
                        } catch (OperationCanceledException oce) {
                            single.Value.TryCancel(oce, _walker.Version);
                            LogCanceled(single.Value.Module);
                        } catch (Exception exception) {
                            single.Value.TrySetException(exception, _walker.Version);
                            node.MarkWalked();
                            LogException(single.Value, exception);
                        }
                        break;
                    case IDependencyChainLoopNode<PythonAnalyzerEntry> loop:
                        try {
                            loopAnalysis = true;
                            AnalyzeLoop(loop, stopWatch);
                        } catch (OperationCanceledException) {
                        } catch (Exception exception) {
                            node.MarkWalked();
                            LogException(loop, exception);
                        }
                        break;
                }
            } finally {
                lock (_syncObj) {
                    node.MoveNext();
                    if (!_isCanceled || loopAnalysis) {
                        _progress.ReportRemaining(_walker.Remaining);
                    }
                    _ace.Signal();
                }
            }
        }

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        private void Analyze(IDependencyChainSingleNode<PythonAnalyzerEntry> node, Stopwatch stopWatch) {
            ActivityTracker.OnEnqueueModule(node.Value.Module.FilePath);
            var entry = node.Value;
            if (!CanUpdateAnalysis(entry, _walker.Version, out var module, out var ast)) {
                return;
            }
            var startTime = stopWatch.Elapsed;
            AnalyzeEntry(node, entry, module, ast, _walker.Version);

            LogCompleted(node, module, stopWatch, startTime);
        }

        private void AnalyzeEntry() {
            var stopWatch = _log != null ? Stopwatch.StartNew() : null;
            try {
                if (!CanUpdateAnalysis(_entry, Version, out var module, out var ast)) {
                    return;
                }
                var startTime = stopWatch?.Elapsed ?? TimeSpan.Zero;
                AnalyzeEntry(null, _entry, module, ast, Version);

                LogCompleted(module, stopWatch, startTime);
            } catch (OperationCanceledException oce) {
                _entry.TryCancel(oce, Version);
                LogCanceled(_entry.Module);
            } catch (Exception exception) {
                _entry.TrySetException(exception, Version);
                LogException(_entry, exception);
            } finally {
                stopWatch?.Stop();
            }
        }

        private void AnalyzeLoop(IDependencyChainLoopNode<PythonAnalyzerEntry> loopNode, Stopwatch stopWatch) {
            var version = _walker.Version;
            var entries = new Dictionary<AnalysisModuleKey, (IPythonModule Module, PythonAnalyzerEntry Entry)>();
            var variables = new Dictionary<(AnalysisModuleKey Module, string Name), int>();
            var importNames = new List<(AnalysisModuleKey From, int FromPosition, AnalysisModuleKey To, string ToName)>();
            var cachedVariables = new Dictionary<AnalysisModuleKey, IVariableCollection>();
            var asts = new Dictionary<AnalysisModuleKey, PythonAst>();
            var startTime = stopWatch.Elapsed;

            // Note: loop analysis is not cancellable. The reason is that when smaller loop
            // appears inside a larger loop gets canceled, it will not be re-walked during
            // the outer loop analysis. For example, functools <=> _functools loop and
            // the related CircularDependencyFunctools test.
            foreach (var entry in loopNode.Values) {
                ActivityTracker.OnEnqueueModule(entry.Module.FilePath);
                if (!CanUpdateAnalysis(entry, Version, out var module, out var ast)) {
                    continue;
                }

                var moduleKey = new AnalysisModuleKey(module);
                entries[moduleKey] = (module, entry);
                AddLoopImportsFromAst(importNames, variables, moduleKey, ast);
                asts.Add(moduleKey, ast);
            }

            if (asts.Count == 0) {
                // Fully cached loop
                if (_log != null && _log.LogLevel == TraceEventType.Verbose) {
                    var names = string.Join(", ", cachedVariables.Select(v => v.Key.Name));
                    _log?.Log(TraceEventType.Verbose, $"Fully cached modules cycle: {names}");
                }
                return;
            }

            var imports = new List<(AnalysisModuleKey From, int FromPosition, AnalysisModuleKey To, int ToPosition)>();
            foreach (var (fromModule, fromPosition, toModule, toName) in importNames) {
                if (!entries.ContainsKey(toModule)) {
                    continue;
                }

                if (toName == null) {
                    imports.Add((fromModule, fromPosition, toModule, 0));
                } else if (variables.TryGetValue((toModule, toName), out var toPosition)) {
                    imports.Add((fromModule, fromPosition, toModule, toPosition));
                }
            }

            var startingKeys = LocationLoopResolver<AnalysisModuleKey>.FindStartingItems(imports);
            var variableHandler = new LoopImportedVariableHandler(_services, asts, cachedVariables, () => false);
            foreach (var key in startingKeys) {
                if (asts.TryGetValue(key, out var startingAst) && entries.TryGetValue(key, out var me)) {
                    variableHandler.WalkModule(me.Module, startingAst);
                }
            }

            foreach (var walker in variableHandler.Walkers) {
                asts.Remove(new AnalysisModuleKey(walker.Module));
            }

            while (asts.Count > 0) {
                var (moduleKey, ast) = asts.First();
                variableHandler.WalkModule(entries[moduleKey].Module, ast);

                foreach (var walker in variableHandler.Walkers) {
                    asts.Remove(new AnalysisModuleKey(walker.Module));
                }
            }

            foreach (var walker in variableHandler.Walkers) {
                var module = (IDocument)walker.Module;
                var moduleKey = new AnalysisModuleKey(module);
                if (entries.TryGetValue(moduleKey, out var e)) {
                    var analysis = CreateAnalysis(null, module, walker.Ast, version, walker);
                    CompleteAnalysis(e.Entry, module, version, analysis);
                }
            }

            loopNode.MarkWalked();
            LogCompleted(loopNode, entries.Values.Select(v => v.Module), stopWatch, startTime);
        }

        private void AddLoopImportsFromAst(
            in List<(AnalysisModuleKey From, int FromPosition, AnalysisModuleKey To, string ToName)> imports,
            in Dictionary<(AnalysisModuleKey Module, string Name), int> variables,
            in AnalysisModuleKey moduleKey,
            in PythonAst ast) {

            var pathResolver = moduleKey.IsTypeshed ? _typeshedPathResolver : _modulesPathResolver;
            var walker = new ImportExportWalker(ast, _platformService, pathResolver, moduleKey.FilePath, moduleKey.IsTypeshed);
            walker.Walk();

            foreach (var export in walker.Exports) {
                var key = (moduleKey, export.Name);
                var location = export.Location.Start;
                if (!variables.TryGetValue(key, out var currentLocation) || currentLocation > location) {
                    variables[key] = location;
                }
            }

            foreach (var (toModule, name, location) in walker.Imports) {
                imports.Add((moduleKey, location.Start, toModule, name));
            }
        }

        private bool CanUpdateAnalysis(PythonAnalyzerEntry entry, int version, out IPythonModule module, out PythonAst ast) {
            if (entry.CanUpdateAnalysis(version, out module, out ast, out var currentAnalysis)) {
                return true;
            }

            if (ast == null) {
                if (currentAnalysis == null) {
                    // Entry doesn't have ast yet. There should be at least one more session.
                    Cancel();
                    _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled (no AST yet).");
                    return false;
                }
                //Debug.Fail($"Library module {module.Name} of type {module.ModuleType} has been analyzed already!");
                return false;
            }

            _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled. Version: {version}, current: {module.Analysis.Version}.");
            return false;
        }

        private void AnalyzeEntry(IDependencyChainSingleNode<PythonAnalyzerEntry> node, PythonAnalyzerEntry entry, IPythonModule module, PythonAst ast, int version) {
            // Now run the analysis.
            var analyzable = module as IAnalyzable;
            analyzable?.NotifyAnalysisBegins();

            Debug.Assert(ast != null);
            var analysis = AnalyzeModule(node, module, ast, version);
            _analyzerCancellationToken.ThrowIfCancellationRequested();

            if (analysis != null) {
                CompleteAnalysis(entry, module, version, analysis);
            }
        }

        private void CompleteAnalysis(PythonAnalyzerEntry entry, IPythonModule module, int version, IDocumentAnalysis analysis) {
            var analyzable = module as IAnalyzable;
            analyzable?.NotifyAnalysisComplete(analysis);
            entry.TrySetAnalysis(analysis, version);

            if (module.ModuleType != ModuleType.User) {
                return;
            }

            var linterDiagnostics = _analyzer.LintModule(module);
            _diagnosticsService?.Replace(entry.Module.Uri, linterDiagnostics, DiagnosticSource.Linter);
        }

        private IDocumentAnalysis AnalyzeModule(IDependencyChainSingleNode<PythonAnalyzerEntry> node, IPythonModule module, PythonAst ast, int version) {
            if (module is IAnalyzable analyzable) {
                var walker = analyzable.Analyze(ast);
                return CreateAnalysis(node, (IDocument)module, ast, version, walker);
            }
            return new EmptyAnalysis(_services, (IDocument)module);
        }

        private IDocumentAnalysis CreateAnalysis(IDependencyChainSingleNode<PythonAnalyzerEntry> node, IDocument document, PythonAst ast, int version, ModuleWalker walker) {
            var canHaveLibraryAnalysis = false;

            // Don't try to drop builtins; it causes issues elsewhere.
            // We probably want the builtin module's AST and other info for evaluation.
            switch (document.ModuleType) {
                case ModuleType.Library:
                case ModuleType.Compiled:
                case ModuleType.CompiledBuiltin:
                case ModuleType.Stub when document.PrimaryModule == null:
                    canHaveLibraryAnalysis = true;
                    break;
            }

            lock (_syncObj) {
                var createLibraryAnalysis = false;
                if (!_isCanceled) {
                    node?.MarkWalked();
                    createLibraryAnalysis = canHaveLibraryAnalysis && !document.IsOpen;
                }

                if (node != null) {
                    createLibraryAnalysis &= !node.HasMissingDependencies &&
                                             node.HasOnlyWalkedDependencies &&
                                             node.IsValidVersion;
                }

                if (!createLibraryAnalysis) {
                    return new DocumentAnalysis(document, version, walker.GlobalScope, walker.Eval, walker.StarImportMemberNames);
                }

                if (document.ModuleType != ModuleType.Stub && !_isCanceled) {
                    ast.ReduceToImports();
                    document.SetAst(ast);
                }

                var eval = new ExpressionEval(walker.Eval.Services, document, ast);
                var analysis = new LibraryAnalysis(document, version, walker.GlobalScope, eval, walker.StarImportMemberNames);

                var dbs = _services.GetService<IModuleDatabaseService>();
                dbs?.StoreModuleAnalysisAsync(analysis, immediate: false, _analyzerCancellationToken).DoNotWait();

                return analysis;
            }
        }

        private void LogCompleted(IDependencyChainLoopNode<PythonAnalyzerEntry> node, IEnumerable<IPythonModule> modules, Stopwatch stopWatch, TimeSpan startTime) {
            if (_log != null) {
                var moduleNames = modules.Select(m => "{0}({1})".FormatInvariant(m.Name, m.Analysis is LibraryAnalysis ? "Library" : m.ModuleType.ToString()));
                var elapsed = Math.Round((stopWatch.Elapsed - startTime).TotalMilliseconds, 2);
                var message = $"Analysis of modules loop on depth {node.VertexDepth} in {elapsed} ms:";
                _log.Log(TraceEventType.Verbose, message);
                foreach (var name in moduleNames) {
                    _log.Log(TraceEventType.Verbose, $"    {name}");
                }
            }
        }

        private void LogCompleted(IDependencyChainSingleNode<PythonAnalyzerEntry> node, IPythonModule module, Stopwatch stopWatch, TimeSpan startTime) {
            if (_log != null) {
                var completed = module.Analysis is LibraryAnalysis ? "completed for library" : "completed";
                var elapsed = Math.Round((stopWatch.Elapsed - startTime).TotalMilliseconds, 2);
                var message = $"Analysis of {module.Name} ({module.ModuleType}) on depth {node.VertexDepth} {completed} in {elapsed} ms.";
                _log.Log(TraceEventType.Verbose, message);
            }
        }

        private void LogCompleted(IPythonModule module, Stopwatch stopWatch, TimeSpan startTime) {
            if (_log != null) {
                var elapsed = Math.Round((stopWatch.Elapsed - startTime).TotalMilliseconds, 2);
                var message = $"Out of order analysis of {module.Name}({module.ModuleType}) completed in {elapsed} ms.";
                _log.Log(TraceEventType.Verbose, message);
            }
        }

        private void LogCanceled(IPythonModule module) {
            if (_log != null) {
                _log.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) canceled.");
            }
        }

        private void LogException(PythonAnalyzerEntry entry, Exception exception) {
            if (_log != null) {
                _log.Log(TraceEventType.Verbose, $"Analysis of {entry.Module.Name}({entry.Module.ModuleType}) failed. {exception}");
            }

            if (TestEnvironment.Current != null) {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private void LogException(IDependencyChainLoopNode<PythonAnalyzerEntry> node, Exception exception) {
            if (_log != null) {
                var moduleNames = string.Join(", ", node.Values.Select(e => $"{e.Module.Name}({e.Module.ModuleType})"));
                var message = $"Analysis of modules loop [{moduleNames}] failed. {exception}";
                _log.Log(TraceEventType.Verbose, message);
            }

            if (TestEnvironment.Current != null) {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private enum State {
            NotStarted = 0,
            Started = 1,
            Completed = 2
        }
    }
}
