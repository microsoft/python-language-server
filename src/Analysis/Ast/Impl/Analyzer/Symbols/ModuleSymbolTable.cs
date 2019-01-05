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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    /// <summary>
    /// Represents set of function body walkers. Functions are walked after
    /// all classes are collected. If function or property return type is unknown,
    /// it can be walked, and so on recursively, until return type is determined
    /// or there is nothing left to walk.
    /// </summary>
    internal sealed class ModuleSymbolTable {
        private readonly ConcurrentDictionary<ScopeStatement, MemberEvaluator> _evaluators
            = new ConcurrentDictionary<ScopeStatement, MemberEvaluator>();
        private readonly ConcurrentBag<ScopeStatement> _processed = new ConcurrentBag<ScopeStatement>();

        public HashSet<Node> ReplacedByStubs { get; }= new HashSet<Node>();

        public IEnumerable<KeyValuePair<ScopeStatement, MemberEvaluator>> Evaluators => _evaluators.ToArray();
        public void Add(MemberEvaluator e) => _evaluators[e.Target] = e;
        public MemberEvaluator Get(ScopeStatement target) => _evaluators.TryGetValue(target, out var w) ? w : null;
        public bool Contains(ScopeStatement node) => _evaluators.ContainsKey(node) || _processed.Contains(node);


        public Task BuildAsync(ExpressionEval eval, CancellationToken cancellationToken = default)
            // This part only adds definition for the function and its overloads
            // to the walker list. It does NOT resolve return types or parameters.
            // Function body is not walked. For the actual function code walk
            // and the type resolution see FunctionWalker class.
            => SymbolCollector.CollectSymbolsAsync(this, eval, cancellationToken);

        public async Task EvaluateAllAsync(CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            while (_evaluators.Count > 0) {
                var walker = _evaluators.First().Value;
                await EvaluateAsync(walker, cancellationToken);
            }
        }

        public async Task EvaluateScopeAsync(ScopeStatement target, CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            while (_evaluators.Count > 0) {
                var member = _evaluators.Keys.FirstOrDefault(w => w.Parent == target);
                if (member == null) {
                    break;
                }
                await EvaluateAsync(_evaluators[member], cancellationToken);
            }
        }

        public async Task EvaluateAsync(ScopeStatement target, CancellationToken cancellationToken = default) {
            if (target != null && _evaluators.TryGetValue(target, out var w)) {
                await EvaluateAsync(w, cancellationToken);
            }
        }

        public Task EvaluateAsync(MemberEvaluator e, CancellationToken cancellationToken = default) {
            // Remove walker before processing as to prevent reentrancy.
            // NOTE: first add then remove so we don't get moment when
            // walker is missing from either set.
            _processed.Add(e.Target);
            _evaluators.TryRemove(e.Target, out _);
            return e.EvaluateAsync(cancellationToken);
        }
    }
}
