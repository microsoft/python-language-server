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
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public enum ConditionTestResult {
        Unrecognized,
        DontWalkBody,
        WalkBody
    }

    public static class IfStatementExtensions {
        public static ConditionTestResult TryHandleSysVersionInfo(this IfStatementTest test, PythonLanguageVersion languageVersion) {
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
                        shouldWalk = languageVersion.ToVersion() < v;
                        break;
                    case PythonOperator.LessThanOrEqual:
                        shouldWalk = languageVersion.ToVersion() <= v;
                        break;
                    case PythonOperator.GreaterThan:
                        shouldWalk = languageVersion.ToVersion() > v;
                        break;
                    case PythonOperator.GreaterThanOrEqual:
                        shouldWalk = languageVersion.ToVersion() >= v;
                        break;
                }
                return shouldWalk ? ConditionTestResult.WalkBody : ConditionTestResult.DontWalkBody;
            }
            return ConditionTestResult.Unrecognized;
        }

        public static ConditionTestResult TryHandleSysPlatform(this IfStatementTest test, bool isWindows) {
            if (test.Test is BinaryExpression cmp &&
                cmp.Left is MemberExpression me && (me.Target as NameExpression)?.Name == "sys" && me.Name == "platform" &&
                cmp.Right is ConstantExpression cex && cex.GetStringValue() is string s) {
                switch (cmp.Operator) {
                    case PythonOperator.Equals:
                        return s == "win32" && isWindows ? ConditionTestResult.WalkBody : ConditionTestResult.DontWalkBody;
                    case PythonOperator.NotEquals:
                        return s == "win32" && isWindows ? ConditionTestResult.DontWalkBody : ConditionTestResult.WalkBody;
                }
                return ConditionTestResult.DontWalkBody;
            }
            return ConditionTestResult.Unrecognized;
        }

        public static ConditionTestResult TryHandleOsPath(this IfStatementTest test, bool isWindows) {
            if (test.Test is BinaryExpression cmp &&
                cmp.Left is ConstantExpression cex && cex.GetStringValue() is string s &&
                cmp.Right is NameExpression nex && nex.Name == "_names") {
                switch (cmp.Operator) {
                    case PythonOperator.In when s == "nt":
                        return isWindows ? ConditionTestResult.WalkBody : ConditionTestResult.DontWalkBody;
                    case PythonOperator.In when s == "posix":
                        return isWindows ? ConditionTestResult.DontWalkBody : ConditionTestResult.WalkBody;
                }
                return ConditionTestResult.DontWalkBody;
            }
            return ConditionTestResult.Unrecognized;
        }

        public static ConditionTestResult TryHandleBigLittleEndian(this IfStatementTest test, bool isLittleEndian) {
            if (test.Test is BinaryExpression cmp &&
                cmp.Left is MemberExpression me && (me.Target as NameExpression)?.Name == "sys" && me.Name == "byteorder" &&
                cmp.Right is ConstantExpression cex && cex.GetStringValue() is string s) {
                switch (cmp.Operator) {
                    case PythonOperator.Equals when s == "little" && isLittleEndian:
                            return ConditionTestResult.WalkBody;
                    case PythonOperator.Equals when s == "big" && !isLittleEndian:
                            return ConditionTestResult.WalkBody;
                    case PythonOperator.NotEquals when s == "little" && !isLittleEndian:
                            return ConditionTestResult.WalkBody;
                    case PythonOperator.NotEquals when s == "big" && isLittleEndian:
                        return ConditionTestResult.WalkBody;
                }
                return ConditionTestResult.DontWalkBody;
            }
            return ConditionTestResult.Unrecognized;
        }
    }
}
