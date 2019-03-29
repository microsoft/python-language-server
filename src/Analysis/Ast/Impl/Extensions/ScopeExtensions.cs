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

using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public static class ScopeExtensions {
        public static bool IsClassScope(this IScope scope) => scope.Node is ClassDefinition;
        public static bool IsFunctionScope(this IScope scope) => scope.Node is FunctionDefinition;

        public static int GetBodyStartIndex(this IScope scope, PythonAst ast) {
            switch (scope.Node) {
                case ClassDefinition cd:
                    return cd.HeaderIndex;
                case FunctionDefinition fd:
                    return fd.HeaderIndex;
                default:
                    return ast.LocationToIndex(scope.Node.GetStart(ast));
            }
        }

        public static IScope FindScope(this IScope parent, IDocument document, SourceLocation location) {
            var children = parent.Children;
            var ast = document.Analysis.Ast;
            var index = ast.LocationToIndex(location);
            IScope candidate = null;

            for (var i = 0; i < children.Count; ++i) {
                if (children[i].Node is FunctionDefinition fd && fd.IsInParameter(ast, location)) {
                    // In parameter name scope, so consider the function scope.
                    return children[i];
                }

                var start = children[i].GetBodyStartIndex(ast);
                if (start > index) {
                    // We've gone past index completely so our last candidate is
                    // the best one.
                    break;
                }

                var end = children[i].Node.EndIndex;
                if (i + 1 < children.Count) {
                    var nextStart = children[i + 1].Node.StartIndex;
                    if (nextStart > end) {
                        end = nextStart;
                    }
                }

                if (index <= end || (candidate == null && i + 1 == children.Count)) {
                    candidate = children[i];
                }
            }

            if (candidate == null) {
                return parent;
            }

            var scopeIndent = GetParentScopeIndent(candidate, document.Analysis.Ast);
            var indent = GetLineIndent(document, index);
            if (indent <= scopeIndent) {
                // Candidate is at deeper indentation than location and the
                // candidate is scoped, so return the parent instead.
                return parent;
            }

            // Recurse to check children of candidate scope
            var child = FindScope(candidate, document, location);

            if (child.Node is FunctionDefinition fd1 && fd1.IsLambda && child.Node.EndIndex < index) {
                // Do not want to extend a lambda function's scope to the end of
                // the parent scope.
                return parent;
            }

            return child;
        }

        private static int GetLineIndent(IDocument document, int index) {
            var content = document.Content;
            if (!string.IsNullOrEmpty(content)) {
                var i = index - 1;
                for (; i >= 0 && content[i] != '\n' && content[i] != '\r'; i--) { }
                var lineStart = i + 1;
                for (i = lineStart; i < content.Length && char.IsWhiteSpace(content[i]) && content[i] != '\n' && content[i] != '\r'; i++) { }
                return i - lineStart + 1;
            }
            return 1;
        }

        private static int GetParentScopeIndent(IScope scope, PythonAst ast) {
            switch (scope.Node) {
                case ClassDefinition cd:
                    // Return column of "class" statement
                    return cd.GetStart(ast).Column;
                case FunctionDefinition fd when !fd.IsLambda:
                    // Return column of "def" statement
                    return fd.GetStart(ast).Column;
                default:
                    return -1;
            }
        }
    }
}
