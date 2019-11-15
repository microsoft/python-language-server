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
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Extensions;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class CleanupWhitespaceWalker : BaseWalker {
            private LinkedList<(int start, int indentation)> _indentations;

            public CleanupWhitespaceWalker(ILogger logger, IPythonModule module, PythonAst ast, string original)
                : base(logger, module, ast, original) {
                // it would be nice if there is a general formatter one can use to just format code rather than
                // having this kind of custom walker for feature which does simple formatting
                _indentations = new LinkedList<(int start, int indentation)>();
            }

            public override bool Walk(ClassDefinition node, Node parent) {
                // assumes no nested class. if there could be nested classes, add interval tree to save indentation
                _indentations.AddLast((node.HeaderIndex, ComputeIndentation(node)));
                return true;
            }

            public override void PostWalk(ClassDefinition node, Node parent) {
                _indentations.AddLast((node.EndIndex, ComputeIndentation(GetContainer(parent))));
            }

            public override bool Walk(FunctionDefinition node, Node parent) {
                // assumes no nested function. if there could be nested function, add interval tree to save indentation
                _indentations.AddLast((node.HeaderIndex, ComputeIndentation(node)));
                _indentations.AddLast((node.EndIndex, ComputeIndentation(GetContainer(parent))));
                return false;
            }

            public override string GetCode(CancellationToken cancellationToken) {
                // get flat statement list
                var statements = Ast.ChildNodesDepthFirst().Where(Candidate).ToList();

                // very first statement
                var firstStatement = statements[0];
                ReplaceNodeWithText(
                    GetSpacesBetween(previous: null, firstStatement, indentation: GetIndentation(firstStatement)),
                    GetSpan(previous: null, firstStatement));

                for (var i = 1; i < statements.Count; i++) {
                    var previous = statements[i - 1];
                    var current = statements[i];

                    var span = GetSpan(previous, current);
                    var codeBetweenStatement = GetOriginalText(span);
                    if (!string.IsNullOrWhiteSpace(codeBetweenStatement)) {
                        AppendOriginalText(current.StartIndex);
                        continue;
                    }

                    var indentation = GetIndentation(current);
                    if (indentation < 0) {
                        // this should never happen.
                        AppendOriginalText(current.StartIndex);
                        continue;
                    }

                    var spacesBetween = GetSpacesBetween(previous, current, indentation);
                    ReplaceNodeWithText(spacesBetween, span);
                }

                return base.GetCode(cancellationToken);

                bool Candidate(Node node) {
                    switch (node) {
                        case PythonAst _:
                            return false;
                        case SuiteStatement _:
                            return false;
                        case Statement _:
                            return true;
                        default:
                            return false;
                    }
                }
            }

            private static IndexSpan GetSpan(Node previous, Node current) {
                if (previous == null) {
                    return IndexSpan.FromBounds(0, current.StartIndex);
                }

                // previous could contain current (ex, class definition)
                // in that case, break span from previous.Start to current.start
                // rather than end
                if (previous.IndexSpan.Contains(current.IndexSpan)) {
                    return IndexSpan.FromBounds(GetEndIndex(previous), current.StartIndex);
                }

                return IndexSpan.FromBounds(previous.EndIndex, current.StartIndex);

                int GetEndIndex(Node node) {
                    if (node is ClassDefinition @class) {
                        return @class.HeaderIndex + 1;
                    }

                    if (node is FunctionDefinition func) {
                        return func.HeaderIndex + 1;
                    }

                    return node.EndIndex;
                }
            }

            private string GetSpacesBetween(Node previous, Node current, int indentation) {
                if (previous == null) {
                    return new string(' ', indentation);
                }

                // same kind of node of one liner
                if (previous.NodeName == current.NodeName && OnSingleLine(previous) && OnSingleLine(current)) {
                    return Environment.NewLine + new string(' ', indentation);
                }

                // no line between header and doc comment
                if (IsDocumentation(current as Statement)) {
                    return Environment.NewLine + new string(' ', indentation);
                }

                // different kind of node or multiple lines
                // always has 1 blank line between
                //
                // all tab vs space option is wrong. not sure how to get option and it probably is job to formatter
                return Environment.NewLine + Environment.NewLine + new string(' ', indentation);
            }

            private bool OnSingleLine(Node previous) {
                var span = previous.GetSpan(Ast);
                return span.Start.Line == span.End.Line;
            }

            private int ComputeIndentation(ScopeStatement node) {
                if (OnSingleLine(node)) {
                    // if whole class i on single line. 
                    // indentation doesn't matter
                    return -1;
                }

                var suiteStatement = node.Body as SuiteStatement;
                if (suiteStatement == null && suiteStatement.Statements.Count <= 0) {
                    // no member, we don't care indentation
                    return -1;
                }

                var start = suiteStatement.Statements[0].GetStart(Ast);
                return Math.Max(0, start.Column - 1);
            }

            private int GetIndentation(Node statement) {
                if (_indentations.Count == 0) {
                    return 0;
                }

                var current = _indentations.First;
                if (statement.StartIndex < current.Value.start) {
                    // top level
                    return 0;
                }

                // current < statement.start
                var next = current.Next;
                if (next == null || statement.StartIndex < next.Value.start) {
                    return current.Value.indentation;
                }

                // next <= statement.start
                _indentations.RemoveFirst();
                return GetIndentation(statement);
            }
        }
    }
}
