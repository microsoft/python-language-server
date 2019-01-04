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
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    /// <summary>
    /// Represents set of function body walkers. Functions are walked after
    /// all classes are collected. If function or property return type is unknown,
    /// it can be walked, and so on recursively, until return type is determined
    /// or there is nothing left to walk.
    /// </summary>
    internal sealed class ModuleSymbolTable {
        private readonly ConcurrentDictionary<ScopeStatement, MemberEvalWalker> _walkers
            = new ConcurrentDictionary<ScopeStatement, MemberEvalWalker>();
        private readonly ConcurrentBag<ScopeStatement> _processed = new ConcurrentBag<ScopeStatement>();

        public HashSet<Node> ReplacedByStubs { get; }= new HashSet<Node>();

        public void Add(MemberEvalWalker walker)
            => _walkers[walker.Target] = walker;

        public MemberEvalWalker Get(ScopeStatement target)
            => _walkers.TryGetValue(target, out var w) ? w : null;

        public bool Contains(ScopeStatement node)
            => _walkers.ContainsKey(node) || _processed.Contains(node);


        public Task BuildAsync(ExpressionEval eval, CancellationToken cancellationToken = default)
            // This part only adds definition for the function and its overloads
            // to the walker list. It does NOT resolve return types or parameters.
            // Function body is not walked. For the actual function code walk
            // and the type resolution see FunctionWalker class.
            => SymbolCollector.CollectSymbolsAsync(this, eval, cancellationToken);

        public async Task ProcessAllAsync(CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            while (_walkers.Count > 0) {
                var walker = _walkers.First().Value;
                await ProcessWalkerAsync(walker, cancellationToken);
            }
        }

        public async Task ProcessScopeMembersAsync(ScopeStatement target, CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            while (_walkers.Count > 0) {
                var member = _walkers.Keys.FirstOrDefault(w => w.Parent == target);
                if (member == null) {
                    break;
                }
                await ProcessWalkerAsync(_walkers[member], cancellationToken);
            }
        }

        public async Task ProcessMemberAsync(ScopeStatement target, CancellationToken cancellationToken = default) {
            if (target != null && _walkers.TryGetValue(target, out var w)) {
                await ProcessWalkerAsync(w, cancellationToken);
            }
        }

        public async Task ProcessConstructorsAsync(ClassDefinition cd, CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var constructors = _walkers
                .Where(kvp => kvp.Key.Parent == cd && (kvp.Key.Name == "__init__" || kvp.Key.Name == "__new__"))
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var ctor in constructors) {
                await ProcessWalkerAsync(ctor, cancellationToken);
            }
        }

        private Task ProcessWalkerAsync(MemberEvalWalker walker, CancellationToken cancellationToken = default) {
            // Remove walker before processing as to prevent reentrancy.
            // NOTE: first add then remove so we don't get moment when
            // walker is missing from either set.
            _processed.Add(walker.Target);
            _walkers.TryRemove(walker.Target, out _);
            return walker.WalkAsync(cancellationToken);
        }
    }
}
