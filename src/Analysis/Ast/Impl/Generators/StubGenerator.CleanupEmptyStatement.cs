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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class CleanupEmptyStatement : BaseWalker {
            public CleanupEmptyStatement(ILogger logger, IPythonModule module, PythonAst ast, string original)
                : base(logger, module, ast, original) {
            }

            public override bool Walk(ClassDefinition node, Node parent) {
                if (HandleBody(node.HeaderIndex, node.Body)) {
                    return false;
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(FunctionDefinition node, Node parent) {
                if (HandleBody(node.HeaderIndex, node.Body)) {
                    return false;
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(EmptyStatement node, Node parent) {
                if (parent is SuiteStatement && GetParent(parent) is FunctionDefinition) {
                    return ReplaceNodeWithText("...", node.IndexSpan);
                }

                return base.Walk(node, parent);
            }

            private bool HandleBody(int index, Statement body) {
                if (body is ErrorStatement ||
                    (body is SuiteStatement suite && suite.Statements.Count == 1 && suite.Statements[0] is EmptyStatement)) {
                    // keep up to header and
                    // get rid of anything after header
                    var span = (index + 1 <= body.EndIndex) ? IndexSpan.FromBounds(index + 1, body.EndIndex) : IndexSpan.FromBounds(index, index);
                    ReplaceNodeWithText(" ..." + Environment.NewLine, span);

                    return true;
                }

                return false;
            }
        }
    }
}
