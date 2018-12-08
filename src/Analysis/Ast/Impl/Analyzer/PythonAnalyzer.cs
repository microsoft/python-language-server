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
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Analysis.Analyzer {
    public sealed class PythonAnalyzer : IPythonAnalyzer {
        private readonly IDependencyResolver _dependencyResolver;

        public PythonAnalyzer(IPythonInterpreter interpreter, IDependencyResolver dependencyResolver) {
            Interpreter = interpreter;
            _dependencyResolver = dependencyResolver;
        }

        public IPythonInterpreter Interpreter { get; }

        /// <summary>
        /// Analyze single document.
        /// </summary>
        public async Task<IDocumentAnalysis> AnalyzeDocumentAsync(IDocument document, CancellationToken cancellationToken) {
            if (!(document is IAnalyzable a)) {
                return null;
            }
            a.NotifyAnalysisPending();
            var version = a.ExpectedAnalysisVersion;
            var analysis = await AnalyzeAsync(document, cancellationToken);
            return a.NotifyAnalysisComplete(analysis, version) ? analysis : null;
        }

        /// <summary>
        /// Analyze document with dependents.
        /// </summary>
        public async Task AnalyzeDocumentDependencyChainAsync(IDocument document, CancellationToken cancellationToken) {
            Check.InvalidOperation(() => _dependencyResolver != null, "Dependency resolver must be provided for the group analysis.");

            if (document is IAnalyzable) {
                var dependencyRoot = await _dependencyResolver.GetDependencyChainAsync(document, cancellationToken);
                // Notify each dependency that the analysis is now pending
                NotifyAnalysisPending(dependencyRoot);
                await AnalyzeChainAsync(dependencyRoot, cancellationToken);
            }
        }

        private void NotifyAnalysisPending(IDependencyChainNode node) {
            node.Analyzable.NotifyAnalysisPending();
            foreach (var c in node.Children) {
                NotifyAnalysisPending(c);
            }
        }

        private async Task AnalyzeChainAsync(IDependencyChainNode node, CancellationToken cancellationToken) {
            var analysis = await AnalyzeAsync(node.Document, cancellationToken);
            if (!node.Analyzable.NotifyAnalysisComplete(analysis, node.SnapshotVersion)) {
                // If snapshot does not match, there is no reason to continue analysis along the chain
                // since subsequent change that incremented the expected version will start
                // another analysis run.
                throw new OperationCanceledException();
            }
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var c in node.Children) {
                await AnalyzeChainAsync(c, cancellationToken);
            }
        }

        private Task<IDocumentAnalysis> AnalyzeAsync(IDocument document, CancellationToken cancellationToken)
            => DocumentAnalysis.CreateAsync(document, cancellationToken);
    }
}
