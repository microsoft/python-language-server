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

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class TypeshedTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task TypeShedSysExcInfo() {
            var code = @"
import sys
e1, e2, e3 = sys.exc_info()
";
            var analysis = await GetAnalysisAsync(code);

            // sys.exc_info() -> (exception_type, exception_value, traceback)
            var f = analysis.Should()
                .HaveVariable("e1").OfType(BuiltinTypeId.Type)
                .And.HaveVariable("e2").OfTypes("BaseException")
                .And.HaveVariable("e3").OfType(BuiltinTypeId.Unknown)
                .And.HaveVariable("sys").OfType(BuiltinTypeId.Module)
                .Which.Should().HaveMember<IPythonFunction>("exc_info").Which;

            f.Overloads.Should().HaveCount(1);
            f.Overloads[0].Documentation.Should().Be("tuple[type, BaseException, Unknown]");
        }

//        [TestMethod, Priority(0)]
//        public async Task TypeShedJsonMakeScanner() {
//            using (var server = await CreateServerAsync()) {
//                server.Analyzer.SetTypeStubPaths(new[] { TestData.GetDefaultTypeshedPath() });
//                var code = @"import _json

//scanner = _json.make_scanner()";
//                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

//                var v0 = analysis.Should().HaveVariable("scanner").WithValueAt<IBuiltinInstanceInfo>(0);

//                v0.Which.Should().HaveSingleOverload()
//                  .Which.Should().HaveName("__call__")
//                  .And.HaveParameters("string", "index")
//                  .And.HaveParameterAt(0).WithName("string").WithType("str").WithNoDefaultValue()
//                  .And.HaveParameterAt(1).WithName("index").WithType("int").WithNoDefaultValue()
//                  .And.HaveSingleReturnType("tuple[object, int]");
//            }
//        }
    }
}
