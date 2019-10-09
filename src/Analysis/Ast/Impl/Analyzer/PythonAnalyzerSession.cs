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
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Text;
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
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Testing;
using Microsoft.Python.Core.Text;
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
        private readonly IDiagnosticsService _diagnosticsService;
        private readonly IOSPlatform _platformService;
        private readonly IProgressReporter _progress;
        private readonly IPythonAnalyzer _analyzer;
        private readonly ILogger _log;
        private readonly bool _forceGC;
        private readonly IModuleDatabaseService _moduleDatabaseService;

        private State _state;
        private bool _isCanceled;
        private int _runningTasks;
        private PathResolverSnapshot _modulesPathResolver;
        private PathResolverSnapshot _typeshedPathResolver;

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
            _analyzer = _services.GetService<IPythonAnalyzer>();
            _log = _services.GetService<ILogger>();
            _moduleDatabaseService = _services.GetService<IModuleDatabaseService>();
            _progress = progress;

            var interpreter = _services.GetService<IPythonInterpreter>();
            _modulesPathResolver = interpreter.ModuleResolution.CurrentPathResolver;
            _typeshedPathResolver = interpreter.TypeshedResolution.CurrentPathResolver;
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
            var originalRemaining = _walker.Remaining;
            var remaining = originalRemaining;
            try {
                _log?.Log(TraceEventType.Verbose, $"Analysis version {Version} of {originalRemaining} entries has started.");
                remaining = await AnalyzeAffectedEntriesAsync(stopWatch);
            } finally {
                stopWatch.Stop();

                bool isCanceled;
                bool isFinal;
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                    _state = State.Completed;
                    isFinal = _walker.MissingKeys.Count == 0 && !isCanceled && remaining == 0;
                    _walker = null;
                }

                if (!isCanceled) {
                    _progress.ReportRemaining(remaining);
                    if (isFinal) {
                        var (modulesCount, totalMilliseconds) = ActivityTracker.EndTracking();
                        totalMilliseconds = Math.Round(totalMilliseconds, 2);
                        (_analyzer as PythonAnalyzer)?.RaiseAnalysisComplete(modulesCount, totalMilliseconds);
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
            var ace = new AsyncCountdownEvent(0);

            bool isCanceled;
            while ((node = await _walker.GetNextAsync(_analyzerCancellationToken)) != null) {
                lock (_syncObj) {
                    isCanceled = _isCanceled;
                }

                if (isCanceled) {
                    switch (node) {
                        case IDependencyChainLoopNode<PythonAnalyzerEntry> loop when loop.Values.All(e => !e.NotAnalyzed):
                        case IDependencyChainSingleNode<PythonAnalyzerEntry> single when !single.Value.NotAnalyzed:
                            remaining++;
                            node.MoveNext();
                            continue;
                    }
                }

                var taskLimitReached = false;
                lock (_syncObj) {
                    _runningTasks++;
                    taskLimitReached = _runningTasks >= _maxTaskRunning || _walker.Remaining == 1;
                }

                if (taskLimitReached) {
                    RunAnalysis(node, stopWatch);
                } else {
                    ace.AddOne();
                    StartAnalysis(node, ace, stopWatch).DoNotWait();
                }
            }

            await ace.WaitAsync(_analyzerCancellationToken);

            lock (_syncObj) {
                if (_walker.MissingKeys.Count == 0 || _walker.MissingKeys.All(k => k.IsTypeshed)) {
                    Debug.Assert(_runningTasks == 0);
                } else if (!_isCanceled && _log != null && _log.LogLevel >= TraceEventType.Verbose) {
                    _log?.Log(TraceEventType.Verbose, $"Missing keys: {string.Join(", ", _walker.MissingKeys)}");
                }
            }

            return remaining;
        }
        
        private void RunAnalysis(IDependencyChainNode node, Stopwatch stopWatch) 
            => ExecutionContext.Run(ExecutionContext.Capture(), s => Analyze(node, null, stopWatch), null);

        private Task StartAnalysis(IDependencyChainNode node, AsyncCountdownEvent ace, Stopwatch stopWatch)
            => Task.Run(() => Analyze(node, ace, stopWatch));

        private void Analyze(IDependencyChainNode node, AsyncCountdownEvent ace, Stopwatch stopWatch) {
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
                            AnalyzeLoop(loop, stopWatch);
                        } catch (OperationCanceledException oce) {
                            //loop.Value.TryCancel(oce, _walker.Version);
                            //LogCanceled(single.Value.Module);
                        } catch (Exception exception) {
                            //loop.Value.TrySetException(exception, _walker.Version);
                            node.MarkWalked();
                            LogException(loop, exception);
                        }

                        break;
                }
            } finally {
                node.MoveNext();

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

            foreach (var entry in loopNode.Values) {
                if (!CanUpdateAnalysis(entry, Version, out var module, out var ast)) {
                    _log?.Log(TraceEventType.Verbose, $"Analysis of loop canceled.");
                    return;
                }

                var moduleKey = new AnalysisModuleKey(module);
                entries[moduleKey] = (module, entry);
                var analysis = TryRestoreCachedAnalysis(module);
                if (analysis != default) {
                    AddLoopImportsFromCachedAnalysis(importNames, variables, moduleKey, analysis);
                    cachedVariables.Add(new AnalysisModuleKey(module), analysis.GlobalScope.Variables);
                } else {
                    AddLoopImportsFromAst(importNames, variables, moduleKey, ast, module.ModuleType == ModuleType.Compiled || module.ModuleType == ModuleType.CompiledBuiltin);
                    asts.Add(new AnalysisModuleKey(module), ast);
                }
            }

            lock (_syncObj) {
                if (_isCanceled) {
                    return;
                }
            }

            if (asts.Count == 0) {
                if (_log != null && _log.LogLevel == TraceEventType.Verbose) {
                    var names = string.Join(", ", cachedVariables.Select(v => v.Key.Name));
                    _log?.Log(TraceEventType.Verbose, $"Attempt to reanalyze fully cached modules cycle: {names}");
                }
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
            lock (_syncObj) {
                if (_isCanceled) {
                    return;
                }
            }

            var variableHandler = new LoopImportedVariableHandler(_services, asts, cachedVariables, IsCanceled);
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
                var entry = entries[new AnalysisModuleKey(module)].Entry;

                var analysis = CreateAnalysis(null, module, walker.Ast, version, walker);
                CompleteAnalysis(entry, module, version, analysis);
            }

            if (!MarkNodeWalked(loopNode)) {
                LogCompleted(loopNode, entries.Values.Select(v => v.Module), stopWatch, startTime);
            }

            bool IsCanceled() {
                lock (_syncObj) {
                    return _isCanceled;
                }
            }
        }

        private void AddLoopImportsFromCachedAnalysis(in List<(AnalysisModuleKey From, int FromPosition, AnalysisModuleKey To, string ToName)> unresolvedImports, 
            in Dictionary<(AnalysisModuleKey Module, string Name), int> variables, 
            in AnalysisModuleKey moduleKey, 
            in IDocumentAnalysis analysis) {
            
            foreach (var variable in analysis.GlobalScope.Variables) {
                var key = (moduleKey, variable.Name);
                var location = variable.Location.IndexSpan.Start;
                if (!variables.TryGetValue(key, out var currentLocation) || currentLocation > location) {
                    variables[key] = location;
                }
            }
        }

        private void AddLoopImportsFromAst(
            in List<(AnalysisModuleKey From, int FromPosition, AnalysisModuleKey To, string ToName)> imports,
            in Dictionary<(AnalysisModuleKey Module, string Name), int> variables,
            in AnalysisModuleKey moduleKey,
            in PythonAst ast, 
            in bool isCompiledModule) {

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

            // Don't add imports from compiled modules
            if (isCompiledModule) {
                return;
            }

            foreach (var (toModule, name, location) in walker.Imports) {
                imports.Add((moduleKey, location.Start, toModule, name));
            }
        }

        private bool CanUpdateAnalysis(PythonAnalyzerEntry entry, int version, out IPythonModule module, out PythonAst ast) {
            if (entry.CanUpdateAnalysis(version, out module, out ast, out var currentAnalysis)) {
                return true;
            }

            if (ast == default) {
                if (currentAnalysis == default) {
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
            var analysis = RestoreOrAnalyzeModule(node, module, ast, version);
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

        private IDocumentAnalysis RestoreOrAnalyzeModule(IDependencyChainSingleNode<PythonAnalyzerEntry> node, IPythonModule module, PythonAst ast, int version) {
            var analysis = TryRestoreCachedAnalysis(module);
            if (analysis != null) {
                MarkNodeWalked(node);
                return analysis;
            }

            var eval = new ExpressionEval(_services, module, ast);
            var walker = new ModuleWalker(eval, SimpleImportedVariableHandler.Instance);
            ast.Walk(walker);
            walker.Complete();
            return CreateAnalysis(node, (IDocument)module, ast, version, walker);
        }

        private bool MarkNodeWalked(IDependencyChainNode node) {
            bool isCanceled;
            lock (_syncObj) {
                isCanceled = _isCanceled;
            }
            if (!isCanceled) {
                node?.MarkWalked();
            }
            return isCanceled;
        }

        private IDocumentAnalysis TryRestoreCachedAnalysis(IPythonModule module) {
            var moduleType = module.ModuleType;
            if (!moduleType.CanBeCached() || _moduleDatabaseService == null || !_moduleDatabaseService.ModuleExistsInStorage(module.Name, module.FilePath)) {
                return null;
            }

            if (_moduleDatabaseService.TryRestoreGlobalScope(module, out var gs)) {
                _log?.Log(TraceEventType.Verbose, "Restored from database: ", module.Name);
                var analysis = new DocumentAnalysis((IDocument)module, 1, gs, new ExpressionEval(_services, module, module.GetAst()), Array.Empty<string>());
                gs.ReconstructVariables();
                return analysis;
            }

            _log?.Log(TraceEventType.Verbose, "Restore from database failed for module ", module.Name);

            return null;
        }

        private IDocumentAnalysis CreateAnalysis(IDependencyChainSingleNode<PythonAnalyzerEntry> node, IDocument document, PythonAst ast, int version, ModuleWalker walker) {
            var canHaveLibraryAnalysis = false;

            // Don't try to drop builtins; it causes issues elsewhere.
            // We probably want the builtin module's AST and other info for evaluation.
            switch (document.ModuleType) {
                case ModuleType.Library:
                case ModuleType.Compiled:
                case ModuleType.CompiledBuiltin:
                    canHaveLibraryAnalysis = true;
                    break;
            }

            var isCanceled = MarkNodeWalked(node);
            var createLibraryAnalysis = !isCanceled &&
                                        node != null &&
                                        !node.HasMissingDependencies &&
                                        canHaveLibraryAnalysis &&
                                        !document.IsOpen &&
                                        node.HasOnlyWalkedDependencies &&
                                        node.IsValidVersion;

            if (!createLibraryAnalysis) {
                return new DocumentAnalysis(document, version, walker.GlobalScope, walker.Eval, walker.StarImportMemberNames);
            }

            ast.Reduce(x => x is ImportStatement || x is FromImportStatement);
            document.SetAst(ast);

            var eval = new ExpressionEval(walker.Eval.Services, document, ast);
            var analysis = new LibraryAnalysis(document, version, walker.GlobalScope, eval, walker.StarImportMemberNames);

            var dbs = _services.GetService<IModuleDatabaseService>();
            dbs?.StoreModuleAnalysisAsync(analysis, CancellationToken.None).DoNotWait();

            return analysis;
        }

        private void LogCompleted(IDependencyChainLoopNode<PythonAnalyzerEntry> node, IEnumerable<IPythonModule> modules, Stopwatch stopWatch, TimeSpan startTime) {
            if (_log != null) {
                var moduleNames = string.Join(", ", modules.Select(m => "{0}({1})".FormatInvariant(m.Name, m.Analysis is LibraryAnalysis ? "Library" : m.ModuleType.ToString())));
                var elapsed = Math.Round((stopWatch.Elapsed - startTime).TotalMilliseconds, 2);
                var message = $"Analysis of modules loop on depth {node.VertexDepth} in {elapsed} ms: [{moduleNames}]";
                _log.Log(TraceEventType.Verbose, message);
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
