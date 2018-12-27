﻿// Copyright(c) Microsoft Corporation
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
    public class CollectionsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ListCtor() {
            const string code = @"
l1 = [1, 'str', 3.0]
x0 = l1[0]
x1 = l1[1]
x2 = l1[2]
x3 = l1[3]
x4 = l1[x0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("x0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x2").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("x3").WithNoTypes()
                .And.HaveVariable("x4").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListNegativeIndex() {
            const string code = @"
l1 = [1, 'str', 3.0]
x0 = l1[-1]
x1 = l1[-2]
x2 = l1[-3]
x3 = l1[x2]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("x0").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x3").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListSlice() {
            const string code = @"
l1 = [1, 'str', 3.0, 2, 3, 4]
l2 = l1[2:4]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("l2").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task ListRecursion() {
            const string code = @"
def f(x):
    print abc
    return f(list(x))

abc = f(())
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("abc");
        }

        [TestMethod, Priority(0)]
        public async Task ListSubclass() {
            const string code = @"
class C(list):
    pass

a = C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").Which.Should().HaveMember("count");
        }

        [TestMethod, Priority(0)]
        public async Task Iterator1_V2() {
            const string code = @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = iter(A)
iB = iter(B)
iC = iter(C)

a = iA.next()
b = iB.next()
c1 = iC.next()
c2 = iC.next()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);

            analysis.Should().HaveVariable("iA").OfType(BuiltinTypeId.ListIterator)
                .And.HaveVariable("iB").OfType(BuiltinTypeId.StrIterator)
                .And.HaveVariable("iC").OfType(BuiltinTypeId.ListIterator);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c1").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("c2").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task Iterator2_V2() {
            const string code = @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = A.__iter__()
iB = B.__iter__()
iC = C.__iter__()

a = iA.next()
b = iB.next()
c1 = iC.next()
c2 = iC.next()

";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("iA").OfType(BuiltinTypeId.ListIterator)
                .And.HaveVariable("iB").OfType(BuiltinTypeId.StrIterator)
                .And.HaveVariable("iC").OfType(BuiltinTypeId.ListIterator);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c1").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("c2").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task Iterator3_V2() {
            const string code = @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA, iB, iC = A.__iter__(), B.__iter__(), C.__iter__()
a = iA.next()
b = next(iB)
_next = next
c = _next(iC)
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task Iterator4_V2() {
            const string code = @"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task Iterator1_V3() {
            const string code = @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA, iB, iC = A.__iter__(), B.__iter__(), C.__iter__()
a = iA.__next__()
b = next(iB)
_next = next
c = _next(iC)
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task Iterator2_V3() {
            const string code = @"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int);
        }
    }
}
