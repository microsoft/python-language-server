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
using System.Linq;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public static class ScopeExtensions {
        public static IEnumerable<string> GetExportableVariableNames(this IGlobalScope scope)
            // drop imported modules and typing
            => scope.Variables
                .Where(v => {
                    // Instances are always fine.
                    if (v.Value is IPythonInstance) {
                        return true;
                    }

                    var valueType = v.Value?.GetPythonType();
                    switch (valueType) {
                        case PythonModule _:
                        case IPythonFunctionType f when f.IsLambda():
                            return false; // Do not re-export modules.
                    }

                    if (scope.Module is TypingModule) {
                        return true; // Let typing module behave normally.
                    }

                    // Do not re-export types from typing. However, do export variables
                    // assigned with types from typing. Example:
                    //    from typing import Any # do NOT export Any
                    //    x = Union[int, str] # DO export x
                    return !(valueType?.DeclaringModule is TypingModule) || v.Name != valueType.Name;
                })
                .Select(v => v.Name)
                .ToArray();

        public static IMember LookupNameInScopes(this IScope currentScope, string name, out IScope scope) {
            scope = null;
            foreach (var s in currentScope.EnumerateTowardsGlobal) {
                if (s.Variables.TryGetVariable(name, out var v) && v != null) {
                    scope = s;
                    return v.Value;
                }
            }
            return null;
        }

        public static IMember LookupImportedNameInScopes(this IScope currentScope, string name, out IScope scope) {
            scope = null;
            foreach (var s in currentScope.EnumerateTowardsGlobal) {
                if (s.Imported.TryGetVariable(name, out var v) && v != null) {
                    scope = s;
                    return v.Value;
                }
            }
            return null;
        }

        public static int GetBodyStartIndex(this IScope scope) {
            switch (scope.Node) {
                case ClassDefinition cd:
                    return cd.HeaderIndex;
                case FunctionDefinition fd:
                    return fd.HeaderIndex;
                case null:
                    return 0;
                default:
                    return scope.Node.StartIndex;
            }
        }

        public static bool IsNestedInScope(this IScope s, IScope outer)
            => s.OuterScope != null && s.OuterScope.EnumerateTowardsGlobal.Any(x => x == outer);

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

                var start = children[i].GetBodyStartIndex();
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
            var indent = GetLineIndent(document, index, out var lineIsEmpty);
            indent = lineIsEmpty ? location.Column : indent; // Take into account virtual space.
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

        private static int GetLineIndent(IDocument document, int index, out bool lineIsEmpty) {
            var content = document.Content;
            lineIsEmpty = true;
            if (!string.IsNullOrEmpty(content)) {
                var i = index - 1;
                for (; i >= 0 && content[i] != '\n' && content[i] != '\r'; i--) { }
                var lineStart = i + 1;
                for (i = lineStart; i < content.Length && content[i] != '\n' && content[i] != '\r'; i++) {
                    if (!char.IsWhiteSpace(content[i])) {
                        lineIsEmpty = false;
                        break;
                    }
                }
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

        /// <summary>
        /// This returns __all__ contents we understood.
        /// This is different than StartImportMemberNames since that only returns results when
        /// all entries are known. This returns whatever we understood even if there are 
        /// ones we couldn't understand in __all__
        /// </summary>
        public static IEnumerable<string> GetBestEffortsAllVariables(this IScope scope) {
            if (scope == null) {
                return Enumerable.Empty<string>();
            }

            // this is different than StartImportMemberNames since that only returns something when
            // all entries are known. for import, we are fine doing best effort
            if (scope.Variables.TryGetVariable("__all__", out var variable) &&
                variable?.Value is IPythonCollection collection) {
                return collection.Contents
                    .OfType<IPythonConstant>()
                    .Select(c => c.GetString())
                    .Where(s => !string.IsNullOrEmpty(s));
            }

            return Enumerable.Empty<string>();
        }
    }
}
