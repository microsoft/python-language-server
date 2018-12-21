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
using Microsoft.Python.Tests.Utilities.FluentAssertions;
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
        public async Task AnnotatedAssign01() {
            var code = @"
x : int = 42

class C:
    y : int = 42

    def func(self):
        self.abc : int = 42

a = C()
a.func()
fob1 = a.abc
fob2 = a.y
fob3 = x
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("fob1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("fob2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("fob3").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("a").Which.Type.Should().BeAssignableTo<IPythonConstant>()
                .Which.Should().HaveMembers("abc", "func", "y", "__doc__", "__class__");
        }

        [TestMethod, Priority(0)]
        public async Task AnnotatedAssign02() {
            var code = @"
def f(val):
    print(val)

class C:
    def __init__(self, y):
        self.y = y

x:f(42) = 1
x:C(42) = 1
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0).Which.Should().HaveName("val").And.HaveType(BuiltinTypeId.Int);

            analysis.Should().HaveClass("C")
                .Which.Should().HaveMethod("__init__")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(1)
                .Which.Should().HaveName("y").And.HaveType(BuiltinTypeId.Int);
        }

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
            analysis.Should().HaveClass("x")
                .Which.Should().HaveMethod("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0)
                .Which.Should().HaveName("self").And.HaveType("x")
                .Which.Should().HaveMember<IPythonType>("x").Which.TypeId.Should().Be(BuiltinTypeId.Str);
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
    }
}
