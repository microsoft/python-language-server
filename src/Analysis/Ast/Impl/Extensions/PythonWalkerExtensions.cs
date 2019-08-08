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
            var executeElse = false;
            foreach (var test in node.Tests) {

                var result = test.TryHandleSysVersionInfo(languageVersion);
                if (result == ConditionTestResult.Unrecognized) {
                    result = test.TryHandleSysPlatform(isWindows);
                    if (result == ConditionTestResult.Unrecognized) {
                        result = test.TryHandleOsPath(isWindows);
                    }
                }

                // If condition is satisfied, walk the corresponding block and 
                // return false indicating that statement should not be walked again.
                // If condition is false or was not recognized, continue but remember
                // if we need to execute final else clause.
                switch (result) {
                    case ConditionTestResult.WalkBody:
                        test.Walk(walker);
                        return false; // We only need to execute one of the clauses.
                    case ConditionTestResult.DontWalkBody:
                        // If condition is false, continue but remember
                        // if we may need to execute the final else clause.
                        executeElse = true;
                        break;
                    case ConditionTestResult.Unrecognized:
                        continue; // See if other conditions may work.
                }
            }

            if (executeElse) {
                node.ElseStatement?.Walk(walker);
                return false;
            }

            // We didn't walk anything, so me caller do their own thing.
            return true;
        }
    }
}
