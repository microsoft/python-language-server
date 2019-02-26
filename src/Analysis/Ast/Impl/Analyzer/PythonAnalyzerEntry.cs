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
        private TaskCompletionSource<IDocumentAnalysis> _analysisTcs;

        public IPythonModule Module { get; }
        public PythonAst Ast { get; private set; }
        public IDocumentAnalysis PreviousAnalysis { get; private set; }
        public ImmutableArray<IPythonModule> AnalysisDependencies { get; private set; }
        public int BufferVersion { get; private set; }
        public int AnalysisVersion { get; private set; }
        public bool NotAnalyzed => PreviousAnalysis is EmptyAnalysis;

        public PythonAnalyzerEntry(IPythonModule module, PythonAst ast, IDocumentAnalysis previousAnalysis, int bufferVersion, int analysisVersion) {
            Module = module;
            Ast = ast;
            PreviousAnalysis = previousAnalysis;
            AnalysisDependencies = ImmutableArray<IPythonModule>.Empty;

            BufferVersion = bufferVersion;
            AnalysisVersion = analysisVersion;
            _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken) 
            => _analysisTcs.Task.ContinueWith(t => t.GetAwaiter().GetResult(), cancellationToken);

        public void TrySetAnalysis(IDocumentAnalysis analysis, int version, object syncObj) {
            lock (syncObj) {
                if (NotAnalyzed) {
                    PreviousAnalysis = analysis;
                }

                if (AnalysisVersion > version) {
                    return;
                }

                AnalysisDependencies = ImmutableArray<IPythonModule>.Empty;
                UpdateAnalysisTcs(version);
            }

            _analysisTcs.TrySetResult(analysis);
        }

        public void TrySetException(Exception ex, int version, object syncObj) {
            lock (syncObj) {
                if (AnalysisVersion > version) {
                    return;
                }

                AnalysisVersion = version;
            }

            _analysisTcs.TrySetException(ex);
        }

        public void TryCancel(OperationCanceledException oce, int version, object syncObj) {
            lock (syncObj) {
                if (AnalysisVersion > version) {
                    return;
                }

                AnalysisVersion = version;
            }
            
            _analysisTcs.TrySetCanceled(oce.CancellationToken);
        }

        public void Invalidate(int analysisVersion)
            => Invalidate(Ast, BufferVersion, analysisVersion);

        public void AddAnalysisDependencies(ImmutableArray<IPythonModule> dependencies) {
            AnalysisDependencies = AnalysisDependencies.AddRange(dependencies);
        }

        public void Invalidate(PythonAst ast, int bufferVersion, int analysisVersion) {
            if (AnalysisVersion >= analysisVersion && BufferVersion >= bufferVersion) {
                return;
            }

            Ast = ast;
            BufferVersion = bufferVersion;
            
            UpdateAnalysisTcs(analysisVersion);
        }

        private void UpdateAnalysisTcs(int analysisVersion) {
            AnalysisVersion = analysisVersion;
            if (_analysisTcs.Task.Status == TaskStatus.RanToCompletion) {
                PreviousAnalysis = _analysisTcs.Task.Result;
                AnalysisDependencies = ImmutableArray<IPythonModule>.Empty;
            }

            if (_analysisTcs.Task.IsCompleted) {
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}
