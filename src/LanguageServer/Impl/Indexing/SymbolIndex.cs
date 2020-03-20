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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal sealed class SymbolIndex : ISymbolIndex {
        private readonly DisposableBag _disposables = new DisposableBag(nameof(SymbolIndex));
        private readonly ConcurrentDictionary<string, IMostRecentDocumentSymbols> _index;
        private readonly IIndexParser _indexParser;
        private readonly bool _libraryMode;

        public SymbolIndex(IFileSystem fileSystem, PythonLanguageVersion version, bool libraryMode = false) {
            _index = new ConcurrentDictionary<string, IMostRecentDocumentSymbols>(PathEqualityComparer.Instance);
            _indexParser = new IndexParser(fileSystem, version);
            _libraryMode = libraryMode;

            _disposables
                .Add(_indexParser)
                .Add(() => {
                    foreach (var recentSymbols in _index.Values) {
                        recentSymbols.Dispose();
                    }
                });
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
            _index.GetOrAdd(path, MakeMostRecentDocSymbols(path)).Parse();
        }

        public void Delete(string path) {
            _index.Remove(path, out var mostRecentDocSymbols);
            mostRecentDocSymbols.Dispose();
        }

        public void ReIndex(string path, IDocument doc) {
            if (_index.TryGetValue(path, out var currentSymbols)) {
                currentSymbols.Index(doc);
            }
        }

        public void MarkAsPending(string path) {
            if (_index.TryGetValue(path, out var mostRecentDocSymbols)) {
                mostRecentDocSymbols.MarkAsPending();
            }
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbolsQuery(string path, string query, IReadOnlyList<HierarchicalSymbol> symbols) {
            var treeSymbols = DecorateWithParentsName(symbols, null)
                .TraverseBreadthFirst(sym => DecorateWithParentsName(sym.symbol.Children ?? Enumerable.Empty<HierarchicalSymbol>(), sym.symbol.Name));

            // Prioritize exact matches, then substrings, then fuzzy matches. The caller is in charge of limiting this.
            return treeSymbols.Where(sym => sym.symbol.Name.EqualsIgnoreCase(query))
                .Concat(treeSymbols.Where(sym => sym.symbol.Name.ContainsOrdinal(query, true)))
                .Concat(treeSymbols.Where(sym => FuzzyMatch(query, sym.symbol.Name)))
                .DistinctBy(sym => sym.symbol) // Deduplicate by reference; we're iterating over the exact same objects.
                .Select(sym => new FlatSymbol(sym.symbol.Name, sym.symbol.Kind, path, sym.symbol.SelectionRange, sym.parentName, sym.symbol._existInAllVariable));
        }

        private IMostRecentDocumentSymbols MakeMostRecentDocSymbols(string path) => new MostRecentDocumentSymbols(path, _indexParser, _libraryMode);

        public void Dispose() {
            _disposables.TryDispose();
        }

        private static IEnumerable<(HierarchicalSymbol symbol, string parentName)> DecorateWithParentsName(IEnumerable<HierarchicalSymbol> symbols, string parentName)
            => symbols.Select((symbol) => (symbol, parentName));

        private static bool FuzzyMatch(string pattern, string name) {
            var patternPos = 0;
            var namePos = 0;

            while (patternPos < pattern.Length && namePos < name.Length) {
                if (char.ToLowerInvariant(pattern[patternPos]) == char.ToLowerInvariant(name[namePos])) {
                    patternPos++;
                }
                namePos++;
            }

            return patternPos == pattern.Length;
        }
    }
}
