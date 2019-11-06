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
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{_module.Name} : {_module.ModuleType}")]
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

        public PythonAnalyzerEntry(EmptyAnalysis emptyAnalysis) : this(emptyAnalysis.Document, emptyAnalysis) {
            _bufferVersion = -1;
        }

        public PythonAnalyzerEntry(CachedAnalysis analysis) : this(analysis.Document, analysis) {
            _bufferVersion = 0;
            _analysisTcs.SetResult(analysis);
        }

        private PythonAnalyzerEntry(IPythonModule module, IDocumentAnalysis analysis) {
            _previousAnalysis = analysis;
            _module = module;
            _moduleType = _module.ModuleType;

            _analysisVersion = 0;
            _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
        }


        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken)
            => _analysisTcs.Task.WaitAsync(cancellationToken);

        public bool CanUpdateAnalysis(int version, out IPythonModule module, out PythonAst ast, out IDocumentAnalysis currentAnalysis) {
            lock (_syncObj) {
                module = _module;
                ast = _ast;
                currentAnalysis = _analysisTcs.Task.Status == TaskStatus.RanToCompletion ? _analysisTcs.Task.Result : default;

                if (module == null || ast == null) {
                    return false;
                }

                if (_previousAnalysis is EmptyAnalysis || _moduleType == ModuleType.User) {
                    return true;
                }

                return _analysisVersion <= version;
            }
        }

        public void TrySetAnalysis(IDocumentAnalysis analysis, int version) {
            TaskCompletionSource<IDocumentAnalysis> tcs;
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
                tcs = _analysisTcs;
            }

            tcs.TrySetResult(analysis);
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
            foreach (var dependency in analysisDependencies.ExcludeDefault().Where(d => d.ModuleType != ModuleType.Specialized)) {
                if (!dependency.Equals(module) &&
                    (dependency.ModuleType == ModuleType.User && dependency.Analysis.Version < version || dependency.Analysis is EmptyAnalysis)) {
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
            var dependencies = new HashSet<AnalysisModuleKey>();
            if (_bufferVersion > bufferVersion || module.ModuleType == ModuleType.Specialized) {
                return dependencies;
            }

            var dw = new DependencyWalker(_module, ast);
            dependencies.UnionWith(dw.Dependencies);

            dependencies.Remove(new AnalysisModuleKey(module));
            return dependencies;
        }

        private void UpdateAnalysisTcs(int analysisVersion) {
            _analysisVersion = analysisVersion;
            if (_analysisTcs.Task.Status == TaskStatus.RanToCompletion) {
                _previousAnalysis = _analysisTcs.Task.Result;
            }

            if (_analysisTcs.Task.IsCompleted) {
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
