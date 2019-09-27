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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class AstExtensions {
        public static Expression FindExpression(this PythonAst ast, int index, FindExpressionOptions options)
            => new ExpressionFinder(ast, options).GetExpression(index) as Expression;

        public static Expression FindExpression(this PythonAst ast, SourceLocation location, FindExpressionOptions options)
            => new ExpressionFinder(ast, options).GetExpression(location) as Expression;

        public static string GetDocumentation(this IScopeNode node) {
            var docExpr = (node?.Body as SuiteStatement)?.Statements?.FirstOrDefault() as ExpressionStatement;
            var ce = docExpr?.Expression as ConstantExpression;
            return ce?.GetStringValue();
        }

        public static bool IsInsideComment(this PythonAst ast, SourceLocation location) {
            var match = Array.BinarySearch(ast.CommentLocations, location);
            // If our index = -1, it means we're before the first comment
            if (match == -1) {
                return false;
            }

            if (match < 0) {
                // If we couldn't find an exact match for this position, get the nearest
                // matching comment before this point
                match = ~match - 1;
            }

            if (match >= ast.CommentLocations.Length) {
                Debug.Fail("Failed to find nearest preceding comment in AST");
                return false;
            }

            if (ast.CommentLocations[match].Line != location.Line) {
                return false;
            }

            return ast.CommentLocations[match].Column < location.Column;
        }

        public static bool IsInsideString(this PythonAst ast, SourceLocation location) {
            var index = ast.LocationToIndex(location);
            return ast.FindExpression(index, new FindExpressionOptions { Literals = true }) != null;
        }

        public static bool IsInParameter(this FunctionDefinition fd, PythonAst tree, SourceLocation location) {
            var index = tree.LocationToIndex(location);
            if (index < fd.StartIndex) {
                return false; // before the node
            }

            if (fd.Body != null && index >= fd.Body.StartIndex) {
                return false; // in the body of the function
            }

            if (fd.NameExpression != null && index < fd.NameExpression.EndIndex) {
                // before the name end
                return false;
            }

            return fd.Parameters.Any(p => {
                var paramName = p.GetVerbatimImage(tree) ?? p.Name;
                return index >= p.StartIndex && index <= p.StartIndex + paramName.Length;
            });
        }

        public static string GetStringValue(this ConstantExpression cex) {
            switch (cex.Value) {
                case AsciiString asc:
                    return asc.String;
                case string s:
                    return s;
            }
            return null;
        }
    }
}
