// Python Tools for Visual Studio
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
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    public class ServerTestMethodAttribute : TestMethodAttribute {
        public bool LatestAvailable2X { get; set; }
        public int VersionArgumentIndex { get; set; } = -1;

        public override TestResult[] Execute(ITestMethod testMethod) {
            if (testMethod.ParameterTypes[0].ParameterType == typeof(Server) && (testMethod.Arguments == null || testMethod.ParameterTypes.Length == testMethod.Arguments.Length + 1)) {
                return new[] { ExecuteWithServer(testMethod) };
            }

            return base.Execute(testMethod);
        }

        private TestResult ExecuteWithServer(ITestMethod testMethod) {
            var arguments = ExtendArguments(testMethod.Arguments);

            TestEnvironmentImpl.AddBeforeAfterTest(async () => {
                var interpreterConfiguration = GetInterpreterConfiguration(arguments);
                var server = await new Server().InitializeAsync(interpreterConfiguration);
                arguments[0] = server;
                return server;
            });

            return testMethod.Invoke(arguments);
        }

        private InterpreterConfiguration GetInterpreterConfiguration(object[] arguments) {
            return VersionArgumentIndex >= 0 && VersionArgumentIndex < arguments.Length 
                ? PythonVersions.GetRequiredCPythonConfiguration((PythonLanguageVersion)arguments[VersionArgumentIndex])
                : LatestAvailable2X ? PythonVersions.LatestAvailable2X : PythonVersions.LatestAvailable;
        }

        private static object[] ExtendArguments(object[] arguments) {
            if (arguments == null || arguments.Length == 0) {
                return new object[1];
            }

            var length = arguments.Length;
            var args = new object[length + 1];
            Array.Copy(arguments, 0, args, 1, length);
            return args;
        }
    }
}