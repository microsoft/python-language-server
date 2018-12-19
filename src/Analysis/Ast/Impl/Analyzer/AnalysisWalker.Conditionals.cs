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
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class AnalysisWalker {
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
                            shouldWalk = _ast.LanguageVersion.ToVersion() < v;
                            break;
                        case PythonOperator.LessThanOrEqual:
                            shouldWalk = _ast.LanguageVersion.ToVersion() <= v;
                            break;
                        case PythonOperator.GreaterThan:
                            shouldWalk = _ast.LanguageVersion.ToVersion() > v;
                            break;
                        case PythonOperator.GreaterThanOrEqual:
                            shouldWalk = _ast.LanguageVersion.ToVersion() >= v;
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

            return !allValidComparisons;
        }
    }
}
