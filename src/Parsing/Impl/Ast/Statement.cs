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
    public abstract class Statement : Node {
        internal Statement() {
        }

        public virtual string Documentation {
            get {
                return null;
            }
        }

        /// <summary>
        /// Returns the length of the keywords (including internal whitespace), such
        /// that StartIndex + KeywordLength represents the end of leading keywords.
        /// </summary>
        public virtual int KeywordLength => 0;
        /// <summary>
        /// The index of the end of the leading keywords.
        /// </summary>
        public virtual int KeywordEndIndex => StartIndex + KeywordLength;

        internal override sealed void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            AppendCodeStringStmt(res, ast, format);
        }

        internal abstract void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format);

        /// <summary>
        /// Returns the expression contained by the statement.
        /// 
        /// Returns null if it's not an expression statement or return statement.
        /// 
        /// New in 1.1.
        /// </summary>
        public static Expression GetExpression(Statement statement) {
            if (statement is ExpressionStatement exprStmt) {
                return exprStmt.Expression;
            } else if (statement is ReturnStatement retStmt) {
                return retStmt.Expression;
            } else {
                return null;
            }
        }
    }
}
