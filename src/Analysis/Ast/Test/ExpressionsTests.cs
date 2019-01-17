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
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ExpressionsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ConstantMathV2() {
            const string code = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
e = 1 + 1L # e is a 'float', should be 'long' under v2.x (error under v3.x)
f = 1 / 2 # f is 'int', should be 'float' under v3.x";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("e").OfType(BuiltinTypeId.Long)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ConstantMathV3() {
            const string code = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
f = 1 / 2 # f is 'int', should be 'float' under v3.x";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task StringMultiplyV2() {
            const string code = @"
x = u'abc %d'
y = x * 100

x1 = 'abc %d'
y1 = x1 * 100

fob = 'abc %d'.lower()
oar = fob * 100

fob2 = u'abc' + u'%d'
oar2 = fob2 * 100"; ;

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("fob2").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task StringMultiplyV3() {
            const string code = @"
x = u'abc %d'
y = x * 100


x1 = 'abc %d'
y1 = x1 * 100

fob = 'abc %d'.lower()
oar = fob * 100

fob2 = u'abc' + u'%d'
oar2 = fob2 * 100"; ;

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("fob2").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar2").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task StringConcatenation() {
            const string code = @"
x = u'abc'
y = x + u'dEf'

x1 = 'abc'
y1 = x1 + 'def'

fob = 'abc'.lower()
oar = fob + 'Def'

fob2 = u'ab' + u'cd'
oar2 = fob2 + u'ef'";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task StringFormattingV2() {
            const string code = @"
x = u'abc %d'
y = x % (42, )

x1 = 'abc %d'
y1 = x1 % (42, )

fob = 'abc %d'.lower()
oar = fob % (42, )

fob2 = u'abc' + u'%d'
oar2 = fob2 % (42, )
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task StringFormattingV3() {
            var code = @"
y = f'abc {42}'
ry = rf'abc {42}'
yr = fr'abc {42}'
fadd = f'abc{42}' + f'{42}'

def f(val):
    print(val)
f'abc {f(42)}'
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("ry").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("yr").OfType(BuiltinTypeId.Str)
                .And.HaveVariable(@"fadd").OfType(BuiltinTypeId.Str);
            // TODO: Enable analysis of f-strings
            //    .And.HaveVariable("val",  BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task RangeIteration() {
            const string code = @"
for i in range(5):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("i")
                .Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task Sum() {
            const string code = @"
s = sum(i for i in [0,1])
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("s")
                .Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task NotOperator() {
            const string code = @"
class C(object):
    def __nonzero__(self):
        pass

    def __bool__(self):
        pass

a = not C()
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task SequenceConcat() {
            const string code = @"
x1 = ()
y1 = x1 + ()
y1v = y1[0]

x2 = (1,2,3)
y2 = x2 + (4.0,5.0,6.0)
y2v = y2[0]

x3 = [1,2,3]
y3 = x3 + [4.0,5.0,6.0]
y3v = y3[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x1").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("y1v").WithNoTypes()
                .And.HaveVariable("y2").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("y2v").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y3").OfType(BuiltinTypeId.List)
                .And.HaveVariable("y3v").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task SequenceMultiply() {
            var code = @"
x = ()
y = x * 100

x1 = (1,2,3)
y1 = x1 * 100

fob = [1,2,3]
oar = fob * 100

fob2 = []
oar2 = fob2 * 100";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType("tuple")
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("oar").OfType("list")
                .And.HaveVariable("oar2").OfType(BuiltinTypeId.List);
        }
    }
}
