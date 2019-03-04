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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class ComprehensionWalker : PythonWalker {
        private readonly IDocumentAnalysis _analysis;
        private readonly HashSet<string> _names = new HashSet<string>();
        private readonly HashSet<NameExpression> _additionalNameNodes = new HashSet<NameExpression>();

        public ComprehensionWalker(IDocumentAnalysis analysis) {
            _analysis = analysis;
        }

        public override bool Walk(GeneratorExpression node) {
            ProcessComprehension(node, node.Item, node.Iterators);
            return false;
        }

        public override bool Walk(ListComprehension node) {
            ProcessComprehension(node, node.Item, node.Iterators);
            return false;
        }

        public override bool Walk(SetComprehension node) {
            ProcessComprehension(node, node.Item, node.Iterators);
            return false;
        }

        public override bool Walk(DictionaryComprehension node) {
            CollectNames(node);
            node.Key?.Walk(new ExpressionWalker(_analysis, _names, _additionalNameNodes));
            node.Value?.Walk(new ExpressionWalker(_analysis, _names, _additionalNameNodes));
            foreach (var iter in node.Iterators) {
                iter?.Walk(new ExpressionWalker(_analysis, null, _additionalNameNodes));
            }

            return true;
        }

        private void CollectNames(Comprehension c) {
            var nc = new NameCollectorWalker(_names, _additionalNameNodes);
            foreach (var cfor in c.Iterators.OfType<ComprehensionFor>()) {
                cfor.Left?.Walk(nc);
            }
        }

        private void ProcessComprehension(Comprehension c, Node item, IEnumerable<ComprehensionIterator> iterators) {
            CollectNames(c);
            item?.Walk(new ExpressionWalker(_analysis, _names, _additionalNameNodes));
            foreach (var iter in iterators) {
                iter.Walk(new ExpressionWalker(_analysis, null, _additionalNameNodes));
            }
        }

        private sealed class NameCollectorWalker : PythonWalker {
            private readonly HashSet<string> _names;
            private readonly HashSet<NameExpression> _additionalNameNodes;

            public NameCollectorWalker(HashSet<string> names, HashSet<NameExpression> additionalNameNodes) {
                _names = names;
                _additionalNameNodes = additionalNameNodes;
            }

            public override bool Walk(NameExpression nex) {
                if (!string.IsNullOrEmpty(nex.Name)) {
                    _names.Add(nex.Name);
                    _additionalNameNodes.Add(nex);
                }

                return false;
            }
        }
    }
}
