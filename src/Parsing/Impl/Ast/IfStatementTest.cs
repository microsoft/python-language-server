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

using System.Text;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    public class IfStatementTest : Node {
        public IfStatementTest(Expression test, Statement body) {
            Test = test;
            Body = body;
        }

        public int HeaderIndex { get; set; }

        public Expression Test { get; }

        public Statement Body { get; set; }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Test != null) {
                    Test.Walk(walker);
                }
                if (Body != null) {
                    Body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public SourceLocation GetHeader(PythonAst ast) => ast.IndexToLocation(HeaderIndex);

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Test.AppendCodeString(res, ast, format);
            Body.AppendCodeString(res, ast, format);
        }
    }
}
