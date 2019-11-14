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
using System.Threading;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class ConvertDocCommentWalker : BaseWalker {
            private string _moduleDocString;

            public ConvertDocCommentWalker(ILogger logger, IPythonModule module, PythonAst ast, string original)
                : base(logger, module, ast, original) {
                _moduleDocString = null;
            }

            public override bool Walk(ConstantExpression node, Node parent) {
                if (parent is ExpressionStatement) {
                    var value = node.GetStringValue();
                    if (value != null) {
                        return ReplaceNodeWithText(MakeDocComment(value), node.IndexSpan);
                    }
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(AssignmentStatement node, Node parent) {
                if (node.Left.Count == 1 && node.Left[0] is NameExpression nex) {
                    if (nex.Name == "__doc__" && node.Right is ConstantExpression constant) {
                        var value = constant.GetStringValue();
                        if (value != null) {
                            if (GetParent(parent) is PythonAst) {
                                // treat module doc string special. we will move it to the top of the stub
                                _moduleDocString = MakeDocComment(value);
                                return RemoveNode(node.IndexSpan);
                            } else {
                                return ReplaceNodeWithText(MakeDocComment(value), node.IndexSpan);
                            }
                        }
                    }
                }

                return base.Walk(node, parent);
            }

            public override string GetCode(CancellationToken cancellationToken) {
                cancellationToken.ThrowIfCancellationRequested();

                return GetModuleDocString() + base.GetCode(cancellationToken);

                string GetModuleDocString() {
                    if (_moduleDocString == null) {
                        return string.Empty;
                    }

                    return _moduleDocString + Environment.NewLine + Environment.NewLine;
                }
            }

            private string MakeDocComment(string text) {
                return $"\"\"\"{text.Replace("\"\"\"", "\\\"\\\"\\\"")}\"\"\"";
            }
        }
    }
}
