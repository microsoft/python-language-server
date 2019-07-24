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

using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class PythonWalkerExtensions {
        public static bool WalkIfWithSystemConditions(this IfStatement node, PythonWalker walker, PythonLanguageVersion languageVersion, bool isWindows) {
            // System version, platform and os.path specializations
            var someRecognized = false;
            foreach (var test in node.Tests) {
                var result = test.TryHandleSysVersionInfo(languageVersion);
                if (result != ConditionTestResult.Unrecognized) {
                    if (result == ConditionTestResult.WalkBody) {
                        test.Walk(walker);
                    } else {
                        node.ElseStatement?.Walk(walker);
                    }
                    someRecognized = true;
                    continue;
                }

                result = test.TryHandleSysPlatform(isWindows);
                if (result != ConditionTestResult.Unrecognized) {
                    if (result == ConditionTestResult.WalkBody) {
                        test.Walk(walker);
                    } else {
                        node.ElseStatement?.Walk(walker);
                    }
                    someRecognized = true;
                    continue;
                }

                result = test.TryHandleOsPath(isWindows);
                if (result != ConditionTestResult.Unrecognized) {
                    if (result == ConditionTestResult.WalkBody) {
                        test.Walk(walker);
                    } else {
                        node.ElseStatement?.Walk(walker);
                    }
                    return false; // Execute only one condition.
                }
            }
            return !someRecognized;
        }
    }
}
