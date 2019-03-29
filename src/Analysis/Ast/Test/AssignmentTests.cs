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

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
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
            const string code = @"
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

            xType.Should().HaveMember<IPythonInstance>("x")
                .Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task AssignToMissingMember() {
            const string code = @"
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
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task BadKeywordArguments() {
            const string code = @"
def f(a, b):
    return a

x = 100
z = f(a=42, x)";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task AssignBytes() {
            const string code = @"
x = b'b'
y = u'u'
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task AssignUnicode() {
            const string code = @"
x = b'b'
y = u'u'
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task Ellipsis() {
            var analysis = await GetAnalysisAsync(@"x = ...");
            analysis.Should().HaveVariable("x").WithNoTypes();
        }
        [TestMethod, Priority(0)]
        public async Task NegativeNumbersV2() {
            const string code = @"
x = -1
y = -3.0
z = -4L
a = z
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Long)
                .And.HaveVariable("a").OfType(BuiltinTypeId.Long);
        }

        [TestMethod, Priority(0)]
        public async Task NegativeNumbersV3() {
            const string code = @"
x = -1
y = -3.0
z = -4L
a = z
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("a").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task Tuple() {
            const string code = @"
x, y, z = 1, 'str', 3.0
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task TupleUnknownReturn() {
            const string code = @"
x, y, z = func()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").And.HaveVariable("y").And.HaveVariable("z");
        }

        [TestMethod, Priority(0)]
        public async Task NestedTuple() {
            const string code = @"
((x, r), y, z) = (1, 1), '', False,
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("r").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("z").Which.Should().HaveType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task NestedTupleSingleValue() {
            const string code = @"
(x, (y, (z))) = 1,
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").And.HaveVariable("y").And.HaveVariable("z");
        }

        [TestMethod, Priority(0)]
        public async Task List() {
            const string code = @"
[year, month] = (1, 2)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("year").And.HaveVariable("month");
        }

        [TestMethod, Priority(0)]
        public async Task AnnotatedAssign() {
            const string code = @"
x : int = 42

class C:
    y : int = 42

    def __init__(self):
        self.abc : int = 42

a = C()
fob1 = a.abc
fob2 = a.y
fob3 = x
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("fob1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("fob2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("fob3").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("a")
                .Which.Should().HaveMembers("abc", "y", "__class__");
        }

        [TestMethod, Priority(0)]
        public async Task BaseInstanceVariable() {
            const string code = @"
class C:
    def __init__(self):
        self.abc = 42

class D(C):
    def __init__(self):
        self.fob = self.abc
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Should().HaveClass("D").Which;

            d.Should().HaveMethod("__init__")
            .Which.Should().HaveSingleOverload()
            .Which.Should().HaveParameterAt(0).Which.Name.Should().Be("self");

            d.Should().HaveMember<IPythonConstant>("fob")
                .Which.Should().HaveType(BuiltinTypeId.Int);
            d.Should().HaveMember<IPythonConstant>("abc")
                .Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task LambdaExpression1() {
            const string code = @"
x = lambda a: a
y = x(42)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task LambdaExpression2() {
            const string code = @"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign1() {
            const string code = @"
class C:
    def func(self):
        self.abc = 42

a = C()
a.func()
fob = a.abc
";
            var analysis = await GetAnalysisAsync(code);
            var intMemberNames = analysis.Document.Interpreter.GetBuiltinType(BuiltinTypeId.Int).GetMemberNames();

            analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.Int)
                .Which.Should().HaveMembers(intMemberNames);
            analysis.Should().HaveVariable("a")
                .Which.Should().HaveMembers("abc", "func", "__class__");
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign2() {
            const string code = @"
class D:
    def func2(self):
        a = C()
        a.func()
        return a.abc

class C:
    def func(self):
        self.abc = [2,3,4]

fob = D().func2()
";
            var analysis = await GetAnalysisAsync(code);
            var listMemberNames = analysis.Document.Interpreter.GetBuiltinType(BuiltinTypeId.List).GetMemberNames();

            analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.List)
                .Which.Should().HaveMembers(listMemberNames);
        }

        [TestMethod, Priority(0)]
        public async Task StrIndex() {
            const string code = @"
a = 'abc'
x = a[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task IncompleteTuple() {
            const string code = @"
a, b = 1
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int);
        }
    }
}
