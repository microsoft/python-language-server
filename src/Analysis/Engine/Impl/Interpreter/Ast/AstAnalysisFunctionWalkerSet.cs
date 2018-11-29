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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    /// <summary>
    /// Represents set of function body walkers. Functions are walked after
    /// all classes are collected. If function or property return type is unknown,
    /// it can be walked, and so on recursively, until return type is determined
    /// or there is nothing left to walk.
    /// </summary>
    class AstAnalysisFunctionWalkerSet {
        private readonly Dictionary<FunctionDefinition, AstAnalysisFunctionWalker> _functionWalkers
            = new Dictionary<FunctionDefinition, AstAnalysisFunctionWalker>();

        public void Add(AstAnalysisFunctionWalker walker)
            => _functionWalkers[walker.Target] = walker;

        public void ProcessSet() {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var constructors = _functionWalkers
                .Where(kvp => kvp.Key.Name == "__init__" || kvp.Key.Name == "__new__")
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var ctor in constructors) {
                ProcessWalker(ctor);
            }

            while (_functionWalkers.Count > 0) {
                var walker = _functionWalkers.First().Value;
                ProcessWalker(walker);
            }
        }

        public void ProcessFunction(FunctionDefinition fn) {
            if (_functionWalkers.TryGetValue(fn, out var w)) {
                ProcessWalker(w);
            }
        }

        private void ProcessWalker(AstAnalysisFunctionWalker walker) {
            // Remove walker before processing as to prevent reentrancy.
            _functionWalkers.Remove(walker.Target);
            walker.Walk();
        }
    }
}
