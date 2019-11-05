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
using System.Threading;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class NodeExtensions {
        public static LocationInfo GetLocation(this Node node, IExpressionEvaluator eval) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return LocationInfo.Empty;
            }

            return GetLocation(node, eval.Ast, eval.Module);
        }
        public static LocationInfo GetLocation(this Node node, IDocumentAnalysis analysis) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return LocationInfo.Empty;
            }

            return GetLocation(node, analysis.Ast, analysis.Document);
        }

        private static LocationInfo GetLocation(Node node, PythonAst ast, IPythonModule module) {
            var start = node.GetStart(ast);
            var end = node.GetEnd(ast);
            return new LocationInfo(module.FilePath, module.Uri, start, end);
        }

        public static Expression RemoveParenthesis(this Expression e) {
            while (e is ParenthesisExpression parExpr) {
                e = parExpr.Expression;
            }
            return e;
        }

        public static List<Node> GetAncestorsOrThis(this Node root, Node node, CancellationToken cancellationToken) {
            var parentChain = new List<Node>();

            // there seems no way to go up the parent chain. always has to go down from the top
            while (root != null) {
                cancellationToken.ThrowIfCancellationRequested();

                var temp = root;
                root = null;

                // this assumes node is not overlapped and children are ordered from left to right
                // in textual position
                foreach (var current in GetChildNodes(temp)) {
                    if (!current.IndexSpan.Contains(node.IndexSpan)) {
                        continue;
                    }

                    parentChain.Add(current);
                    root = current;
                    break;
                }
            }

            return parentChain;

            IEnumerable<Node> GetChildNodes(Node current) {
                // workaround import statement issue
                switch (current) {
                    case ImportStatement import: {
                        foreach (var name in WhereNotNull(import.Names)) {
                            yield return name;
                        }

                        foreach (var name in WhereNotNull(import.AsNames)) {
                            yield return name;
                        }

                        yield break;
                    }

                    case FromImportStatement fromImport: {
                        yield return fromImport.Root;

                        foreach (var name in WhereNotNull(fromImport.Names)) {
                            yield return name;
                        }

                        foreach (var name in WhereNotNull(fromImport.AsNames)) {
                            yield return name;
                        }

                        yield break;
                    }

                    default:
                        foreach (var child in current.GetChildNodes()) {
                            yield return child;
                        }
                        yield break;
                }
            }

            IEnumerable<T> WhereNotNull<T>(IEnumerable<T> items) where T : class {
                return items.Where(n => n != null);
            }
        }
    }
}
