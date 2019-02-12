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
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Indexing {
    internal sealed class SymbolIndex : ISymbolIndex {
        private static int DefaultVersion = 0;
        private readonly ConcurrentDictionary<string, VersionedValue<IReadOnlyList<HierarchicalSymbol>>> _index;

        public SymbolIndex(IEqualityComparer<string> comparer = null) {
            comparer = comparer ?? PathEqualityComparer.Instance;
            _index = new ConcurrentDictionary<string, VersionedValue<IReadOnlyList<HierarchicalSymbol>>>(comparer);
        }

        public IEnumerable<HierarchicalSymbol> HierarchicalDocumentSymbols(string path)
            => _index.TryGetValue(path, out var list) ? list.Value : Enumerable.Empty<HierarchicalSymbol>();

        public IEnumerable<FlatSymbol> WorkspaceSymbols(string query) {
            return _index.SelectMany(kvp => WorkspaceSymbolsQuery(query, kvp.Key, kvp.Value.Value));
        }

        private IEnumerable<FlatSymbol> WorkspaceSymbolsQuery(string query, string path,
            IEnumerable<HierarchicalSymbol> symbols) {
            var rootSymbols = DecorateWithParentsName(symbols, null);
            var treeSymbols = rootSymbols.TraverseBreadthFirst((symbolAndParent) => {
                var sym = symbolAndParent.symbol;
                return DecorateWithParentsName(sym.Children.MaybeEnumerate(), sym.Name);
            });
            foreach (var (sym, parentName) in treeSymbols) {
                if (sym.Name.ContainsOrdinal(query, ignoreCase: true)) {
                    yield return new FlatSymbol(sym.Name, sym.Kind, path, sym.SelectionRange, parentName);
                }
            }
        }

        public void UpdateIndexIfNewer(string path, PythonAst ast, int version) {
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            var versionedList = _index.GetOrAdd(path, MakeVersionedValue(walker.Symbols, version));
            versionedList.UpdateIfNewer(walker.Symbols, version);
        }

        public void DeleteIfNewer(string path, int version) {
            var currentVersion = _index[path].Version;
            if (version <= currentVersion) return;
            // Point A
            _index.Remove(path, out var oldVersionedValue);
            // Point B
            if (oldVersionedValue.Version > version) {
                // An update happened at point A
                // Reinsert that version, only if key doesn't exist
                _index.GetOrAdd(path, oldVersionedValue);
                // Update could have happened at B
            }
        }

        public bool IsIndexed(string path) => _index.ContainsKey(path);

        private static IEnumerable<(HierarchicalSymbol symbol, string parentName)> DecorateWithParentsName(
            IEnumerable<HierarchicalSymbol> symbols, string parentName)
            => symbols.Select((symbol) => (symbol, parentName));

        public int GetNewVersion(string path) {
            var versionedList =
                _index.GetOrAdd(path, MakeVersionedValue(new List<HierarchicalSymbol>(), DefaultVersion));
            return versionedList.GetNewVersion();
        }

        private VersionedValue<IReadOnlyList<HierarchicalSymbol>> MakeVersionedValue(
            IReadOnlyList<HierarchicalSymbol> symbols, int version) {
            return new VersionedValue<IReadOnlyList<HierarchicalSymbol>>(symbols, version);
        }

        internal class VersionedValue<T> {
            private readonly object _syncObj = new object();
            private int _newVersion;

            public VersionedValue(T value, int version) {
                Value = value;
                Version = version;
                _newVersion = version + 1;
            }

            public int Version { get; private set; }

            public T Value { get; private set; }

            public void UpdateIfNewer(T value, int version) {
                lock (_syncObj) {
                    if (version > Version) {
                        Value = value;
                        Version = version;
                    }
                }
            }

            public int GetNewVersion() {
                return Interlocked.Increment(ref _newVersion);
            }
        }
    }
}
