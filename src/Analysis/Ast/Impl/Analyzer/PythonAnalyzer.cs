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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public sealed class PythonAnalyzer : IPythonAnalyzer, IDisposable {
        private readonly IServiceManager _services;
        private readonly IDependencyResolver<ModuleKey, AnalysisEntry> _dependencyResolver;
        private readonly Dictionary<ModuleKey, AnalysisEntry> _analysisEntries = new Dictionary<ModuleKey, AnalysisEntry>();
        private readonly DisposeToken _disposeToken = DisposeToken.Create<PythonAnalyzer>();
        private readonly object _syncObj = new object();
        private readonly AsyncManualResetEvent _analysisCompleteEvent = new AsyncManualResetEvent();
        private readonly ILogger _log;
        private readonly int _maxTaskRunning = 8;
        private int _runningTasks;
        private int _version;

        public PythonAnalyzer(IServiceManager services) {
            _services = services;
            _log = services.GetService<ILogger>();
            _dependencyResolver = new DependencyResolver<ModuleKey, AnalysisEntry>(new ModuleDependencyFinder(services.GetService<IFileSystem>()));
            _analysisCompleteEvent.Set();
        }

        public void Dispose() => _disposeToken.TryMarkDisposed();

        public Task WaitForCompleteAnalysisAsync(CancellationToken cancellationToken = default)
            => _analysisCompleteEvent.WaitAsync(cancellationToken);

        public void EnqueueDocumentForAnalysis(IPythonModule module, PythonAst ast, int version, CancellationToken cancellationToken)
            => AnalyzeDocumentAsync(module, ast, version, cancellationToken).DoNotWait();

        public async Task<IDocumentAnalysis> GetAnalysisAsync(IPythonModule module, int waitTime, CancellationToken cancellationToken) {
            var key = new ModuleKey(module);
            AnalysisEntry entry;
            lock (_syncObj) {
                if (!_analysisEntries.TryGetValue(key, out entry)) {
                    return new EmptyAnalysis(_services, (IDocument)module);
                }
            }

            using (var timeoutCts = new CancellationTokenSource()) {
                if (waitTime >= 0 && !Debugger.IsAttached) {
                    timeoutCts.CancelAfter(waitTime);
                }

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken)) {
                    var timeoutToken = timeoutCts.Token;
                    while (!timeoutToken.IsCancellationRequested) {
                        try {
                            var analysis = await entry.GetAnalysisAsync(cts.Token);
                            lock (_syncObj) {
                                if (entry.Version == analysis.Version) {
                                    return analysis;
                                }
                            }
                        } catch (OperationCanceledException) when (!timeoutToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
                            lock (_syncObj) {
                                if (!_analysisEntries.TryGetValue(key, out entry)) {
                                    return new EmptyAnalysis(_services, (IDocument)module);
                                }
                            }
                        }
                    }
                }
            }

            return entry.PreviousAnalysis;
        }

        private async Task AnalyzeDocumentAsync(IPythonModule module, PythonAst ast, int version, CancellationToken cancellationToken) {
            var key = new ModuleKey(module);
            AnalysisEntry entry;
            lock (_syncObj) {
                if (_analysisEntries.TryGetValue(key, out entry)) {
                    entry.Invalidate(_version);
                } else {
                    _analysisEntries[key] = entry = new AnalysisEntry(module, ast, new EmptyAnalysis(_services, (IDocument)module), _version);
                }
            }

            _analysisCompleteEvent.Reset();
            _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) queued");

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.CancellationToken, cancellationToken)) {
                var analysisToken = cts.Token;
 
                var walker = await _dependencyResolver.AddChangesAsync(new ModuleKey(module), entry, version, cts.Token);
                var abortAnalysisOnVersionChange = true;
                lock (_syncObj) {
                    if (_version < walker.Version) {
                        _version = walker.Version;
                        foreach (var affectedEntry in walker.AffectedValues) {
                            affectedEntry.Invalidate(_version);
                            if (affectedEntry.UserNotAnalyzed) {
                                abortAnalysisOnVersionChange = false;
                            }
                        }
                    }
                }

                if (walker.MissingKeys.Count > 0) {
                    LoadMissingDocuments(module.Interpreter, walker.MissingKeys);
                }

                var stopWatch = Stopwatch.StartNew();
                IDependencyChainNode<AnalysisEntry> node;
                while ((node = await walker.GetNextAsync(analysisToken)) != null) {
                    lock (_syncObj) {
                        if (_version > walker.Version) {
                            if (abortAnalysisOnVersionChange) {
                                break;
                            }

                            if (!node.Value.UserNotAnalyzed) {
                                node.MarkCompleted();
                                continue;
                            }
                        }
                    }
                    
                    if (Interlocked.Increment(ref _runningTasks) >= _maxTaskRunning) {
                        await AnalyzeAsync(node, walker.Version, stopWatch, analysisToken);
                    } else {
                        StartAnalysis(node, walker.Version, stopWatch, analysisToken);
                    }
                }

                stopWatch.Stop();

                if (walker.MissingKeys.Count == 0) {
                    Interlocked.Exchange(ref _runningTasks, 0);
                    _analysisCompleteEvent.Set();
                }
            }
        }

        private static void LoadMissingDocuments(IPythonInterpreter interpreter, ImmutableArray<ModuleKey> missingKeys) {
            foreach (var (moduleName, _, isTypeshed) in missingKeys) {
                var moduleResolution = isTypeshed ? interpreter.TypeshedResolution : interpreter.ModuleResolution;
                moduleResolution.GetOrLoadModule(moduleName);
            }
        }

        private void StartAnalysis(IDependencyChainNode<AnalysisEntry> node, int version, Stopwatch stopWatch, CancellationToken cancellationToken) 
            => Task.Run(() => AnalyzeAsync(node, version, stopWatch, cancellationToken), cancellationToken).DoNotWait();

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        private async Task AnalyzeAsync(IDependencyChainNode<AnalysisEntry> node, int version, Stopwatch stopWatch, CancellationToken cancellationToken) {
            try {
                var startTime = stopWatch.ElapsedMilliseconds;
                var module = node.Value.Module;
                var ast = node.Value.Ast;

                // Now run the analysis.
                var walker = new ModuleWalker(_services, module, ast);

                await ast.WalkAsync(walker, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Note that we do not set the new analysis here and rather let
                // Python analyzer to call NotifyAnalysisComplete.
                await walker.CompleteAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var analysis = new DocumentAnalysis((IDocument)module, version, walker.GlobalScope, walker.Eval);

                (module as IAnalyzable)?.NotifyAnalysisComplete(analysis);
                lock (_syncObj) {
                    node.Value.TrySetAnalysis(analysis, version);
                }

                node.MarkCompleted();
                _log?.Log(TraceEventType.Verbose, $"Analysis of {module.Name}({module.ModuleType}) complete in {stopWatch.ElapsedMilliseconds - startTime} ms.");
            } catch (OperationCanceledException oce) {
                lock (_syncObj) {
                    node.Value.TryCancel(oce, version);
                }
            } catch (Exception exception) {
                lock (_syncObj) {
                    node.Value.TrySetException(exception, version);
                }
            } finally {
                Interlocked.Decrement(ref _runningTasks);
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
                IsTypeshed = module.ModuleType == ModuleType.Stub;
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

        private sealed class AnalysisEntry {
            private TaskCompletionSource<IDocumentAnalysis> _analysisTcs;

            public IPythonModule Module { get; }
            public PythonAst Ast { get; }
            public IDocumentAnalysis PreviousAnalysis { get; private set; }
            public int Version { get; private set; }
            public bool UserNotAnalyzed => PreviousAnalysis is EmptyAnalysis && Module.ModuleType == ModuleType.User;

            public AnalysisEntry(IPythonModule module, PythonAst ast, IDocumentAnalysis previousAnalysis, int version) {
                Module = module;
                Ast = ast;
                PreviousAnalysis = previousAnalysis;

                Version = version;
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken) 
                => _analysisTcs.Task.ContinueWith(t => t.GetAwaiter().GetResult(), cancellationToken);

            public void TrySetAnalysis(IDocumentAnalysis analysis, int version) {
                if (version >= Version) {
                    _analysisTcs.TrySetResult(analysis);
                }
            }

            public void TrySetException(Exception ex, int version) {
                if (version >= Version) {
                    _analysisTcs.TrySetException(ex);
                }
            }

            public void TryCancel(OperationCanceledException oce, int version) {
                if (version >= Version) {
                    _analysisTcs.TrySetCanceled(oce.CancellationToken);
                }
            }

            public void Invalidate(int version) {
                if (Version >= version) {
                    return;
                }
                Version = version;

                if (!_analysisTcs.TrySetCanceled() && _analysisTcs.Task.Status == TaskStatus.RanToCompletion) {
                    PreviousAnalysis = _analysisTcs.Task.Result;
                }
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>();
            }
        }

        private sealed class ModuleDependencyFinder : IDependencyFinder<ModuleKey, AnalysisEntry> {
            private readonly IFileSystem _fileSystem;

            public ModuleDependencyFinder(IFileSystem fileSystem) {
                _fileSystem = fileSystem;
            }

            public Task<ImmutableArray<ModuleKey>> FindDependenciesAsync(AnalysisEntry value, CancellationToken cancellationToken) {
                var dependencies = new HashSet<ModuleKey>();
                var module = value.Module;
                var isTypeshed = module.ModuleType == ModuleType.Stub; // TODO: This is not the correct way to determine Typeshed.
                var moduleResolution = module.Interpreter.ModuleResolution;
                var pathResolver = isTypeshed
                    ? module.Interpreter.TypeshedResolution.CurrentPathResolver
                    : moduleResolution.CurrentPathResolver;

                if (module.Stub != null) {
                    dependencies.Add(new ModuleKey(module.Stub));
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
