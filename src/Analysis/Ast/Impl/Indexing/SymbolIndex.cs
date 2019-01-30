﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Indexing {
    internal sealed class SymbolIndex : ISymbolIndex {
        private readonly ConcurrentDictionary<string, IReadOnlyList<HierarchicalSymbol>> _index;

        public SymbolIndex(IEqualityComparer<string> comparer = null) {
            comparer = comparer ?? PathEqualityComparer.Instance;
            _index = new ConcurrentDictionary<string, IReadOnlyList<HierarchicalSymbol>>(comparer);
        }

        public IEnumerable<HierarchicalSymbol> HierarchicalDocumentSymbols(string path)
            => _index.TryGetValue(path, out var list) ? list : Enumerable.Empty<HierarchicalSymbol>();

        public IEnumerable<FlatSymbol> WorkspaceSymbols(string query) {
            return _index.SelectMany(kvp => WorkspaceSymbolsQuery(query, kvp.Key, kvp.Value));
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbolsQuery(string query, string path, IEnumerable<HierarchicalSymbol> symbols) {
            var rootSymbols = DecorateWithParentsName(symbols, null);
            var treeSymbols = rootSymbols.TraverseBreadthFirst((symbolAndParent) => {
                var sym = symbolAndParent.symbol;
                return DecorateWithParentsName(sym.Children.MaybeEnumerate(), sym.Name);
            });
            foreach (var (sym, parentName) in treeSymbols) {
                if (sym.Name.ContainsOrdinal(query, ignoreCase: true)) {
                    yield return new FlatSymbol(sym.Name, sym.Kind, new Uri(path), sym.SelectionRange, parentName);
                }
            }
        }

        public void UpdateIndex(string path, PythonAst ast) {
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            _index[path] = walker.Symbols;
        }

        public void Delete(string path) => _index.TryRemove(path, out var _);

        public bool IsIndexed(string path) => _index.ContainsKey(path);

        private static IEnumerable<(HierarchicalSymbol symbol, string parentName)> DecorateWithParentsName(IEnumerable<HierarchicalSymbol> symbols, string parentName)
            => symbols.Select((symbol) => (symbol, parentName));
    }
}
