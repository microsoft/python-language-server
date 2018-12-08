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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class DocumentAnalysis : IDocumentAnalysis {
        /// <summary>Top-level module members: global functions, variables and classes.</summary>
        private readonly ConcurrentDictionary<string, IMember> _members = new ConcurrentDictionary<string, IMember>();

        public DocumentAnalysis(IDocument document) {
            Check.ArgumentNotNull(nameof(document), document);
            Document = document;
        }

        public static async Task<IDocumentAnalysis> CreateAsync(IDocument document, CancellationToken cancellationToken) {
            var da = new DocumentAnalysis(document);
            await da.AnalyzeAsync(cancellationToken);
            return da;
        }

        public IDocument Document { get; }

        public IEnumerable<IPythonType> GetAllAvailableItems(SourceLocation location) {
            return Enumerable.Empty<IPythonType>();
        }

        public IEnumerable<IPythonType> GetMembers(SourceLocation location) {
            return Enumerable.Empty<IPythonType>();
        }

        public IEnumerable<IPythonFunctionOverload> GetSignatures(SourceLocation location) {
            return Enumerable.Empty<IPythonFunctionOverload>();
        }

        public IEnumerable<IPythonType> GetValues(SourceLocation location) {
            return Enumerable.Empty<IPythonType>();
        }

        private async Task AnalyzeAsync(CancellationToken cancellationToken) {
            var ast = await Document.GetAstAsync(cancellationToken);
            var walker = new AstAnalysisWalker(Document, ast, suppressBuiltinLookup: false);
            ast.Walk(walker);
            walker.Complete();
        }
    }
}
