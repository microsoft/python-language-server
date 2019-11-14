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
using System.Text;
using System.Threading;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class CleanupImportWalker : BaseWalker {
            private readonly Dictionary<string, string> _fromMap;
            private readonly Dictionary<string, List<string>> _importMap;
            private readonly HashSet<string> _importToKeepInOriginalForm;

            public CleanupImportWalker(ILogger logger, IPythonModule module, PythonAst ast, string original)
                : base(logger, module, ast, original) {
                _fromMap = new Dictionary<string, string>();
                _importMap = new Dictionary<string, List<string>>();
                _importToKeepInOriginalForm = new HashSet<string>();
            }

            public override bool Walk(ImportStatement node, Node parent) {
                if (node.Names.Count == 1) {
                    var moduleName = node.Names[0].MakeString();
                    var asName = node.AsNames[0].Name;

                    // from "import x as y", save y -> x map
                    // later we will use this to revert back
                    //
                    // import x as y
                    // z = y.z
                    // to
                    // from x.y import z as z
                    _fromMap.Add(asName, moduleName);

                    return RemoveNode(node.IndexSpan);
                }

                return base.Walk(node, parent);
            }

            public override bool Walk(AssignmentStatement node, Node parent) {
                if (node.Left.Count == 1 && node.Left[0] is NameExpression nex) {
                    if (node.Right is CallExpression call &&
                        MatchMemberName(call.Target as MemberExpression, out var targetName)) {

                        // handle call only for __future__
                        if (targetName == "_mod___future__") {
                            // we treate __future__ special since it needs to be at the top
                            _importMap.GetOrAdd(targetName).Add(nex.Name);
                            return RemoveNode(node.IndexSpan);
                        } else {
                            // keep import as it is for other call
                            // when y in "import x as y" is used any other way than what we expected
                            // keey original "import x as y"
                            _importToKeepInOriginalForm.Add(targetName);
                        }
                    }

                    // handle member access
                    if (MatchMemberName(node.Right as MemberExpression, out var memberName)) {
                        // this saves "z = y.z" to y -> list of z so that we can revert things
                        // to from x.y import z as z form
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

            public override string GetCode(CancellationToken cancellationToken) {
                cancellationToken.ThrowIfCancellationRequested();
                return GetImportStatements() + base.GetCode(cancellationToken);
            }

            private string GetImportStatements() {
                var sb = new StringBuilder();

                // handle __future__ case first
                var future = _fromMap.FirstOrDefault(kv => kv.Value == "__future__");
                AppendImports(sb, future, useAs: false);

                foreach (var asName in _importToKeepInOriginalForm) {
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

            private void AppendImports(StringBuilder sb, KeyValuePair<string, string> entry, bool useAs) {
                if (entry.Key != null) {
                    if (_importMap.TryGetValue(entry.Key, out var imports) && imports.Count > 0) {
                        sb.AppendLine($"from {entry.Value} import {GetImports(imports, useAs)}");

                        _fromMap.Remove(entry.Key);
                        _importMap.Remove(entry.Key);
                    }
                }
            }

            private static string GetImports(IEnumerable<string> imports, bool useAs) {
                if (useAs) {
                    imports = imports.Select(i => $"{i} as {i}");
                }

                return string.Join(", ", imports);
            }
        }
    }
}
