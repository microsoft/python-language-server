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
using Microsoft.Python.Analysis.Dependencies;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AnalysisQueue : IAnalysisQueue {
        private readonly IPythonAnalyzer _analyzer;

        public AnalysisQueue(IPythonAnalyzer analyzer) {
            _analyzer = analyzer;
        }

        public async Task EnqueueAsync(IDependencyChainNode node, CancellationToken cancellationToken) {
            CheckDocumentVersionMatch(node, cancellationToken);
            await _analyzer.AnalyzeAsync(node.Document, cancellationToken);
            foreach (var c in node.Children) {
                await EnqueueAsync(c, cancellationToken);
            }
        }

        private void CheckDocumentVersionMatch(IDependencyChainNode node, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Document.Version != node.DocumentVersion) {
                throw new OperationCanceledException();
            }
        }
    }
}
