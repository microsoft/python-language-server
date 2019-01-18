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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class NodeExtensions {
        public static LocationInfo GetLocation(this Node node, IPythonModule module, PythonAst ast = null) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return LocationInfo.Empty;
            }

            ast = ast ?? (module as IDocument)?.GetAnyAst();
            if (ast != null) {
                var start = node.GetStart(ast);
                var end = node.GetEnd(ast);
                return new LocationInfo(module.FilePath, module.Uri, start.Line, start.Column, end.Line, end.Column);
            }

            return LocationInfo.Empty;
        }

        public static LocationInfo GetLocationOfName(this Node node, NameExpression header, IPythonModule module, PythonAst ast = null) {
            if (header == null) {
                return LocationInfo.Empty;
            }

            var loc = node.GetLocation(module, ast);
            if (!loc.Equals(LocationInfo.Empty)) {
                ast = ast ?? (module as IDocument)?.GetAnyAst();
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
    }
}
