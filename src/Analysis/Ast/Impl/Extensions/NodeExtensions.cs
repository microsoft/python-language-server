﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class NodeExtensions {
        public static LocationInfo GetLocation(this Node node, IPythonModule module) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return LocationInfo.Empty;
            }

            var ast = (module as IDocument)?.GetAnyAst();
            if (ast != null) {
                var start = node.GetStart(ast);
                var end = node.GetEnd(ast);
                return new LocationInfo(module.FilePath, module.Uri, start.Line, start.Column, end.Line, end.Column);
            }

            return LocationInfo.Empty;
        }

        public static LocationInfo GetLocationOfName(this Node node, NameExpression header, IPythonModule module) {
            if (header == null) {
                return LocationInfo.Empty;
            }

            var loc = node.GetLocation(module);
            if (!loc.Equals(LocationInfo.Empty)) {
                var ast = (module as IDocument)?.GetAnyAst();
                if (ast != null) {
                    var nameStart = header.GetStart(ast);
                    if (!nameStart.IsValid) {
                        return loc;
                    }
                    if (nameStart.Line > loc.StartLine || (nameStart.Line == loc.StartLine && nameStart.Column > loc.StartColumn)) {
                        return new LocationInfo(loc.FilePath, loc.DocumentUri, nameStart.Line, nameStart.Column, loc.EndLine, loc.EndColumn);
                    }
                }
            }
            return LocationInfo.Empty;
        }

        public static bool IsInAst(this ScopeStatement node, PythonAst ast) {
            while (node.Parent != null) {
                node = node.Parent;
            }
            return ast == node;
        }

        public static Expression RemoveParenthesis(this Expression e) {
            while (e is ParenthesisExpression parExpr) {
                e = parExpr.Expression;
            }
            return e;
        }


        public static IndexSpan GetIndexSpan(this Node node, PythonAst ast)
            => ast != null && node != null ? node.GetSpan(ast).ToIndexSpan(ast) : default;

        public static IndexSpan GetNameSpan(this Node node, PythonAst ast) {
            if (ast == null) {
                return default;
            }

            switch (node) {
                case MemberExpression mex:
                    return mex.GetNameSpan(ast).ToIndexSpan(ast);
                case ClassDefinition cd:
                    return cd.NameExpression.GetSpan(ast).ToIndexSpan(ast);
                case FunctionDefinition fd:
                    return fd.NameExpression.GetSpan(ast).ToIndexSpan(ast);
                default:
                    return node.GetSpan(ast).ToIndexSpan(ast);
            }
        }
    }
}
