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
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{_module.Name}({_module.ModuleType})")]
    internal sealed class PythonAnalyzerEntry {
        private readonly object _syncObj = new object();
        private TaskCompletionSource<IDocumentAnalysis> _analysisTcs;
        private IPythonModule _module;
        private bool _isUserModule;
        private PythonAst _ast;
        private IDocumentAnalysis _previousAnalysis;
        private HashSet<AnalysisModuleKey> _parserDependencies;
        private HashSet<AnalysisModuleKey> _analysisDependencies;
        private int _bufferVersion;
        private int _analysisVersion;
        private int _depth;

        public IPythonModule Module {
            get {
                lock (_syncObj) {
                    return _module;
                }
            }
        }

        public bool IsUserModule {
            get {
                lock (_syncObj) {
                    return _isUserModule;
                }
            }
        }

        public IDocumentAnalysis PreviousAnalysis {
            get {
                lock (_syncObj) {
                    return _previousAnalysis;
                }
            }
        }
        
        public int BufferVersion => _bufferVersion;

        public int AnalysisVersion {
            get {
                lock (_syncObj) {
                    return _analysisVersion;
                }
            }
        }

        public int Depth {
            get {
                lock (_syncObj) {
                    return _depth;
                }
            }
        }

        public bool NotAnalyzed => PreviousAnalysis is EmptyAnalysis;
        
        public PythonAnalyzerEntry(EmptyAnalysis emptyAnalysis) {
            _previousAnalysis = emptyAnalysis;
            _module = emptyAnalysis.Document;
            _isUserModule = emptyAnalysis.Document.ModuleType == ModuleType.User;
            _depth = _isUserModule ? 0 : -1;

            _bufferVersion = -1;
            _analysisVersion = 0;
            _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken) 
            => _analysisTcs.Task.ContinueWith(t => t.GetAwaiter().GetResult(), cancellationToken);

        public bool IsValidVersion(int version, out IPythonModule module, out PythonAst ast) {
            lock (_syncObj) {
                module = _module;
                ast = _ast;
                if (ast == null || module == null) {
                    return false;
                }

                return _previousAnalysis is EmptyAnalysis || _isUserModule || _analysisVersion <= version;
            }
        }

        public void SetDepth(int version, int depth) {
            lock (_syncObj) {
                if (_analysisVersion > version) {
                    return;
                }

                _depth = _depth == -1 ? depth : Math.Min(_depth, depth);
            }
        }

        public void TrySetAnalysis(IDocumentAnalysis analysis, int version) {
            lock (_syncObj) {
                if (_previousAnalysis is EmptyAnalysis) {
                    _previousAnalysis = analysis;
                }

                if (_analysisVersion > version) {
                    return;
                }

                _analysisDependencies = null;
                UpdateAnalysisTcs(version);
            }

            _analysisTcs.TrySetResult(analysis);
        }

        public void TrySetException(Exception ex, int version) {
            lock (_syncObj) {
                if (_analysisVersion > version) {
                    return;
                }

                _analysisVersion = version;
            }

            _analysisTcs.TrySetException(ex);
        }

        public void TryCancel(OperationCanceledException oce, int version) {
            lock (_syncObj) {
                if (_analysisVersion > version) {
                    return;
                }

                _analysisVersion = version;
            }
            
            _analysisTcs.TrySetCanceled(oce.CancellationToken);
        }

        public void Invalidate(int analysisVersion) {
            lock (_syncObj) {
                if (_analysisVersion >= analysisVersion || !_analysisTcs.Task.IsCompleted) {
                    return;
                }

                UpdateAnalysisTcs(analysisVersion);
            }
        }

        public bool Invalidate(ImmutableArray<IPythonModule> analysisDependencies, int analysisVersion, out ImmutableArray<AnalysisModuleKey> dependencies) {
            dependencies = ImmutableArray<AnalysisModuleKey>.Empty;
            IPythonModule module;
            int version;
            lock (_syncObj) {
                if (_analysisVersion >= analysisVersion) {
                    return false;
                }

                version = _analysisVersion;
                module = _module;
            }

            var dependenciesHashSet = new HashSet<AnalysisModuleKey>();
            foreach (var dependency in analysisDependencies) {
                if (dependency != module && (dependency.ModuleType == ModuleType.User && dependency.Analysis.Version < version || dependency.Analysis is EmptyAnalysis)) {
                    dependenciesHashSet.Add(new AnalysisModuleKey(dependency));
                }
            }

            if (dependenciesHashSet.Count == 0) {
                return false;
            }

            lock (_syncObj) {
                if (_analysisVersion >= analysisVersion) {
                    return false;
                }

                if (_analysisDependencies == null) {
                    _analysisDependencies = dependenciesHashSet;
                } else {
                    var countBefore = _analysisDependencies.Count;
                    _analysisDependencies.UnionWith(dependenciesHashSet);
                    if (countBefore == _analysisDependencies.Count) {
                        return false;
                    }
                }

                UpdateAnalysisTcs(analysisVersion);
                dependencies = _parserDependencies != null
                    ? ImmutableArray<AnalysisModuleKey>.Create(_parserDependencies.Union(_analysisDependencies).ToArray())
                    : ImmutableArray<AnalysisModuleKey>.Create(_analysisDependencies);
                return true;
            }
        }

        public bool Invalidate(IPythonModule module, PythonAst ast, int bufferVersion, int analysisVersion, out ImmutableArray<AnalysisModuleKey> dependencies) {
            dependencies = ImmutableArray<AnalysisModuleKey>.Empty;
            if (_bufferVersion >= bufferVersion) {
                return false;
            }

            var dependenciesHashSet = FindDependencies(module, ast, bufferVersion);
            lock (_syncObj) {
                if (_analysisVersion >= analysisVersion && _bufferVersion >= bufferVersion) {
                    return false;
                }

                _ast = ast;
                _module = module;
                _isUserModule = module.ModuleType == ModuleType.User;
                _parserDependencies = dependenciesHashSet;

                Interlocked.Exchange(ref _bufferVersion, bufferVersion);
                UpdateAnalysisTcs(analysisVersion);
                dependencies = _analysisDependencies != null 
                    ? ImmutableArray<AnalysisModuleKey>.Create(_parserDependencies.Union(_analysisDependencies).ToArray())
                    : ImmutableArray<AnalysisModuleKey>.Create(_parserDependencies);
                return true;
            }
        }

        private HashSet<AnalysisModuleKey> FindDependencies(IPythonModule module, PythonAst ast, int bufferVersion) {
            var isTypeshed = module is StubPythonModule stub && stub.IsTypeshed;
            var moduleResolution = module.Interpreter.ModuleResolution;
            var pathResolver = isTypeshed
                ? module.Interpreter.TypeshedResolution.CurrentPathResolver
                : moduleResolution.CurrentPathResolver;

            var dependencies = new HashSet<AnalysisModuleKey>();

            if (module.Stub != null) {
                dependencies.Add(new AnalysisModuleKey(module.Stub));
            }

            foreach (var node in ast.TraverseDepthFirst<Node>(n => n.GetChildNodes())) {
                if (_bufferVersion > bufferVersion) {
                    return dependencies;
                }

                switch (node) {
                    case ImportStatement import:
                        foreach (var moduleName in import.Names) {
                            HandleSearchResults(isTypeshed, dependencies, moduleResolution, pathResolver.FindImports(module.FilePath, moduleName, import.ForceAbsolute));
                        }
                        break;
                    case FromImportStatement fromImport:
                        var imports = pathResolver.FindImports(module.FilePath, fromImport);
                        HandleSearchResults(isTypeshed, dependencies, moduleResolution, imports);
                        if (imports is IImportChildrenSource childrenSource) {
                            foreach (var name in fromImport.Names) {
                                if (childrenSource.TryGetChildImport(name.Name, out var childImport)) {
                                    HandleSearchResults(isTypeshed, dependencies, moduleResolution, childImport);
                                }
                            }
                        }
                        break;
                }
            }

            dependencies.Remove(new AnalysisModuleKey(module));
            return dependencies;
        }

        private static void HandleSearchResults(bool isTypeshed, HashSet<AnalysisModuleKey> dependencies, IModuleManagement moduleResolution, IImportSearchResult searchResult) {
            switch (searchResult) {
                case ModuleImport moduleImport when !Ignore(moduleResolution, moduleImport.FullName):
                    dependencies.Add(new AnalysisModuleKey(moduleImport.FullName, moduleImport.ModulePath, isTypeshed));
                    return;
                case PossibleModuleImport possibleModuleImport when !Ignore(moduleResolution, possibleModuleImport.PrecedingModuleFullName):
                    dependencies.Add(new AnalysisModuleKey(possibleModuleImport.PrecedingModuleFullName, possibleModuleImport.PrecedingModulePath, isTypeshed));
                    return;
                default:
                    return;
            }
        }

        private static bool Ignore(IModuleManagement moduleResolution, string name)
            => moduleResolution.BuiltinModuleName.EqualsOrdinal(name) || moduleResolution.GetSpecializedModule(name) != null;

        private void UpdateAnalysisTcs(int analysisVersion) {
            _analysisVersion = analysisVersion;
            if (_analysisTcs.Task.Status == TaskStatus.RanToCompletion) {
                _previousAnalysis = _analysisTcs.Task.Result;
                _analysisDependencies = null;
            }

            if (_analysisTcs.Task.IsCompleted) {
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
