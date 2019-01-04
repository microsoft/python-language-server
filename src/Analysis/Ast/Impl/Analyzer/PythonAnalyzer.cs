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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Analyzer {
    public sealed class PythonAnalyzer : IPythonAnalyzer, IDisposable {
        private readonly IServiceContainer _services;
        private readonly IDependencyResolver _dependencyResolver;
        private readonly CancellationTokenSource _globalCts = new CancellationTokenSource();
        private readonly ILogger _log;

        public PythonAnalyzer(IServiceContainer services) {
            _services = services;
            _log = services.GetService<ILogger>();
            _dependencyResolver = services.GetService<IDependencyResolver>();
        }

        public void Dispose() => _globalCts.Cancel();

        /// <summary>
        /// Analyze single document.
        /// </summary>
        public async Task AnalyzeDocumentAsync(IDocument document, CancellationToken cancellationToken) {
            var node = new DependencyChainNode(document);
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, cancellationToken)) {
                node.Analyzable.NotifyAnalysisPending();
                var analysis = await AnalyzeAsync(node, cts.Token).ConfigureAwait(false);
                node.Analyzable.NotifyAnalysisComplete(analysis);
            }
        }

        /// <summary>
        /// Analyze document with dependents.
        /// </summary>
        public async Task AnalyzeDocumentDependencyChainAsync(IDocument document, CancellationToken cancellationToken) {
            Check.InvalidOperation(() => _dependencyResolver != null, "Dependency resolver must be provided for the group analysis.");

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, cancellationToken)) {
                var dependencyRoot = await _dependencyResolver.GetDependencyChainAsync(document, cts.Token).ConfigureAwait(false);
                // Notify each dependency that the analysis is now pending
                NotifyAnalysisPending(dependencyRoot);

                cts.Token.ThrowIfCancellationRequested();
                await AnalyzeChainAsync(dependencyRoot, cts.Token).ConfigureAwait(false);
            }
        }

        private void NotifyAnalysisPending(IDependencyChainNode node) {
            node.Analyzable.NotifyAnalysisPending();
            foreach (var c in node.Children) {
                NotifyAnalysisPending(c);
            }
        }

        private async Task AnalyzeChainAsync(IDependencyChainNode node, CancellationToken cancellationToken) {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, cancellationToken)) {
                var analysis = await AnalyzeAsync(node, cts.Token).ConfigureAwait(false);

                NotifyAnalysisComplete(node, analysis);
                cts.Token.ThrowIfCancellationRequested();

                foreach (var c in node.Children) {
                    await AnalyzeChainAsync(c, cts.Token).ConfigureAwait(false);
                }
            }
        }

        private static void NotifyAnalysisComplete(IDependencyChainNode node, IDocumentAnalysis analysis) {
            if (!node.Analyzable.NotifyAnalysisComplete(analysis)) {
                // If snapshot does not match, there is no reason to continue analysis along the chain
                // since subsequent change that incremented the expected version will start
                // another analysis run.
                throw new OperationCanceledException();
            }
        }

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        private async Task<IDocumentAnalysis> AnalyzeAsync(IDependencyChainNode node, CancellationToken cancellationToken) {
            var _startTime = DateTime.Now;

            _log?.Log(TraceEventType.Verbose, $"Analysis begins: {node.Document.Name}({node.Document.ModuleType})");
            // Store current expected version so we can see if it still 
            // the same at the time the analysis completes.
            var analysisVersion = node.Analyzable.ExpectedAnalysisVersion;

            // Make sure the file is parsed ans the AST is up to date.
            var ast = await node.Document.GetAstAsync(cancellationToken);
            _log?.Log(TraceEventType.Verbose, $"Parse of {node.Document.Name}({node.Document.ModuleType}) complete in {(DateTime.Now - _startTime).TotalMilliseconds} ms.");

            // Now run the analysis.
            var walker = new ModuleWalker(_services, node.Document, ast);

            await ast.WalkAsync(walker, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Note that we do not set the new analysis here and rather let
            // Python analyzer to call NotifyAnalysisComplete.
            var gs = await walker.CompleteAsync(cancellationToken);
            _log?.Log(TraceEventType.Verbose, $"Analysis of {node.Document.Name}({node.Document.ModuleType}) complete in {(DateTime.Now - _startTime).TotalMilliseconds} ms.");
            return new DocumentAnalysis(node.Document, analysisVersion, gs);
        }

    }
}
