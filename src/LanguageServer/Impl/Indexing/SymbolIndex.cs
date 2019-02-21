﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal sealed class SymbolIndex : ISymbolIndex {
        private readonly ConcurrentDictionary<string, IMostRecentDocumentSymbols> _index;
        private readonly IFileSystem _fileSystem;
        private readonly PythonLanguageVersion _version;
        private readonly IIndexParser _indexParser;

        public SymbolIndex(IFileSystem fileSystem, PythonLanguageVersion version) {
            _fileSystem = fileSystem;
            _version = version;

            var comparer = PathEqualityComparer.Instance;
            _index = new ConcurrentDictionary<string, IMostRecentDocumentSymbols>(comparer);
            _indexParser = new IndexParser(_fileSystem, _version);
        }

        public Task<IReadOnlyList<HierarchicalSymbol>> HierarchicalDocumentSymbolsAsync(string path, CancellationToken ct = default) {
            if (_index.TryGetValue(path, out var mostRecentSymbols)) {
                return mostRecentSymbols.GetSymbolsAsync(ct);
            } else {
                return Task.FromResult<IReadOnlyList<HierarchicalSymbol>>(new List<HierarchicalSymbol>());
            }
        }

        public async Task<IReadOnlyList<FlatSymbol>> WorkspaceSymbolsAsync(string query, int maxLength, CancellationToken ct = default) {

            var results = new List<FlatSymbol>();

            foreach (var filePathAndRecent in _index) {
                var symbols = await filePathAndRecent.Value.GetSymbolsAsync(ct);
                results.AddRange(WorkspaceSymbolsQuery(filePathAndRecent.Key, query, symbols).Take(maxLength - results.Count));
                ct.ThrowIfCancellationRequested();
                if (results.Count >= maxLength) {
                    break;
                }
            }
            return results;
        }

        public void Add(string path, IDocument doc) {
            _index.GetOrAdd(path, MakeMostRecentDocSymbols(path)).Index(doc);
        }

        public void Parse(string path) {
            var mostRecentSymbols = _index.GetOrAdd(path, MakeMostRecentDocSymbols(path));
            mostRecentSymbols.Parse();
        }

        public void Delete(string path) {
            _index.Remove(path, out var mostRecentDocSymbols);
            mostRecentDocSymbols.Dispose();
        }

        public bool IsIndexed(string path) => _index.ContainsKey(path);

        public void ReIndex(string path, IDocument doc) {
            if (_index.TryGetValue(path, out var currentSymbols)) {
                currentSymbols.Index(doc);
            }
        }

        public void MarkAsPending(string path) {
            _index[path].MarkAsPending();
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbolsQuery(string path, string query,
            IReadOnlyList<HierarchicalSymbol> symbols) {
            var rootSymbols = DecorateWithParentsName(symbols, null);
            var treeSymbols = rootSymbols.TraverseBreadthFirst((symAndPar) => {
                var sym = symAndPar.symbol;
                return DecorateWithParentsName((sym.Children ?? Enumerable.Empty<HierarchicalSymbol>()).ToList(), sym.Name);
            });
            return treeSymbols.Where(sym => sym.symbol.Name.ContainsOrdinal(query, ignoreCase: true))
                              .Select(sym => new FlatSymbol(sym.symbol.Name, sym.symbol.Kind, path, sym.symbol.SelectionRange, sym.parentName));
        }


        private static IEnumerable<(HierarchicalSymbol symbol, string parentName)> DecorateWithParentsName(
            IEnumerable<HierarchicalSymbol> symbols, string parentName) {
            return symbols.Select((symbol) => (symbol, parentName)).ToList();
        }

        private IMostRecentDocumentSymbols MakeMostRecentDocSymbols(string path) {
            return new MostRecentDocumentSymbols(path, _fileSystem, _version, _indexParser);
        }

        public void Dispose() {
            foreach (var recentSymbols in _index.Values) {
                recentSymbols.Dispose();
            }
        }
    }
}
