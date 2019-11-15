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

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Extensions;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private abstract class BaseWalker : PythonWalkerWithParent {
            private readonly string _original;
            private readonly StringBuilder _sb;

            // index in _original that points to the part that are processed
            private int _lastIndexProcessed;

            protected readonly ILogger Logger;
            protected readonly IPythonModule Module;
            protected readonly PythonAst Ast;

            public BaseWalker(ILogger logger, IPythonModule module, PythonAst ast, string original) {
                Logger = logger;
                Module = module;
                Ast = ast;

                _original = original;
                _sb = new StringBuilder();

                _lastIndexProcessed = 0;
            }

            public virtual string GetCode(CancellationToken cancellationToken) {
                cancellationToken.ThrowIfCancellationRequested();

                // get code that are not left in original code
                AppendOriginalText(_original.Length);

                return _sb.ToString();
            }

            protected string GetOriginalText(IndexSpan span) {
                return _original.Substring(span.Start, span.Length);
            }

            protected void AppendOriginalText(int index) {
                _sb.Append(_original.Substring(_lastIndexProcessed, index - _lastIndexProcessed));
                _lastIndexProcessed = Math.Max(index, 0);
            }

            protected void AppendText(string text, int lastIndex) {
                _sb.Append(text);
                _lastIndexProcessed = Math.Max(lastIndex, 0);
            }

            protected bool RemoveNode(IndexSpan span, bool removeTrailingText = true) {
                return ReplaceNodeWithText(string.Empty, span, removeTrailingText);
            }

            protected bool ReplaceNodeWithText(string text, IndexSpan span, bool removeTrailingText = false) {
                span = GetSpan(span, removeTrailingText);

                // put code between last point we copied and this node
                AppendOriginalText(span.Start);

                // if we have str literal under expression, convert it to prettified doc comment
                AppendText(text, span.End);

                // stop walk down
                return false;

                IndexSpan GetSpan(IndexSpan spanToReplace, bool trailingText) {
                    if (!trailingText) {
                        return spanToReplace;
                    }

                    var loc = Ast.IndexToLocation(spanToReplace.End);
                    if (loc.Line >= Ast.NewLineLocations.Length) {
                        return spanToReplace;
                    }

                    return IndexSpan.FromBounds(spanToReplace.Start, Ast.NewLineLocations[loc.Line - 1].EndIndex);
                }
            }

            protected ScopeStatement GetContainer(Node node) {
                while (node != null) {
                    if (node is ScopeStatement scope) {
                        return scope;
                    }

                    node = GetParent(node);
                }

                return Ast;
            }

            protected static bool IsPrivate(string identifier, HashSet<string> allVariables) {
                return identifier.StartsWith("_") && !allVariables.Contains(identifier);
            }

            protected static bool IsDocumentation(Statement statement) {
                return statement is ExpressionStatement exprStmt && exprStmt.Expression is ConstantExpression;
            }
        }
    }
}
