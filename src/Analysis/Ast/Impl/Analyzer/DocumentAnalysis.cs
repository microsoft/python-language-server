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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class DocumentAnalysis : IDocumentAnalysis {
        private readonly IServiceContainer _services;

        public DocumentAnalysis(IDocument document, IServiceContainer services) {
            Check.ArgumentNotNull(nameof(document), document);
            Check.ArgumentNotNull(nameof(services), services);
            _services = services;
            Document = document;
            GlobalScope = new EmptyGlobalScope(document);
        }

        public static async Task<IDocumentAnalysis> CreateAsync(IDocument document, IServiceContainer services, CancellationToken cancellationToken) {
            var da = new DocumentAnalysis(document, services);
            await da.AnalyzeAsync(cancellationToken);
            return da;
        }

        public IDocument Document { get; }
        public IGlobalScope GlobalScope { get; private set; }

        public IVariableCollection TopLevelMembers => GlobalScope.Variables;
        public IEnumerable<IVariable> AllMembers 
            => (GlobalScope as IScope)
                .TraverseBreadthFirst(s => s.Children)
                .SelectMany(s => s.Variables);

        public IEnumerable<IPythonType> GetAllAvailableItems(SourceLocation location) => Enumerable.Empty<IPythonType>();
        public IEnumerable<IPythonType> GetMembers(SourceLocation location) => Enumerable.Empty<IPythonType>();
        public IEnumerable<IPythonFunctionOverload> GetSignatures(SourceLocation location) => Enumerable.Empty<IPythonFunctionOverload>();
        public IEnumerable<IPythonType> GetValues(SourceLocation location) => Enumerable.Empty<IPythonType>();

        private async Task AnalyzeAsync(CancellationToken cancellationToken) {
            var a = (Document as IAnalyzable) ?? throw new InvalidOperationException("Object must implement IAnalyzable to be analyzed.");
            GlobalScope = await a.AnalyzeAsync(cancellationToken);
        }
    }
}
