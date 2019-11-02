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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Extensions;

namespace Microsoft.Python.LanguageServer.Utilities {
    /// <summary>
    /// Generate unique identifier based on given context
    /// </summary>
    internal class UniqueNameGenerator {
        private readonly IDocumentAnalysis _analysis;
        private readonly IScopeNode _scope;
        private readonly bool _uniqueInModule;

        public static string Generate(IDocumentAnalysis analysis, int position, string name) {
            var generator = new UniqueNameGenerator(analysis, position);
            return generator.Generate(name);
        }

        public static string Generate(IDocumentAnalysis analysis, string name) {
            var generator = new UniqueNameGenerator(analysis, position: -1);
            return generator.Generate(name);
        }

        public UniqueNameGenerator(IDocumentAnalysis analysis, int position) {
            _analysis = analysis;
            _uniqueInModule = position < 0;

            if (!_uniqueInModule) {
                var finder = new ExpressionFinder(analysis.Ast, new FindExpressionOptions() { Names = true });
                finder.Get(position, position, out _, out _, out _scope);
            }
        }

        public string Generate(string name) {
            // for now, there is only 1 new name rule which is just incrementing count at the end.
            int count = 0;
            Func<string> getNextName = () => {
                return $"{name}{++count}";
            };

            // for now, everything is fixed. and there is no knob to control what to consider when
            // creating unique name and how to create new name if there is a conflict
            if (_uniqueInModule) {
                return GenerateModuleWideUniqueName(name, getNextName);
            } else {
                return GenerateContextBasedUniqueName(name, getNextName);
            }
        }

        private string GenerateModuleWideUniqueName(string name, Func<string> getNextName) {
            // think of a better way to do this.
            var leafScopes = GetLeafScopes(_analysis.Ast.ChildNodesDepthFirst().OfType<IScopeNode>());

            while (true) {
                if (!leafScopes.Any(s => NameExist(name, s))) {
                    return name;
                }

                name = getNextName();
            }
        }

        private HashSet<IScopeNode> GetLeafScopes(IEnumerable<IScopeNode> scopes) {
            var set = scopes.ToHashSet();
            foreach (var scope in set.ToList()) {
                if (scope.ParentScopeNode != null) {
                    set.Remove(scope.ParentScopeNode);
                }
            }

            return set;
        }

        private bool NameExist(string name, IScopeNode scope) {
            var eval = _analysis.ExpressionEvaluator;
            using (eval.OpenScope(_analysis.Document, scope)) {
                return eval.LookupNameInScopes(name, LookupOptions.All) != null;
            }
        }

        private string GenerateContextBasedUniqueName(string name, Func<string> getNextName) {
            var eval = _analysis.ExpressionEvaluator;
            using (eval.OpenScope(_analysis.Document, _scope)) {
                while (true) {
                    var member = eval.LookupNameInScopes(name, LookupOptions.All);
                    if (member == null) {
                        return name;
                    }

                    name = getNextName();
                }
            }
        }
    }
}
