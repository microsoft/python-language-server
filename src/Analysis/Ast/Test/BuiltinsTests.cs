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

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class BuiltinsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() { 
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow(true, true)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(false, false)]
        [DataTestMethod, Priority(0)]
        public async Task BuiltinsTest(bool isPython3X, bool isAnaconda) {
            const string code = @"
x = 1
";
            var configuration = isPython3X 
                ? isAnaconda ? PythonVersions.LatestAnaconda3X : PythonVersions.LatestAvailable3X 
                : isAnaconda ? PythonVersions.LatestAnaconda2X : PythonVersions.LatestAvailable2X;
            var analysis = await GetAnalysisAsync(code, configuration);

            var v = analysis.Should().HaveVariable("x").Which;
            var t = v.Value.GetPythonType();
            t.Should().BeAssignableTo<IMemberContainer>();

            var mc = (IMemberContainer)t;
            var names = mc.GetMemberNames().ToArray();
            names.Length.Should().BeGreaterThan(50);
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinsTrueFalse() {
            const string code = @"
booltypetrue = True
booltypefalse = False
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"booltypetrue").OfType(BuiltinTypeId.Bool)
                .And.HaveVariable(@"booltypefalse").OfType(BuiltinTypeId.Bool);
        }

        [DataRow(true, true)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(false, false)]
        [DataTestMethod, Priority(0)]
        public async Task UnknownType(bool isPython3X, bool isAnaconda) {
            const string code = @"x = 1";

            var configuration = isPython3X
                ? isAnaconda ? PythonVersions.LatestAnaconda3X : PythonVersions.LatestAvailable3X
                : isAnaconda ? PythonVersions.LatestAnaconda2X : PythonVersions.LatestAvailable2X;
            var analysis = await GetAnalysisAsync(code, configuration);

            var unkType = analysis.Document.Interpreter.UnknownType;
            unkType.TypeId.Should().Be(BuiltinTypeId.Unknown);
        }

        [TestMethod, Priority(0)]
        public async Task Type() {
            const string code = @"
class _C:
    def _m(self): pass
MethodType = type(_C()._m)";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"MethodType").OfType(BuiltinTypeId.Method);
        }
    }
}
