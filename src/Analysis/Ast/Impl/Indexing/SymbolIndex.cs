using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Indexing {
    internal sealed class SymbolIndex : ISymbolIndex {
        private readonly ConcurrentDictionary<Uri, IReadOnlyList<HierarchicalSymbol>> _index = new ConcurrentDictionary<Uri, IReadOnlyList<HierarchicalSymbol>>();

        public IEnumerable<HierarchicalSymbol> HierarchicalDocumentSymbols(Uri uri)
            => _index.TryGetValue(uri, out var list) ? list : Enumerable.Empty<HierarchicalSymbol>();

        public IEnumerable<FlatSymbol> WorkspaceSymbols(string query) {
            return _index.SelectMany(kvp => WorkspaceSymbolsQuery(query, kvp.Key, kvp.Value));
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbolsQuery(string query, Uri uri, IEnumerable<HierarchicalSymbol> symbols) {
            var rootSymbols = symbols.Select((symbol) => {
                return (symbol, parentName: (string) null);
            });
            var treeSymbols = rootSymbols.TraverseBreadthFirst((symbolAndParent) => {
                var symbol = symbolAndParent.symbol;
                return symbol.Children.MaybeEnumerate().Select((child) => {
                    return (child, symbol.Name);
                });
            });
            foreach (var (sym, parentName) in treeSymbols) {
                if (sym.Name.ContainsOrdinal(query, ignoreCase: true)) {
                    yield return new FlatSymbol(sym.Name, sym.Kind, uri, sym.SelectionRange, parentName);
                }
            }
        }

        public void UpdateIndex(Uri uri, PythonAst ast) {
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            _index[uri] = walker.Symbols;
        }

        public void Delete(Uri uri) => _index.TryRemove(uri, out var _);

        public bool IsIndexed(Uri uri) => _index.ContainsKey(uri);
    }
}
