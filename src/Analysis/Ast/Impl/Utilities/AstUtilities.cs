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

using System;
using System.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Utilities {
    public static class AstUtilities {
        public static PythonAst MakeEmptyAst(Uri documentUri) {
            using (var sr = new StringReader(string.Empty)) {
                return Parser.CreateParser(sr, PythonLanguageVersion.None).ParseFile(documentUri);
            }
        }

        public static Expression TryCreateExpression(string expression, PythonLanguageVersion version) {
            using (var sr = new StringReader(expression)) {
                var parser = Parser.CreateParser(sr, version, ParserOptions.Default);
                var ast = parser.ParseFile();
                if (ast.Body is SuiteStatement ste && ste.Statements.Count > 0 && ste.Statements[0] is ExpressionStatement es) {
                    return es.Expression;
                }
            }
            return null;
        }
    }
}
