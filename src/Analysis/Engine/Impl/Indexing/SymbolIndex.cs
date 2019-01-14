// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Indexing {
    internal class SymbolIndex : ISymbolIndex {
        private readonly ConcurrentDictionary<Uri, IReadOnlyList<HierarchicalSymbol>> _index = new ConcurrentDictionary<Uri, IReadOnlyList<HierarchicalSymbol>>();

        public SymbolIndex() { }

        public async Task<IEnumerable<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(Uri uri, CancellationToken token = default) {
            return _index.TryGetValue(uri, out var list) ? list : null;
        }

        public Task<IEnumerable<FlatSymbol>> WorkspaceSymbolsAsync(string query, CancellationToken token = default) {
            return Task.FromResult(WorkspaceSymbols(query));
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbols(string query, CancellationToken token = default) {
            foreach (var kvp in _index) {
                foreach (var found in WorkspaceSymbolsQuery(query, kvp.Key, kvp.Value, token: token)) {
                    yield return found;
                }
            }
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbolsQuery(string query, Uri uri, IEnumerable<HierarchicalSymbol> symbols, CancellationToken token = default) {
            // Some semblance of a BFS.
            var ll = new LinkedList<(HierarchicalSymbol, string)>(symbols.Select(s => (s, (string)null)));
            
            while (ll.First != null) {
                token.ThrowIfCancellationRequested();

                var (sym, parent) = ll.First.Value;
                ll.RemoveFirst();

                if (sym.Name.Contains(query)) {
                    yield return new FlatSymbol {
                        Name = sym.Name,
                        Kind = sym.Kind,
                        DocumentUri = uri,
                        Range = sym.SelectionRange,
                        ContainerName = parent,
                    };
                }

                foreach (var child in sym.Children.MaybeEnumerate()) {
                    ll.AddLast((child, sym.Name));
                }
            }
        }

        public void UpdateParseTree(Uri uri, PythonAst ast) {
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            _index[uri] = walker.Symbols;
        }
    }
}
