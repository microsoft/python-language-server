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
    public class IteratorTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

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

            analysis.Should().HaveVariable("A").OfType(BuiltinTypeId.List)
                .And.HaveVariable("B").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("C").OfType(BuiltinTypeId.List);

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

            analysis.Should().HaveVariable("A").OfType(BuiltinTypeId.List)
                .And.HaveVariable("B").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("C").OfType(BuiltinTypeId.List);

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
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float);
        }

        //[TestMethod, Priority(0)]
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

            analysis.Should().HaveVariable("A").OfType(BuiltinTypeId.List)
                .And.HaveVariable("B").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("C").OfType(BuiltinTypeId.List);


            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float);
        }

        //[TestMethod, Priority(0)]
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
