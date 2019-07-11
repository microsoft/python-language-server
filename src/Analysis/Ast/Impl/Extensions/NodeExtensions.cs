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
            return new LocationInfo(module.FilePath, module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        public static Expression RemoveParenthesis(this Expression e) {
            while (e is ParenthesisExpression parExpr) {
                e = parExpr.Expression;
            }
            return e;
        }
    }
}
