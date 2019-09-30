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

using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class ExpressionLocator {

        public static void FindExpression(PythonAst ast, SourceLocation position, FindExpressionOptions options, out Node expression, out Node statement, out IScopeNode scope) {
            expression = null;
            statement = null;
            scope = null;

            if (ast == null) {
                return;
            }

            var finder = new ExpressionFinder(ast, options);

            var index = ast.LocationToIndex(position);
            finder.Get(index, index, out expression, out statement, out scope);

            var col = position.Column;
            while (CanBackUp(ast, expression, statement, scope, col)) {
                col -= 1;
                index -= 1;
                finder.Get(index, index, out expression, out statement, out scope);
            }

            expression = expression ?? (statement as ExpressionStatement)?.Expression;
            scope = scope ?? ast;
        }

        private static bool CanBackUp(PythonAst ast, Node node, Node statement, IScopeNode scope, int column) {
            if (node != null || !((statement as ExpressionStatement)?.Expression is ErrorExpression)) {
                return false;
            }

            var top = 1;
            if (scope != null) {
                var scopeStart = scope.GetStart(ast);
                if (scope.Body != null) {
                    top = scope.Body?.GetEnd(ast).Line == scopeStart.Line
                        ? scope.Body.GetStart(ast).Column
                        : scopeStart.Column;
                } else {
                    top = scopeStart.Column;
                }
            }

            return column > top;
        }
    }
}
