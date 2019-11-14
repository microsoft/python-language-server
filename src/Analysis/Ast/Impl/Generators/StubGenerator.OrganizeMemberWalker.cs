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
using System.Threading;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class OrganizeMemberWalker : BaseWalker {
            private readonly Dictionary<Node, List<Node>> _assignments;

            public OrganizeMemberWalker(ILogger logger, IPythonModule module, PythonAst ast, string original)
                : base(logger, module, ast, original) {
                _assignments = new Dictionary<Node, List<Node>>();
            }

            public override bool Walk(CallExpression node, Node parent) {
                _assignments.GetOrAdd(GetContainer(parent)).Add(node);

                // stop walk down
                return false;
            }

            public override bool Walk(AssignmentStatement node, Node parent) {
                _assignments.GetOrAdd(GetContainer(parent)).Add(node);

                // stop walk down
                return false;
            }

            public override string GetCode(CancellationToken cancellationToken) {
                var codeToSkipQueue = new SortedList<int, Node>();

                // put attributes top and then other members
                foreach (var entry in _assignments.OrderBy(kv => kv.Key.StartIndex)) {
                    var (index, indentation) = GetStartingPoint(entry.Key);

                    AppendTextUpto(codeToSkipQueue, index);

                    var indentationString = new string(' ', indentation);
                    for (var i = 0; i < entry.Value.Count; i++) {
                        var node = entry.Value[i];
                        AppendText($"{(i == 0 ? string.Empty : indentationString)}" + GetOriginalText(node.IndexSpan) + Environment.NewLine, index);
                        codeToSkipQueue.Add(node.StartIndex, node);
                    }
                }

                if (codeToSkipQueue.Count > 0) {
                    AppendTextUpto(codeToSkipQueue, codeToSkipQueue.Last().Value.EndIndex);
                }

                return base.GetCode(cancellationToken);
            }

            private void AppendTextUpto(SortedList<int, Node> codeToSkipQueue, int lastIndex) {
                foreach (var kv in codeToSkipQueue.TakeWhile(kv => kv.Key < lastIndex).ToList()) {
                    RemoveNode(kv.Value.IndexSpan, removeTrailingText: false);

                    codeToSkipQueue.Remove(kv.Key);
                }

                AppendOriginalText(lastIndex);
            }

            private (int, int) GetStartingPoint(Node node) {
                var body = (node is PythonAst ast) ? ast.Body : ((ClassDefinition)node).Body;

                // TODO: add a method to return next line start index from end index so that we can just
                //       add it next line
                if (body is SuiteStatement suiteStatements && suiteStatements.Statements.Count > 0) {
                    var stmt = GetStartingStatement(suiteStatements.Statements);
                    return GetStartIndexAndIndentation(stmt);
                }

                return GetStartIndexAndIndentation(body);

                (int, int) GetStartIndexAndIndentation(Node current) {
                    var start = current.GetStart(Ast);
                    return (Math.Max(0, current.StartIndex - 1), Math.Max(start.Column - 1, 0));
                }
            }

            private static Statement GetStartingStatement(IList<Statement> statements) {
                foreach (var statement in statements) {
                    if (IsDocumentation(statement) ||
                        statement is ImportStatement ||
                        statement is FromImportStatement) {
                        continue;
                    }

                    return statement;
                }

                // how this can happen if this contains assigments?
                return statements[0];
            }

            private static bool IsDocumentation(Statement statement) {
                return statement is ExpressionStatement exprStmt && exprStmt.Expression is ConstantExpression;
            }

            private Node GetContainer(Node node) {
                while (node != null) {
                    if (node is PythonAst ||
                        node is ClassDefinition) {
                        return node;
                    }

                    node = GetParent(node);
                }

                return Ast;
            }
        }
    }
}
