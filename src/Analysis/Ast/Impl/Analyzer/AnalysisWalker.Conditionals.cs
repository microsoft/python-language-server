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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal partial class AnalysisWalker {
        public override async Task<bool> WalkAsync(IfStatement node, CancellationToken cancellationToken = default) {
            var allValidComparisons = true;
            foreach (var test in node.Tests) {
                if (test.Test is BinaryExpression cmp &&
                    cmp.Left is MemberExpression me && (me.Target as NameExpression)?.Name == "sys" && me.Name == "version_info" &&
                    cmp.Right is TupleExpression te && te.Items.All(i => (i as ConstantExpression)?.Value is int)) {
                    Version v;
                    try {
                        v = new Version(
                            (int)((te.Items.ElementAtOrDefault(0) as ConstantExpression)?.Value ?? 0),
                            (int)((te.Items.ElementAtOrDefault(1) as ConstantExpression)?.Value ?? 0)
                        );
                    } catch (ArgumentException) {
                        // Unsupported comparison, so walk all children
                        return true;
                    }

                    var shouldWalk = false;
                    switch (cmp.Operator) {
                        case PythonOperator.LessThan:
                            shouldWalk = Ast.LanguageVersion.ToVersion() < v;
                            break;
                        case PythonOperator.LessThanOrEqual:
                            shouldWalk = Ast.LanguageVersion.ToVersion() <= v;
                            break;
                        case PythonOperator.GreaterThan:
                            shouldWalk = Ast.LanguageVersion.ToVersion() > v;
                            break;
                        case PythonOperator.GreaterThanOrEqual:
                            shouldWalk = Ast.LanguageVersion.ToVersion() >= v;
                            break;
                    }

                    if (shouldWalk) {
                        // Supported comparison, so only walk the one block
                        await test.WalkAsync(this, cancellationToken);
                        return false;
                    }
                } else {
                    allValidComparisons = false;
                }
            }

            // Handle basic check such as
            // if isinstance(value, type):
            //    return value
            // by assigning type to the value unless clause is raising exception.
            var ce = node.Tests.FirstOrDefault()?.Test as CallExpression;
            if (ce?.Target is NameExpression ne && ne.Name == @"isinstance" && ce.Args.Count == 2) {
                var nex = ce.Args[0].Expression as NameExpression;
                var name = nex?.Name;
                var typeName = (ce.Args[1].Expression as NameExpression)?.Name;
                if (name != null && typeName != null) {
                    var typeId = typeName.GetTypeId();
                    if (typeId != BuiltinTypeId.Unknown) {
                        Eval.DeclareVariable(name, new PythonType(typeName, typeId), nex);
                    }
                }
            }

            return !allValidComparisons;
        }
    }
}
