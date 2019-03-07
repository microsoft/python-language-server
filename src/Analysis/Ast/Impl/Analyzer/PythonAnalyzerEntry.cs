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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class PythonAnalyzerEntry {
        private readonly object _syncObj = new object();
        private TaskCompletionSource<IDocumentAnalysis> _analysisTcs;
        private IPythonModule _module;
        private PythonAst _ast;
        private IDocumentAnalysis _previousAnalysis;
        private ImmutableArray<IPythonModule> _analysisDependencies;
        private int _bufferVersion;
        private int _analysisVersion;

        public IPythonModule Module {
            get {
                lock (_syncObj) {
                    return _module;
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

        public ImmutableArray<IPythonModule> AnalysisDependencies {
            get {
                lock (_syncObj) {
                    return _analysisDependencies;
                }
            }
        }

        public int BufferVersion {
            get {
                lock (_syncObj) {
                    return _bufferVersion;
                }
            }
        }

        public int AnalysisVersion {
            get {
                lock (_syncObj) {
                    return _analysisVersion;
                }
            }
        }

        public bool NotAnalyzed => PreviousAnalysis is EmptyAnalysis;

        public PythonAnalyzerEntry(IPythonModule module, PythonAst ast, IDocumentAnalysis previousAnalysis, int bufferVersion, int analysisVersion) {
            _module = module;
            _ast = ast;
            _previousAnalysis = previousAnalysis;
            _analysisDependencies = ImmutableArray<IPythonModule>.Empty;

            _bufferVersion = bufferVersion;
            _analysisVersion = analysisVersion;
            _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken) 
            => _analysisTcs.Task.ContinueWith(t => t.GetAwaiter().GetResult(), cancellationToken);

        public bool IsValidVersion(int version, out IPythonModule module, out PythonAst ast) {
            lock (_syncObj) {
                module = _module;
                ast = _ast;
                return _analysisVersion <= version;
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

                _analysisDependencies = ImmutableArray<IPythonModule>.Empty;
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
                if (_analysisVersion >= analysisVersion) {
                    return;
                }

                UpdateAnalysisTcs(analysisVersion);
            }
        }

        public void Invalidate(ImmutableArray<IPythonModule> dependencies, int analysisVersion) {
            lock (_syncObj) {
                if (_analysisVersion >= analysisVersion) {
                    return;
                }

                UpdateAnalysisTcs(analysisVersion);
                _analysisDependencies = _analysisDependencies.AddRange(dependencies);
            }
        }

        public void Invalidate(IPythonModule module, PythonAst ast, int bufferVersion, int analysisVersion) {
            lock (_syncObj) {
                if (_analysisVersion >= analysisVersion && _bufferVersion >= bufferVersion) {
                    return;
                }

                _ast = ast;
                _module = module;
                _bufferVersion = bufferVersion;

                UpdateAnalysisTcs(analysisVersion);
            }
        }

        private void UpdateAnalysisTcs(int analysisVersion) {
            _analysisVersion = analysisVersion;
            if (_analysisTcs.Task.Status == TaskStatus.RanToCompletion) {
                _previousAnalysis = _analysisTcs.Task.Result;
                _analysisDependencies = ImmutableArray<IPythonModule>.Empty;
            }

            if (_analysisTcs.Task.IsCompleted) {
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
