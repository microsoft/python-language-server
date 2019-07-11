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

using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class PythonModuleExtensions {
        internal static PythonAst GetAst(this IPythonModule module)
            => ((IAstNodeContainer)module).GetAstNode<PythonAst>(module);

        internal static T GetAstNode<T>(this IPythonModule module, object o) where T : Node
            => ((IAstNodeContainer)module).GetAstNode<T>(o);
        internal static void AddAstNode(this IPythonModule module, object o, Node n)
            => ((IAstNodeContainer)module).AddAstNode(o, n);

        /// <summary>
        /// Returns the string line corresponding to the given location
        /// </summary>
        /// <param name="line">The line number</param>
        internal static string GetLine(this IPythonModule module, PythonAst ast, int line) {
            string content = module.Analysis?.Document?.Content;
            if (string.IsNullOrEmpty(content)) {
                return string.Empty;
            }

            SourceLocation source = new SourceLocation(line, 1);
            var start = ast.LocationToIndex(source);
            var end = start;


            for (; end < content.Length && content[end] != '\n' && content[end] != '\r'; end++) ;
            return content.Substring(start, end - start);
        }
    }
}
