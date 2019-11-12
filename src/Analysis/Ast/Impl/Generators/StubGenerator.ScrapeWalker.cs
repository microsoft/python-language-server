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
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Extensions;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class ScrapeWalker : PythonWalkerWithParent {
            private readonly ILogger _logger;
            private readonly IPythonModule _module;
            private readonly PythonAst _ast;
            private readonly string _original;
            private readonly HashSet<string> _allVariablesMap;

            private readonly Dictionary<string, string> _fromMap;
            private readonly Dictionary<string, List<string>> _importMap;
            private readonly HashSet<string> importToKeepInOriginalForm;
            private readonly StringBuilder _sb;

            // index in _original that points to the part that are processed
            private int _lastIndexProcessed;

            public ScrapeWalker(ILogger logger, IPythonModule module, PythonAst ast, string original) {
                _logger = logger;
                _module = module;
                _ast = ast;

                _original = original;
                _allVariablesMap = new HashSet<string>(GetBestEffortAllVariables(_module.GlobalScope));

                _lastIndexProcessed = 0;

                _sb = new StringBuilder();
                _fromMap = new Dictionary<string, string>();
                _importMap = new Dictionary<string, List<string>>();
                importToKeepInOriginalForm = new HashSet<string>();
            }

            public override bool Walk(ImportStatement node, Node parent) {
                if (node.Names.Count == 1) {
                    var moduleName = node.Names[0].MakeString();
                    var asName = node.AsNames[0].Name;
                    _fromMap.Add(asName, moduleName);

                    return RemoveNode(node.IndexSpan);
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(FunctionDefinition node, Node parent) {
                if (IsPrivate(node.Name)) {
                    // remove private member not in __all__
                    return RemoveNode(node.IndexSpan);
                }

                // require proper scope. but for now, just always global

                var eval = _module.Analysis.ExpressionEvaluator;
                using (eval.OpenScope(_module.Analysis.Document, null)) {
                    var member = eval.LookupNameInScopes(node.Name, Analysis.Analyzer.LookupOptions.All);
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(ConstantExpression node, Node parent) {
                if (parent is ExpressionStatement) {
                    var value = node.GetStringValue();
                    if (value != null) {
                        return ReplaceNodeWithText(MakeDocComment(value), node.IndexSpan);
                    }
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(EmptyStatement node, Node parent) {
                if (parent is SuiteStatement && GetParent(parent) is FunctionDefinition) {
                    return ReplaceNodeWithText("...", node.IndexSpan);
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(AssignmentStatement node, Node parent) {
                if (node.Left.Count == 1 && node.Left[0] is NameExpression nex) {
                    if (nex.Name == "__doc__" && node.Right is ConstantExpression constant) {
                        var value = constant.GetStringValue();
                        if (value != null) {
                            return ReplaceNodeWithText(MakeDocComment(value), node.IndexSpan);
                        }
                    }

                    if (IsPrivate(nex.Name)) {
                        // remove any private variables
                        return RemoveNode(node.IndexSpan);
                    }

                    if (node.Right is CallExpression call &&
                        MatchMemberName(call.Target as MemberExpression, out var targetName)) {

                        // handle call only for __future__
                        if (targetName == "_mod___future__") {
                            _importMap.GetOrAdd(targetName).Add(nex.Name);
                            return RemoveNode(node.IndexSpan);
                        } else {
                            // keep import as it is for other call
                            importToKeepInOriginalForm.Add(targetName);
                        }
                    }

                    // handle member access
                    if (MatchMemberName(node.Right as MemberExpression, out var memberName)) {
                        _importMap.GetOrAdd(memberName).Add(nex.Name);
                        return RemoveNode(node.IndexSpan);
                    }
                }

                return base.Walk(node, parent);

                bool MatchMemberName(MemberExpression member, out string name) {
                    name = null;

                    if (member == null) {
                        return false;
                    }

                    if (member.Target is NameExpression targetName &&
                        _fromMap.ContainsKey(targetName.Name)) {
                        name = targetName.Name;
                        return true;
                    }

                    return false;
                }
            }

            public string GetCode(CancellationToken cancellationToken) {
                cancellationToken.ThrowIfCancellationRequested();

                // get code that are not left in original code
                AppendOriginalText(_original.Length - 1);

                return $"{GetImportStatements()}" + _sb.ToString();

                string GetImportStatements() {
                    var sb = new StringBuilder();

                    var future = _fromMap.FirstOrDefault(kv => kv.Value == "__future__");
                    AppendImports(sb, future, useAs: false);

                    foreach (var asName in importToKeepInOriginalForm) {
                        if (_fromMap.TryGetValue(asName, out var import)) {
                            sb.AppendLine($"import {import} as {asName}");
                        }
                    }

                    foreach (var kv in _fromMap.OrderBy(kv => kv.Value).ToList()) {
                        // in stub, only xxx as yyy is considered "exported"
                        AppendImports(sb, kv, useAs: true);
                    }

                    return sb.ToString();
                }

                void AppendImports(StringBuilder sb, KeyValuePair<string, string> entry, bool useAs) {
                    if (entry.Key != null) {
                        if (_importMap.TryGetValue(entry.Key, out var imports) && imports.Count > 0) {
                            sb.AppendLine($"from {entry.Value} import {GetImports(imports, useAs)}");

                            _fromMap.Remove(entry.Key);
                            _importMap.Remove(entry.Key);
                        }
                    }
                }

                string GetImports(IEnumerable<string> imports, bool useAs) {
                    if (useAs) {
                        imports = imports.Select(i => $"{i} as {i}");
                    }

                    return string.Join(", ", imports);
                }
            }

            private void AppendOriginalText(int index) {
                _sb.Append(_original.Substring(_lastIndexProcessed, index - _lastIndexProcessed + 1));
                _lastIndexProcessed = index;
            }

            private void AppendText(string text, int lastIndex) {
                _sb.Append(text);
                _lastIndexProcessed = lastIndex;
            }

            private bool RemoveNode(IndexSpan span, bool removeTrailingText = true) {
                return ReplaceNodeWithText(string.Empty, GetSpan(span, removeTrailingText));

                IndexSpan GetSpan(IndexSpan spanToRemove, bool trailingText) {
                    var loc = _ast.IndexToLocation(spanToRemove.End);
                    if (loc.Line >= _ast.NewLineLocations.Length) {
                        return spanToRemove;
                    }

                    return IndexSpan.FromBounds(spanToRemove.Start, _ast.NewLineLocations[loc.Line - 1].EndIndex);
                }
            }

            private bool ReplaceNodeWithText(string text, IndexSpan span) {
                // put code between last point we copied and this node
                AppendOriginalText(span.Start - 1);

                // if we have str literal under expression, convert it to prettified doc comment
                AppendText(text, span.End);

                // stop walk down
                return false;
            }

            private string MakeDocComment(string text) {
                return $"\"\"\"{text.Replace("\"\"\"", "\\\"\\\"\\\"")}\"\"\"";
            }

            private IEnumerable<string> GetBestEffortAllVariables(IScope scope) {
                // this is different than StartImportMemberNames since that only returns something when
                // all entries are known. 
                if (scope.Variables.TryGetVariable("__all__", out var variable) &&
                    variable?.Value is IPythonCollection collection) {
                    return collection.Contents
                        .OfType<IPythonConstant>()
                        .Select(c => c.GetString())
                        .Where(s => !string.IsNullOrEmpty(s));
                }

                return Array.Empty<string>();
            }

            private bool IsPrivate(string identifier) {
                return identifier.StartsWith("__") && !_allVariablesMap.Contains(identifier);
            }
        }
    }
}
