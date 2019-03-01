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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Documents;
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
        private readonly IDependencyResolver<ModuleKey, PythonAnalyzerEntry> _dependencyResolver;
        private readonly Dictionary<ModuleKey, PythonAnalyzerEntry> _analysisEntries = new Dictionary<ModuleKey, PythonAnalyzerEntry>();
        private readonly DisposeToken _disposeToken = DisposeToken.Create<PythonAnalyzer>();
        private readonly object _syncObj = new object();
        private readonly AsyncManualResetEvent _analysisCompleteEvent = new AsyncManualResetEvent();
        private readonly AsyncAutoResetEvent _analysisRunningEvent = new AsyncAutoResetEvent();
        private readonly ProgressReporter _progress;
        private readonly ILogger _log;
        private readonly int _maxTaskRunning = 8;
        private int _runningTasks;
        private int _version;

        public PythonAnalyzer(IServiceManager services) {
            _services = services;
            _log = services.GetService<ILogger>();
            _progress = new ProgressReporter(services.GetService<IProgressService>());
            _dependencyResolver = new DependencyResolver<ModuleKey, PythonAnalyzerEntry>(new ModuleDependencyFinder());
            _analysisCompleteEvent.Set();
            _analysisRunningEvent.Set();
        }

        public void Dispose() {
            _progress.Dispose();
            _disposeToken.TryMarkDisposed();
        }

        public Task WaitForCompleteAnalysisAsync(CancellationToken cancellationToken = default)
            => _analysisCompleteEvent.WaitAsync(cancellationToken);

        public async Task<IDocumentAnalysis> GetAnalysisAsync(IPythonModule module, int waitTime, CancellationToken cancellationToken) {
            var key = new ModuleKey(module);
            PythonAnalyzerEntry entry;
            lock (_syncObj) {
                if (!_analysisEntries.TryGetValue(key, out entry)) {
                    var emptyAnalysis = new EmptyAnalysis(_services, (IDocument)module);
                    entry = new PythonAnalyzerEntry(module, emptyAnalysis.Ast, emptyAnalysis, -1, 0);
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
                if (_analysisEntries.TryGetValue(new ModuleKey(module), out var entry)) {
                    entry.Invalidate(_version + 1);
                }
            }
        }

        public void EnqueueDocumentForAnalysis(IPythonModule module, ImmutableArray<IPythonModule> dependencies) {
            var key = new ModuleKey(module);
            PythonAnalyzerEntry entry;
            lock (_syncObj) {
                if (!_analysisEntries.TryGetValue(key, out entry)) {
                    return;
                }

                var significantDependencies = dependencies.Where(IsSignificant);
                if (significantDependencies.Count == 0) {
                    return;
                }

                entry.Invalidate(_version + 1);
                entry.AddAnalysisDependencies(significantDependencies);
            }

            AnalyzeDocumentAsync(key, entry, default).DoNotWait();

            bool IsSignificant(IPythonModule m)
                => m != entry.Module && ((m.ModuleType == ModuleType.User && m.Analysis.Version < entry.AnalysisVersion) || m.Analysis is EmptyAnalysis);
        }

        public void EnqueueDocumentForAnalysis(IPythonModule module, PythonAst ast, int bufferVersion, CancellationToken cancellationToken) {
            var key = new ModuleKey(module);
            PythonAnalyzerEntry entry;
            lock (_syncObj) {
                if (_analysisEntries.TryGetValue(key, out entry)) {
                    if (entry.BufferVersion >= bufferVersion) {
                        return;
                    }

                    entry.Invalidate(ast, bufferVersion, _version + 1);
                } else {
                    entry = new PythonAnalyzerEntry(module, ast, new EmptyAnalysis(_services, (IDocument)module), bufferVersion, _version + 1);
                    _analysisEntries[key] = entry;
                }
            }

            AnalyzeDocumentAsync(key, entry, cancellationToken).DoNotWait();
        }

        private async Task AnalyzeDocumentAsync(ModuleKey key, PythonAnalyzerEntry entry, CancellationToken cancellationToken) {
            _analysisCompleteEvent.Reset();
            _log?.Log(TraceEventType.Verbose, $"Analysis of {entry.Module.Name}({entry.Module.ModuleType}) queued");

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.CancellationToken, cancellationToken)) {
                var analysisToken = cts.Token;

                var walker = await _dependencyResolver.AddChangesAsync(key, entry, cts.Token, _progress);
                var stopOnVersionChange = true;
                lock (_syncObj) {
                    if (_version > walker.Version) {
                        return;
                    }

                    _version = walker.Version;
                }

                if (walker.MissingKeys.Count > 0) {
                    LoadMissingDocuments(entry.Module.Interpreter, walker.MissingKeys);
                }

                await _analysisRunningEvent.WaitAsync(cancellationToken);
                var stopWatch = Stopwatch.StartNew();

                try {
                    lock (_syncObj) {
                        foreach (var affectedEntry in walker.AffectedValues) {
                            affectedEntry.Invalidate(_version);
                            if (affectedEntry.NotAnalyzed) {
                                stopOnVersionChange = false;
                            }
                        }

                        if (_version > walker.Version && stopOnVersionChange) {
                            return;
                        }
                    }

                    _log?.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} of {walker.AffectedValues.Count} entries has started.");
                    _progress.ReportRemaining(walker.Remaining);
                    await AnalyzeAffectedEntriesAsync(walker, stopOnVersionChange, stopWatch, analysisToken);
                } finally {
                    _analysisRunningEvent.Set();
                    stopWatch.Stop();
                    _progress.ReportRemaining(walker.Remaining);

                    if (_log != null) {
                        if (walker.Remaining == 0) {
                            _log?.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} has been completed in {stopWatch.Elapsed.TotalMilliseconds} ms.");
                        } else {
                            _log?.Log(TraceEventType.Verbose, $"Analysis version {walker.Version} has been canceled in {stopWatch.Elapsed.TotalMilliseconds} ms with {walker.Remaining} remaining entries.");
                        }
                    }
                }
            }
        }

        private async Task AnalyzeAffectedEntriesAsync(IDependencyChainWalker<ModuleKey, PythonAnalyzerEntry> walker, bool stopOnVersionChange, Stopwatch stopWatch, CancellationToken cancellationToken) {
            IDependencyChainNode<PythonAnalyzerEntry> node;
            while ((node = await walker.GetNextAsync(cancellationToken)) != null) {
                int version;
                lock (_syncObj) {
                    version = _version;
                }

                if (version > walker.Version) {
                    if (stopOnVersionChange) {
                        return;
                    }

                    if (!node.Value.NotAnalyzed) {
                        node.Skip();
                        continue;
                    }
                }

                if (Interlocked.Increment(ref _runningTasks) >= _maxTaskRunning) {
                    Analyze(node, walker.Version, stopWatch, cancellationToken);
                } else {
                    StartAnalysis(node, walker.Version, stopWatch, cancellationToken);
                }
            }

            if (walker.MissingKeys.Where(k => !k.IsTypeshed).Count == 0) {
                Interlocked.Exchange(ref _runningTasks, 0);
                _analysisCompleteEvent.Set();
            }
        }

        private static void LoadMissingDocuments(IPythonInterpreter interpreter, ImmutableArray<ModuleKey> missingKeys) {
            foreach (var (moduleName, _, isTypeshed) in missingKeys) {
                var moduleResolution = isTypeshed ? interpreter.TypeshedResolution : interpreter.ModuleResolution;
                moduleResolution.GetOrLoadModule(moduleName);
            }
        }

        private void StartAnalysis(IDependencyChainNode<PythonAnalyzerEntry> node, int version, Stopwatch stopWatch, CancellationToken cancellationToken)
            => Task.Run(() => Analyze(node, version, stopWatch, cancellationToken), cancellationToken);

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        private void Analyze(IDependencyChainNode<PythonAnalyzerEntry> node, int version, Stopwatch stopWatch, CancellationToken cancellationToken) {
            try {
                var startTime = stopWatch.Elapsed;
                var module = node.Value.Module;
                var ast = node.Value.Ast;

                // Now run the analysis.
                var walker = new ModuleWalker(_services, module, ast);

                ast.Walk(walker);
                cancellationToken.ThrowIfCancellationRequested();

                // Note that we do not set the new analysis here and rather let
                // Python analyzer to call NotifyAnalysisComplete.
                walker.Complete();
                cancellationToken.ThrowIfCancellationRequested();
                var analysis = new DocumentAnalysis((IDocument)module, version, walker.GlobalScope, walker.Eval);

                (module as IAnalyzable)?.NotifyAnalysisComplete(analysis);
                node.Value.TrySetAnalysis(analysis, version, _syncObj);

                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) complete in {(stopWatch.Elapsed - startTime).TotalMilliseconds} ms.");
            } catch (OperationCanceledException oce) {
                node.Value.TryCancel(oce, version, _syncObj);
            } catch (Exception exception) {
                node.Value.TrySetException(exception, version, _syncObj);
            } finally {
                Interlocked.Decrement(ref _runningTasks);
                node.Commit();
            }
        }

        [DebuggerDisplay("{Name} : {FilePath}")]
        private struct ModuleKey : IEquatable<ModuleKey> {
            public string Name { get; }
            public string FilePath { get; }
            public bool IsTypeshed { get; }

            public ModuleKey(IPythonModule module) {
                Name = module.Name;
                FilePath = module.ModuleType == ModuleType.CompiledBuiltin ? null : module.FilePath;
                IsTypeshed = module is StubPythonModule stub && stub.IsTypeshed;
            }

            public ModuleKey(string name, string filePath, bool isTypeshed) {
                Name = name;
                FilePath = filePath;
                IsTypeshed = isTypeshed;
            }

            public bool Equals(ModuleKey other)
                => Name.EqualsOrdinal(other.Name) && FilePath.PathEquals(other.FilePath) && IsTypeshed == other.IsTypeshed;

            public override bool Equals(object obj) => obj is ModuleKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    var hashCode = (Name != null ? Name.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (FilePath != null ? FilePath.GetPathHashCode() : 0);
                    hashCode = (hashCode * 397) ^ IsTypeshed.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(ModuleKey left, ModuleKey right) => left.Equals(right);

            public static bool operator !=(ModuleKey left, ModuleKey right) => !left.Equals(right);

            public void Deconstruct(out string moduleName, out string filePath, out bool isTypeshed) {
                moduleName = Name;
                filePath = FilePath;
                isTypeshed = IsTypeshed;
            }

            public override string ToString() => $"{Name}({FilePath})";
        }

        private sealed class ModuleDependencyFinder : IDependencyFinder<ModuleKey, PythonAnalyzerEntry> {
            public Task<ImmutableArray<ModuleKey>> FindDependenciesAsync(PythonAnalyzerEntry value, CancellationToken cancellationToken) {
                var dependencies = new HashSet<ModuleKey>();
                var module = value.Module;
                var isTypeshed = module is StubPythonModule stub && stub.IsTypeshed;
                var moduleResolution = module.Interpreter.ModuleResolution;
                var pathResolver = isTypeshed
                    ? module.Interpreter.TypeshedResolution.CurrentPathResolver
                    : moduleResolution.CurrentPathResolver;

                if (module.Stub != null) {
                    dependencies.Add(new ModuleKey(module.Stub));
                }

                foreach (var dependency in value.AnalysisDependencies) {
                    dependencies.Add(new ModuleKey(dependency));
                }

                foreach (var node in value.Ast.TraverseDepthFirst<Node>(n => n.GetChildNodes())) {
                    if (cancellationToken.IsCancellationRequested) {
                        return Task.FromCanceled<ImmutableArray<ModuleKey>>(cancellationToken);
                    }

                    switch (node) {
                        case ImportStatement import:
                            foreach (var moduleName in import.Names) {
                                HandleSearchResults(isTypeshed, dependencies, moduleResolution, pathResolver.FindImports(module.FilePath, moduleName, import.ForceAbsolute));
                            }
                            break;
                        case FromImportStatement fromImport:
                            HandleSearchResults(isTypeshed, dependencies, moduleResolution, pathResolver.FindImports(module.FilePath, fromImport));
                            break;
                    }
                }

                dependencies.Remove(new ModuleKey(value.Module));
                return Task.FromResult(ImmutableArray<ModuleKey>.Create(dependencies));
            }

            private static void HandleSearchResults(bool isTypeshed, HashSet<ModuleKey> dependencies, IModuleManagement moduleResolution, IImportSearchResult searchResult) {
                switch (searchResult) {
                    case ModuleImport moduleImport when !Ignore(moduleResolution, moduleImport.FullName):
                        dependencies.Add(new ModuleKey(moduleImport.FullName, moduleImport.ModulePath, isTypeshed));
                        return;
                    case PossibleModuleImport possibleModuleImport when !Ignore(moduleResolution, possibleModuleImport.PrecedingModuleFullName):
                        dependencies.Add(new ModuleKey(possibleModuleImport.PrecedingModuleFullName, possibleModuleImport.PrecedingModulePath, isTypeshed));
                        return;
                    default:
                        return;
                }
            }

            private static bool Ignore(IModuleManagement moduleResolution, string name)
                => moduleResolution.BuiltinModuleName.EqualsOrdinal(name) || moduleResolution.GetSpecializedModule(name) != null;
        }
    }
}
