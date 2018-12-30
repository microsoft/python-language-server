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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;

namespace Microsoft.Python.Parsing.Ast {
    public class PrintStatement : Statement {
        private readonly Expression[] _expressions;

        public PrintStatement(Expression destination, Expression[] expressions, bool trailingComma) {
            Destination = destination;
            _expressions = expressions;
            TrailingComma = trailingComma;
        }

        public Expression Destination { get; }

        public IList<Expression> Expressions => _expressions;

        public bool TrailingComma { get; }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Destination?.Walk(walker);
                foreach (var expression in _expressions.MaybeEnumerate()) {
                    expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Destination != null) {
                    await Destination.WalkAsync(walker, cancellationToken);
                }
                foreach (var expression in _expressions.MaybeEnumerate()) {
                    await expression.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("print");
            if (Destination != null) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(">>");
                Destination.AppendCodeString(res, ast, format);
                if (_expressions.Length > 0) {
                    res.Append(this.GetThirdWhiteSpace(ast));
                    res.Append(',');
                }
            }
            ListExpression.AppendItems(res, ast, format, string.Empty, string.Empty, this, Expressions);
        }
    }
}
