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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class DocumentAnalysis : IDocumentAnalysis {
        public DocumentAnalysis(IDocument document) {
            Check.ArgumentNotNull(nameof(document), document);
            Document = document;
            GlobalScope = new EmptyGlobalScope(document);
        }

        public static async Task<IDocumentAnalysis> CreateAsync(IDocument document, CancellationToken cancellationToken) {
            var da = new DocumentAnalysis(document);
            await da.AnalyzeAsync(cancellationToken);
            return da;
        }

        public IDocument Document { get; }
        public IGlobalScope GlobalScope { get; private set; }
        public IReadOnlyDictionary<string, IMember> Members => GlobalScope.Variables;
        public IEnumerable<IPythonType> GetAllAvailableItems(SourceLocation location) => Enumerable.Empty<IPythonType>();
        public IEnumerable<IMember> GetMembers(SourceLocation location) => Enumerable.Empty<IMember>();
        public IEnumerable<IPythonFunctionOverload> GetSignatures(SourceLocation location) => Enumerable.Empty<IPythonFunctionOverload>();
        public IEnumerable<IPythonType> GetValues(SourceLocation location) => Enumerable.Empty<IPythonType>();

        private async Task AnalyzeAsync(CancellationToken cancellationToken) {
            var ast = await Document.GetAstAsync(cancellationToken);
            var walker = new AstAnalysisWalker(Document, ast, suppressBuiltinLookup: false);
            ast.Walk(walker);
            GlobalScope = walker.Complete();
        }
    }
}
