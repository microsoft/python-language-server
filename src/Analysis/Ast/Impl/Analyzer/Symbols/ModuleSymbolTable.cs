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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    /// <summary>
    /// Represents set of function body walkers. Functions are walked after
    /// all classes are collected. If function or property return type is unknown,
    /// it can be walked, and so on recursively, until return type is determined
    /// or there is nothing left to walk.
    /// </summary>
    internal sealed class ModuleSymbolTable {
        private readonly Dictionary<IScopeNode, MemberEvaluator> _evaluators = new Dictionary<IScopeNode, MemberEvaluator>();
        private readonly HashSet<IScopeNode> _processed = new HashSet<IScopeNode>();

        public HashSet<INode> ReplacedByStubs { get; } = new HashSet<INode>();

        public IEnumerable<KeyValuePair<IScopeNode, MemberEvaluator>> Evaluators => _evaluators.ToArray();
        public void Add(MemberEvaluator e) => _evaluators[e.Target] = e;
        public bool Contains(IScopeNode node) => _evaluators.ContainsKey(node) || _processed.Contains(node);

        public void Build(ExpressionEval eval)
            // This part only adds definition for the function and its overloads
            // to the walker list. It does NOT resolve return types or parameters.
            // Function body is not walked. For the actual function code walk
            // and the type resolution see FunctionWalker class.
            => SymbolCollector.CollectSymbols(this, eval);

        public void EvaluateAll() {
            // Evaluate top-level functions first.
            while (_evaluators.Count > 0) {
                var walker = _evaluators.FirstOrDefault(e => e.Value.Target is FunctionDefinition fd && fd.ParentNode == null).Value;
                if (walker == null) {
                    break;
                }
                Evaluate(walker);
            }

            // Evaluate classes.
            while (_evaluators.Count > 0) {
                var walker = _evaluators.FirstOrDefault(e => e.Value.Target is ClassDefinition).Value;
                if (walker == null) {
                    break;
                }
                Evaluate(walker);
            }

            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            while (_evaluators.Count > 0) {
                var walker = _evaluators.First().Value;
                Evaluate(walker);
            }
        }

        public void EvaluateScope(IScopeNode target) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            while (_evaluators.Count > 0) {
                var member = _evaluators.Keys.FirstOrDefault(w => w.ParentNode == target);
                if (member == null) {
                    break;
                }
                Evaluate(_evaluators[member]);
            }
        }

        public IMember Evaluate(IScopeNode target) {
            if (target != null && _evaluators.TryGetValue(target, out var w)) {
                Evaluate(w);
                return w.Result;
            }
            return null;
        }

        public void Evaluate(MemberEvaluator e) {
            // Remove walker before processing as to prevent reentrancy.
            // NOTE: first add then remove so we don't get moment when
            // walker is missing from either set.
            _processed.Add(e.Target);
            _evaluators.Remove(e.Target);
            e.Evaluate();
        }
    }
}
