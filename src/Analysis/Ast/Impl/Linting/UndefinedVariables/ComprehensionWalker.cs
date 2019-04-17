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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class ComprehensionWalker : PythonWalker {
        private readonly UndefinedVariablesWalker _walker;
        private readonly HashSet<string> _localNames;
        private readonly HashSet<NameExpression> _localNameNodes;

        public ComprehensionWalker(UndefinedVariablesWalker walker, HashSet<string> localNames, HashSet<NameExpression> localNameNodes) {
            _walker = walker;
            _localNames = localNames;
            _localNameNodes = localNameNodes;
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

        public override bool Walk(ForStatement node) {
            var nc = new NameCollectorWalker(_localNames, _localNameNodes);
            node.Left?.Walk(nc);
            return true;
        }

        public override bool Walk(DictionaryComprehension node) {
            var nc = CollectNames(node);
            var ew = new ExpressionWalker(_walker, nc.Names, nc.NameExpressions);
            node.Key?.Walk(ew);
            node.Value?.Walk(ew);
            foreach (var iter in node.Iterators) {
                iter?.Walk(ew);
            }
            return false;
        }

        private NameCollectorWalker CollectNames(Comprehension c) {
            var nc = new NameCollectorWalker(_localNames, _localNameNodes);
            foreach (var cfor in c.Iterators.OfType<ComprehensionFor>()) {
                cfor.Left?.Walk(nc);
            }
            return nc;
        }

        private void ProcessComprehension(Comprehension c, Node item, IEnumerable<ComprehensionIterator> iterators) {
            var nc = CollectNames(c);
            var ew = new ExpressionWalker(_walker, nc.Names, nc.NameExpressions);
            item?.Walk(ew);
            foreach (var iter in iterators) {
                iter.Walk(ew);
            }
        }
    }
}
