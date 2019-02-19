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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class PythonAnalyzerEntry {
        private TaskCompletionSource<IDocumentAnalysis> _analysisTcs;

        public IPythonModule Module { get; }
        public PythonAst Ast { get; private set; }
        public IDocumentAnalysis PreviousAnalysis { get; private set; }
        public int Version { get; private set; }
        public bool UserNotAnalyzed => PreviousAnalysis is EmptyAnalysis && Module.ModuleType == ModuleType.User;

        public PythonAnalyzerEntry(IPythonModule module, PythonAst ast, IDocumentAnalysis previousAnalysis, int version) {
            Module = module;
            Ast = ast;
            PreviousAnalysis = previousAnalysis;

            Version = version;
            _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken) 
            => _analysisTcs.Task.ContinueWith(t => t.GetAwaiter().GetResult(), cancellationToken);

        public void TrySetAnalysis(IDocumentAnalysis analysis, int version, object syncObj) {
            lock (syncObj) {
                if (UserNotAnalyzed) {
                    PreviousAnalysis = analysis;
                }

                if (Version > version) {
                    return;
                }

                Version = version;
            }

            _analysisTcs.TrySetResult(analysis);
        }

        public void TrySetException(Exception ex, int version, object syncObj) {
            lock (syncObj) {
                if (Version > version) {
                    return;
                }

                Version = version;
            }

            _analysisTcs.TrySetException(ex);
        }

        public void TryCancel(OperationCanceledException oce, int version, object syncObj) {
            lock (syncObj) {
                if (Version > version) {
                    return;
                }

                Version = version;
            }
            
            _analysisTcs.TrySetCanceled(oce.CancellationToken);
        }

        public void Invalidate(int version, PythonAst ast) {
            if (Version >= version) {
                return;
            }

            Version = version;
            Ast = ast;
            if (_analysisTcs.Task.Status == TaskStatus.RanToCompletion) {
                PreviousAnalysis = _analysisTcs.Task.Result;
            }

            if (_analysisTcs.Task.IsCompleted) {
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>();
            }
        }
    }
}
