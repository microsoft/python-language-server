using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.LanguageServer.Indexing {
    interface IMostRecentDocumentSymbols : IDisposable {
        void Parse();
        void Add(IDocument doc);
        void ReIndex(IDocument doc);
        Task<IReadOnlyList<HierarchicalSymbol>> GetSymbolsAsync();
        void MarkAsPending();
    }
}
