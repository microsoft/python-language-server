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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class AssignmentTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task AssignSelf() {
            var code = @"
class x(object):
    def __init__(self):
        self.x = 'abc'
    def f(self):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            var cls = analysis.Should().HaveClass("x").Which;

            var xType = cls.Should().HaveMethod("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0)
                .Which.Should().HaveName("self").And.HaveType("x").Which;

            xType.Should().HaveMember<IPythonType>("x")
                .Which.TypeId.Should().Be(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task AssignToMissingMember() {
            var code = @"
class test():
    x = 0;
    y = 1;
t = test()
t.x, t. =
";
            // This just shouldn't crash, we should handle the malformed code
            await GetAnalysisAsync(code);
        }

        [TestMethod, Priority(0)]
        public async Task Backquote() {
            var analysis = await GetAnalysisAsync(@"x = `42`", PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Bytes);
        }

        [TestMethod, Priority(0)]
        public async Task BadKeywordArguments() {
            var code = @"def f(a, b):
    return a

x = 100
z = f(a=42, x)";

            var analysis = await GetAnalysisAsync(code);
                analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);
        }
    }
}
