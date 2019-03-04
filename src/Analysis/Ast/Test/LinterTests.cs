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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LinterTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Variable() {
            const string code = @"
import sys
e1, e2, e3 = sys.exc_info()
";
            var analysis = await GetAnalysisAsync(code);
            // sys.exc_info() -> (exception_type, exception_value, traceback)
            var f = analysis.Should()
                .HaveVariable("e1").OfType(BuiltinTypeId.Type)
                .And.HaveVariable("e2").OfType("BaseException")
                .And.HaveVariable("e3").OfType("TracebackType")
                .And.HaveVariable("sys").OfType(BuiltinTypeId.Module)
                .Which.Should().HaveMember<IPythonFunctionType>("exc_info").Which;

            f.Overloads.Should().HaveCount(1);
            f.Overloads[0].GetReturnDocumentation(null)
                .Should().Be(@"Tuple[ (Optional[Type[BaseException]],Optional[BaseException],Optional[TracebackType])]");
        }

        [TestMethod, Priority(0)]
        public async Task TypeShedJsonMakeScanner() {
            const string code = @"import _json
scanner = _json.make_scanner()";
            var analysis = await GetAnalysisAsync(code);

            var v0 = analysis.Should().HaveVariable("scanner").Which;

            v0.Should().HaveMember<IPythonFunctionType>("__call__")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveName("__call__")
                    .And.HaveParameters("self", "string", "index")
                    .And.HaveParameterAt(1).WithName("string").WithType("str").WithNoDefaultValue()
                    .And.HaveParameterAt(2).WithName("index").WithType("int").WithNoDefaultValue()
                    .And.HaveReturnDocumentation("Tuple[ (Any,int)]");
        }

        [TestMethod, Priority(0)]
        public async Task MergeStubs() {
            var analysis = await GetAnalysisAsync("import Package.Module\n\nc = Package.Module.Class()");

            analysis.Should()
                .HaveVariable("Package")
                    .Which.Value.Should().HaveMember<IPythonModule>("Module");

            analysis.Should().HaveVariable("c")
                .Which.Value.Should().HaveMembers("untyped_method", "inferred_method", "typed_method")
                .And.NotHaveMembers("typed_method_2");
        }

        [TestMethod, Priority(0)]
        public async Task TypeStubConditionalDefine() {
            var seen = new HashSet<Version>();

            const string code = @"import sys

if sys.version_info < (2, 7):
    LT_2_7 : bool = ...
if sys.version_info <= (2, 7):
    LE_2_7 : bool = ...
if sys.version_info > (2, 7):
    GT_2_7 : bool = ...
if sys.version_info >= (2, 7):
    GE_2_7 : bool = ...

";

            var fullSet = new[] { "LT_2_7", "LE_2_7", "GT_2_7", "GE_2_7" };

            foreach (var ver in PythonVersions.Versions) {
                if (!seen.Add(ver.Version)) {
                    continue;
                }

                Console.WriteLine(@"Testing with {0}", ver.InterpreterPath);
                using (var s = await CreateServicesAsync(TestData.Root, ver)) {
                    var analysis = await GetAnalysisAsync(code, s, @"testmodule", TestData.GetTestSpecificPath(@"testmodule.pyi"));

                    var expected = new List<string>();
                    var pythonVersion = ver.Version.ToLanguageVersion();
                    if (pythonVersion.Is3x()) {
                        expected.Add("GT_2_7");
                        expected.Add("GE_2_7");
                    } else if (pythonVersion == PythonLanguageVersion.V27) {
                        expected.Add("GE_2_7");
                        expected.Add("LE_2_7");
                    } else {
                        expected.Add("LT_2_7");
                        expected.Add("LE_2_7");
                    }

                    analysis.GlobalScope.Variables.Select(m => m.Name).Where(n => n.EndsWithOrdinal("2_7"))
                        .Should().Contain(expected)
                        .And.NotContain(fullSet.Except(expected));
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeShedNoTypeLeaks() {
            const string code = @"import json
json.dump()
x = 1";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("json")
                .Which.Should().HaveMember("dump");

            analysis.Should().HaveVariable("x")
                .Which.Should().HaveMember("bit_length")
                .And.NotHaveMember("SIGABRT");
        }

        [TestMethod, Priority(0)]
        public async Task VersionCheck() {
            const string code = @"
import asyncio
loop = asyncio.get_event_loop()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should()
                .HaveVariable("loop")
                .Which.Value.Should().HaveMembers("add_reader", "add_writer", "call_at", "close");
        }
    }
}
