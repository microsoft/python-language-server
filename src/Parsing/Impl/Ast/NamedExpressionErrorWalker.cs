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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Extensions;

namespace Microsoft.Python.Parsing.Ast {
    internal class NamedExpressionErrorWalker : PythonWalker {
        private readonly Action<int, int, string> _reportError;
        private readonly Stack<bool> _scopeStack = new Stack<bool>(new[] { false });

        internal static void Check(PythonAst ast, PythonLanguageVersion langVersion, Action<int, int, string> reportError) {
            if (langVersion < PythonLanguageVersion.V38) {
                return;
            }

            ast.Walk(new NamedExpressionErrorWalker(reportError));
        }

        private NamedExpressionErrorWalker(Action<int, int, string> reportError) {
            _reportError = reportError;
        }

        private void ReportSyntaxError(int start, int end, string message) => _reportError(start, end, message);

        private bool InClassBody => _scopeStack.Peek();

        public override bool Walk(ClassDefinition node) {
            _scopeStack.Push(true);
            return base.Walk(node);
        }

        public override void PostWalk(ClassDefinition node) {
            base.PostWalk(node);
            _scopeStack.Pop();
        }

        public override bool Walk(FunctionDefinition node) {
            _scopeStack.Push(false);
            return base.Walk(node);
        }

        public override void PostWalk(FunctionDefinition node) {
            base.PostWalk(node);
            _scopeStack.Pop();
        }

        public override bool Walk(GeneratorExpression node) {
            CheckComprehension(node.Iterators, node.Item);
            return false;
        }

        public override bool Walk(DictionaryComprehension node) {
            CheckComprehension(node.Iterators, node.Key, node.Value);
            return false;
        }

        public override bool Walk(ListComprehension node) {
            CheckComprehension(node.Iterators, node.Item);
            return false;
        }

        public override bool Walk(SetComprehension node) {
            CheckComprehension(node.Iterators, node.Item);
            return false;
        }

        private void CheckComprehension(IEnumerable<ComprehensionIterator> iterators, params Expression[] items) {
            ImmutableArray<string> seenNamed = ImmutableArray<string>.Empty;
            ImmutableArray<string> seenIterator = ImmutableArray<string>.Empty;

            foreach (var iterator in iterators) {
                switch (iterator) {
                    case ComprehensionFor cf:
                        if (cf.Left != null) {
                            foreach (var name in cf.Left.ChildNodesBreadthFirst().OfType<NameExpression>()) {
                                if (string.IsNullOrWhiteSpace(name.Name) || name.Name == "_") {
                                    continue;
                                }

                                seenIterator = seenIterator.Add(name.Name);
                                if (seenNamed.Contains(name.Name)) {
                                    ReportSyntaxError(name.StartIndex, name.EndIndex, Resources.NamedExpressionIteratorRebindsNamedErrorMsg.FormatInvariant(name.Name));
                                }
                            }
                        }

                        if (cf.List != null) {
                            foreach (var ne in cf.List.ChildNodesBreadthFirst().OfType<NamedExpression>()) {
                                ReportSyntaxError(ne.StartIndex, ne.EndIndex, Resources.NamedExpressionInComprehensionIteratorErrorMsg);
                                if (InClassBody) {
                                    ReportSyntaxError(ne.StartIndex, ne.EndIndex, Resources.NamedExpressionInClassBodyErrorMsg);
                                }
                            }
                        }

                        break;

                    case ComprehensionIf ci:
                        if (ci.Test == null) {
                            continue;
                        }

                        foreach (var ne in ci.Test.ChildNodesBreadthFirst().OfType<NamedExpression>()) {
                            if (InClassBody) {
                                ReportSyntaxError(ne.StartIndex, ne.EndIndex, Resources.NamedExpressionInClassBodyErrorMsg);
                            }

                            foreach (var name in ne.Target.ChildNodesBreadthFirst().OfType<NameExpression>()) {
                                if (string.IsNullOrWhiteSpace(name.Name) || name.Name == "_") {
                                    continue;
                                }

                                seenNamed = seenNamed.Add(name.Name);
                                if (seenIterator.Contains(name.Name)) {
                                    ReportSyntaxError(name.StartIndex, name.EndIndex, Resources.NamedExpressionRebindIteratorErrorMsg.FormatInvariant(name.Name));
                                }
                            }
                        }

                        break;
                }
            }

            foreach (var ne in items.SelectChildNodesBreadthFirst().OfType<NamedExpression>()) {
                if (InClassBody) {
                    ReportSyntaxError(ne.StartIndex, ne.EndIndex, Resources.NamedExpressionInClassBodyErrorMsg);
                }

                foreach (var name in ne.Target.ChildNodesBreadthFirst().OfType<NameExpression>()) {
                    if (string.IsNullOrWhiteSpace(name.Name) || name.Name == "_") {
                        continue;
                    }

                    if (seenIterator.Contains(name.Name)) {
                        ReportSyntaxError(name.StartIndex, name.EndIndex, Resources.NamedExpressionRebindIteratorErrorMsg.FormatInvariant(name.Name));
                    }
                }
            }
        }
    }
}
