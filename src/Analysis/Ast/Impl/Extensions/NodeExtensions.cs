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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class NodeExtensions {
        public static LocationInfo GetLocation(this Node node, IPythonModule module) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return LocationInfo.Empty;
            }

            var start = node.GetStart(module.GetAst());
            var end = node.GetEnd(module.GetAst());
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
