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

using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    /// <summary>
    /// Helper class that provides methods for looking up variables
    /// and types in a chain of scopes during analysis.
    /// </summary>
    internal sealed partial class ExpressionEval : IExpressionEvaluator {
        public IPythonType GetTypeFromPepHint(Node node) {
            var location = GetLoc(node);
            var content = (Module as IDocument)?.Content;
            if (string.IsNullOrEmpty(content) || !location.EndLine.HasValue) {
                return null;
            }

            var ch = '\0';
            var i = node.IndexSpan.End;
            // Starting with the end of the node, look for #.
            for (; i < content.Length; i++) {
                ch = content[i];
                if (ch == '#' || ch == '\r' || ch == '\n') {
                    break;
                }
            }

            if (ch != '#') {
                return null;
            }

            // Skip # and whitespace.
            i++;
            for (; i < content.Length; i++) {
                ch = content[i];
                if (!char.IsWhiteSpace(ch)) {
                    break;
                }
            }

            // Must be at 'type:'
            if (ch != 't' || i > content.Length - 5) {
                return null;
            }

            if (content[i + 1] != 'y' || content[i + 2] != 'p' || content[i + 3] != 'e' || content[i + 4] != ':') {
                return null;
            }

            // Skip 'type:' and locate end of the line.
            i += 5;
            var hintStart = i;
            for (; i < content.Length; i++) {
                if (content[i] == '\r' || content[i] == '\n') {
                    break;
                }
            }

            if (i == hintStart) {
                return null;
            }

            // Type alone is not a valid syntax, so we need to simulate the annotation.
            var typeString = content.Substring(hintStart, i - hintStart);
            return GetTypeFromString(typeString);
        }

        public IPythonType GetTypeFromString(string typeString) {
            // Type alone is not a valid syntax, so we need to simulate the annotation.
            typeString = $"x: {typeString}";
            using (var sr = new StringReader(typeString)) {
                var sink = new CollectingErrorSink();
                var parser = Parser.CreateParser(sr, Module.Interpreter.LanguageVersion, new ParserOptions { ErrorSink = sink });
                var ast = parser.ParseFile();
                var exprStatement = (ast?.Body as SuiteStatement)?.Statements?.FirstOrDefault() as ExpressionStatement;
                if (!(Statement.GetExpression(exprStatement) is ExpressionWithAnnotation annExpr) || sink.Errors.Count > 0) {
                    return null;
                }

                var ann = new TypeAnnotation(Ast.LanguageVersion, annExpr.Annotation);
                var value = ann.GetValue(new TypeAnnotationConverter(this));
                var t = value.GetPythonType();
                if (!t.IsUnknown()) {
                    return t;
                }
            }
            return null;
        }
    }
}
