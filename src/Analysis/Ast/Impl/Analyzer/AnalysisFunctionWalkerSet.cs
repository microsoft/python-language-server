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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Represents set of function body walkers. Functions are walked after
    /// all classes are collected. If function or property return type is unknown,
    /// it can be walked, and so on recursively, until return type is determined
    /// or there is nothing left to walk.
    /// </summary>
    class AnalysisFunctionWalkerSet {
        private readonly ConcurrentDictionary<FunctionDefinition, AnalysisFunctionWalker> _functionWalkers
            = new ConcurrentDictionary<FunctionDefinition, AnalysisFunctionWalker>();
        private readonly ConcurrentBag<FunctionDefinition> _processed = new ConcurrentBag<FunctionDefinition>();

        public void Add(AnalysisFunctionWalker walker)
            => _functionWalkers[walker.Target] = walker;

        public async Task ProcessSetAsync(CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var constructors = _functionWalkers
                .Where(kvp => kvp.Key.Name == "__init__" || kvp.Key.Name == "__new__")
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var ctor in constructors) {
                await ProcessWalkerAsync(ctor, cancellationToken);
            }

            while (_functionWalkers.Count > 0) {
                var walker = _functionWalkers.First().Value;
                await ProcessWalkerAsync(walker, cancellationToken);
            }
        }

        public async Task ProcessConstructorsAsync(ClassDefinition cd, CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var constructors = _functionWalkers
                .Where(kvp => GetClassParent(kvp.Key) == cd && (kvp.Key.Name == "__init__" || kvp.Key.Name == "__new__"))
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var ctor in constructors) {
                await ProcessWalkerAsync(ctor, cancellationToken);
            }
        }

        public async Task<bool> ProcessFunctionAsync(FunctionDefinition fn, CancellationToken cancellationToken = default) {
            if (_functionWalkers.TryGetValue(fn, out var w)) {
                await ProcessWalkerAsync(w, cancellationToken);
                return true;
            }
            return false;
        }

        public bool Contains(FunctionDefinition node)
            => _functionWalkers.ContainsKey(node) || _processed.Contains(node);

        private Task ProcessWalkerAsync(AnalysisFunctionWalker walker, CancellationToken cancellationToken = default) {
            // Remove walker before processing as to prevent reentrancy.
            // NOTE: first add then remove so we don't get moment when
            // walker is missing from either set.
            _processed.Add(walker.Target);
            _functionWalkers.TryRemove(walker.Target, out _);
            return walker.WalkAsync(cancellationToken);
        }

        private static ClassDefinition GetClassParent(FunctionDefinition fd) {
            ScopeStatement node = fd;
            while (node != null) {
                if (node is ClassDefinition cd) {
                    return cd;
                }
                node = node.Parent;
            }
            return null;
        }
    }
}
