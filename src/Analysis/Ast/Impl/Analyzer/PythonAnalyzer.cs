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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Shell;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    public sealed class PythonAnalyzer: IPythonAnalyzer {
        private readonly IDependencyResolver _dependencyResolver;

        public PythonAnalyzer(IServiceContainer services) {
            Interpreter = services.GetService<IPythonInterpreter>();
            _dependencyResolver = services.GetService<IDependencyResolver>();
        }

        public IPythonInterpreter Interpreter { get; }

        public IEnumerable<string> TypeStubDirectories => throw new NotImplementedException();

        /// <summary>
        /// Analyze single document.
        /// </summary>
        public Task AnalyzeDocumentAsync(IDocument document, CancellationToken cancellationToken) {
            if(!(document is IAnalyzable a)) {
                return;
            }

            a.NotifyAnalysisPending();

        }

        /// <summary>
        /// Analyze document with dependents.
        /// </summary>
        public async Task AnalyzeDocumentDependencyChainAsync(IDocument document, CancellationToken cancellationToken) {
            if (!(document is IAnalyzable a)) {
                return;
            }

            var dependencyRoot = await _dependencyResolver.GetDependencyChainAsync(document, cancellationToken);
            // Notify each dependency that the analysis is now pending
            NotifyAnalysisPending(dependencyRoot);


            CheckDocumentVersionMatch(node, cancellationToken);
        }

        private void NotifyAnalysisPending(IDependencyChainNode node) {
            node.Analyzable.NotifyAnalysisPending();
            foreach (var c in node.Children) {
                NotifyAnalysisPending(c);
            }
        }

        private void AnalyzeChainAsync(IDependencyChainNode node, CancellationToken cancellationToken) {
            Task.Run(async () => {
                if (node.Analyzable is IDocument doc) {
                    await Analyze(doc, cancellationToken);
                    foreach (var c in node.Children) {
                        await EnqueueAsync(c, cancellationToken);
                    }
                }
            });
        }

        private Task AnalyzeAsync(IDependencyChainNode node, CancellationToken cancellationToken)
            => Task.Run(() => {
                var analysis = Analyze(node.Document);
                if(!node.Analyzable.NotifyAnalysisComplete(analysis)) {
                    throw new OperationCanceledException();
                }
            });

        private IDocumentAnalysis Analyze(IDocument document) {
        }

        private void CheckDocumentVersionMatch(IDependencyChainNode node, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Analyzable.ExpectedAnalysisVersion != node.SnapshotVersion) {
            }
        }

    }
}
