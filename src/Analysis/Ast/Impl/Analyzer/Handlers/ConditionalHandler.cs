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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class ConditionalHandler : StatementHandler {
        private readonly IOSPlatform _platformService;

        private enum ConditionTestResult {
            Unrecognized,
            DontWalkBody,
            WalkBody
        }

        public ConditionalHandler(AnalysisWalker walker) : base(walker) {
            _platformService = Eval.Services.GetService<IOSPlatform>();
        }

        public bool HandleIf(IfStatement node) {
            // System version, platform and os.path specializations
            var someRecognized = false;
            foreach (var test in node.Tests) {
                var result = TryHandleSysVersionInfo(test);
                if (result != ConditionTestResult.Unrecognized) {
                    if (result == ConditionTestResult.WalkBody) {
                        test.Walk(Walker);
                    }
                    someRecognized = true;
                    continue;
                }

                result = TryHandleSysPlatform(test);
                if (result != ConditionTestResult.Unrecognized) {
                    if (result == ConditionTestResult.WalkBody) {
                        test.Walk(Walker);
                    }
                    someRecognized = true;
                    continue;
                }

                result = TryHandleOsPath(test);
                if (result != ConditionTestResult.Unrecognized) {
                    if (result == ConditionTestResult.WalkBody) {
                        test.Walk(Walker);
                        return false; // Execute only one condition.
                    }
                    someRecognized = true;
                }
            }
            return !someRecognized;
        }

        private ConditionTestResult TryHandleSysVersionInfo(IfStatementTest test) {
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
                    return ConditionTestResult.WalkBody;
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
                return shouldWalk ? ConditionTestResult.WalkBody : ConditionTestResult.DontWalkBody;
            }
            return ConditionTestResult.Unrecognized;
        }

        private ConditionTestResult TryHandleSysPlatform(IfStatementTest test) {
            if (test.Test is BinaryExpression cmp &&
                cmp.Left is MemberExpression me && (me.Target as NameExpression)?.Name == "sys" && me.Name == "platform" &&
                cmp.Right is ConstantExpression cex && cex.GetStringValue() is string s) {
                switch (cmp.Operator) {
                    case PythonOperator.Equals:
                        return s == "win32" && _platformService.IsWindows ? ConditionTestResult.WalkBody : ConditionTestResult.DontWalkBody;
                    case PythonOperator.NotEquals:
                        return s == "win32" && _platformService.IsWindows ? ConditionTestResult.DontWalkBody : ConditionTestResult.WalkBody;
                }
                return ConditionTestResult.DontWalkBody;
            }
            return ConditionTestResult.Unrecognized;
        }

        private ConditionTestResult TryHandleOsPath(IfStatementTest test) {
            if (test.Test is BinaryExpression cmp &&
                cmp.Left is ConstantExpression cex && cex.GetStringValue() is string s &&
                cmp.Right is NameExpression nex && nex.Name == "_names") {
                switch (cmp.Operator) {
                    case PythonOperator.In when s == "nt":
                        return _platformService.IsWindows ? ConditionTestResult.WalkBody : ConditionTestResult.DontWalkBody;
                    case PythonOperator.In when s == "posix":
                        return _platformService.IsWindows ? ConditionTestResult.DontWalkBody : ConditionTestResult.WalkBody;
                }
                return ConditionTestResult.DontWalkBody;
            }
            return ConditionTestResult.Unrecognized;
        }
    }
}
