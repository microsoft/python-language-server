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
        private ModuleType _moduleType;
        private PythonAst _ast;
        private IDocumentAnalysis _previousAnalysis;
        private HashSet<AnalysisModuleKey> _parserDependencies;
        private HashSet<AnalysisModuleKey> _analysisDependencies;
        private int _bufferVersion;
        private int _analysisVersion;

        public IPythonModule Module {
            get {
                lock (_syncObj) {
                    return _module;
                }
            }
        }

        public bool IsUserOrBuiltin {
            get {
                lock (_syncObj) {
                    return _moduleType == ModuleType.User || _moduleType == ModuleType.Builtins;
                }
            }
        }

        public bool IsUserModule {
            get {
                lock (_syncObj) {
                    return _moduleType == ModuleType.User;
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

        public bool NotAnalyzed => PreviousAnalysis is EmptyAnalysis;
        
        public PythonAnalyzerEntry(EmptyAnalysis emptyAnalysis) {
            _previousAnalysis = emptyAnalysis;
            _module = emptyAnalysis.Document;
            _moduleType = _module.ModuleType;

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

                if (module == null) {
                    return false;
                }

                if (ast == null) {
                    Debug.Assert(!(_previousAnalysis is LibraryAnalysis), $"Library module {module.Name} of type {module.ModuleType} has been analyzed already!");
                    return false;
                }

                return _previousAnalysis is EmptyAnalysis || _moduleType == ModuleType.User || _analysisVersion <= version;
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

                if (analysis is LibraryAnalysis) {
                    _ast = null;
                    _parserDependencies = null;
                }

                _analysisDependencies = null;
                UpdateAnalysisTcs(version);
                _previousAnalysis = analysis;
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
                _moduleType = module.ModuleType;
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
            if (_bufferVersion > bufferVersion) {
                return new HashSet<AnalysisModuleKey>();
            }

            var walker = new DependencyWalker(module);
            ast.Walk(walker);
            var dependencies = walker.Dependencies;
            dependencies.Remove(new AnalysisModuleKey(module));
            return dependencies;
        }

        private static bool Ignore(IModuleManagement moduleResolution, string fullName, string modulePath)
            => moduleResolution.BuiltinModuleName.EqualsOrdinal(fullName) || moduleResolution.GetSpecializedModule(fullName, modulePath) != null;

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

        private class DependencyWalker : PythonWalker {
            private readonly IPythonModule _module;
            private readonly bool _isTypeshed;
            private readonly IModuleManagement _moduleResolution;
            private readonly PathResolverSnapshot _pathResolver;

            public HashSet<AnalysisModuleKey> Dependencies { get; }

            public DependencyWalker(IPythonModule module) {
                _module = module;
                _isTypeshed = module is StubPythonModule stub && stub.IsTypeshed;
                _moduleResolution = module.Interpreter.ModuleResolution;
                _pathResolver = _isTypeshed
                    ? module.Interpreter.TypeshedResolution.CurrentPathResolver
                    : _moduleResolution.CurrentPathResolver;

                Dependencies = new HashSet<AnalysisModuleKey>();

                if (module.Stub != null) {
                    Dependencies.Add(new AnalysisModuleKey(module.Stub));
                }
            }

            public override bool Walk(ImportStatement import) {
                var forceAbsolute = import.ForceAbsolute;
                foreach (var moduleName in import.Names) {
                    var importNames = ImmutableArray<string>.Empty;
                    foreach (var nameExpression in moduleName.Names) {
                        importNames = importNames.Add(nameExpression.Name);
                        var imports = _pathResolver.GetImportsFromAbsoluteName(_module.FilePath, importNames, forceAbsolute);
                        HandleSearchResults(imports);
                    }
                }

                return false;
            }

            public override bool Walk(FromImportStatement fromImport) {
                var imports = _pathResolver.FindImports(_module.FilePath, fromImport);
                HandleSearchResults(imports);
                if (imports is IImportChildrenSource childrenSource) {
                    foreach (var name in fromImport.Names) {
                        if (childrenSource.TryGetChildImport(name.Name, out var childImport)) {
                            HandleSearchResults(childImport);
                        }
                    }
                }

                return false;
            }

            private void HandleSearchResults(IImportSearchResult searchResult) {
                switch (searchResult) {
                    case ModuleImport moduleImport when !Ignore(_moduleResolution, moduleImport.FullName, moduleImport.ModulePath):
                        Dependencies.Add(new AnalysisModuleKey(moduleImport.FullName, moduleImport.ModulePath, _isTypeshed));
                        return;
                    case PossibleModuleImport possibleModuleImport when !Ignore(_moduleResolution, possibleModuleImport.PrecedingModuleFullName, possibleModuleImport.PrecedingModulePath):
                        Dependencies.Add(new AnalysisModuleKey(possibleModuleImport.PrecedingModuleFullName, possibleModuleImport.PrecedingModulePath, _isTypeshed));
                        return;
                    default:
                        return;
                }
            }
        }
    }
}
