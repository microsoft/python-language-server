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

namespace Microsoft.Python.Parsing.Ast {
    public class ReturnStatement : Statement {
        private readonly Expression _expression;

        public ReturnStatement(Expression expression) {
            _expression = expression;
        }

        public Expression Expression {
            get { return _expression; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public void RoundTripRemoveValueWhiteSpace(PythonAst ast) {
            ast.SetAttribute(this, NodeAttributes.IsAltFormValue, NodeAttributes.IsAltFormValue);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("return");
            if (_expression != null) {
                var len = res.Length;

                _expression.AppendCodeString(res, ast, format);
                if (this.IsAltForm(ast)) {
                    // remove the leading white space and insert a single space
                    res.Remove(len, _expression.GetLeadingWhiteSpace(ast).Length);
                    res.Insert(len, ' ');
                }
            }
        }
    }
}
