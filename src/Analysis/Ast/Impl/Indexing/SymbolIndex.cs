using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Indexing {
    internal sealed class SymbolIndex : ISymbolIndex {
        private readonly ConcurrentDictionary<Uri, IReadOnlyList<HierarchicalSymbol>> _index = new ConcurrentDictionary<Uri, IReadOnlyList<HierarchicalSymbol>>();
        private bool _empty;
        
        public SymbolIndex() {
            _empty = false;
        }
        
        public bool isNotEmpty() {
            return _empty;
        }


        public IEnumerable<HierarchicalSymbol> HierarchicalDocumentSymbols(Uri uri)
            => _index.TryGetValue(uri, out var list) ? list : Enumerable.Empty<HierarchicalSymbol>();

        public IEnumerable<FlatSymbol> WorkspaceSymbols(string query) {
            foreach (var kvp in _index) {
                foreach (var found in WorkspaceSymbolsQuery(query, kvp.Key, kvp.Value)) {
                    yield return found;
                }
            }
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbolsQuery(string query, Uri uri, IEnumerable<HierarchicalSymbol> symbols) {
            // Some semblance of a BFS.
            var queue = new Queue<(HierarchicalSymbol, string)>(symbols.Select(s => (s, (string)null)));

            while (queue.Count > 0) {
                var (sym, parent) = queue.Dequeue();

                if (sym.Name.ContainsOrdinal(query, ignoreCase: true)) {
                    yield return new FlatSymbol(sym.Name, sym.Kind, uri, sym.SelectionRange, parent);
                }

                foreach (var child in sym.Children.MaybeEnumerate()) {
                    queue.Enqueue((child, sym.Name));
                }
            }
        }

        public void UpdateIndex(Uri uri, PythonAst ast) {
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            _index[uri] = walker.Symbols;
        }
    }
}
