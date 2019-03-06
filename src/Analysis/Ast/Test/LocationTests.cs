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
    public class LocationTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Assignments() {
            const string code = @"
a = 1
a = 2

for x in y:
    a = 3

if True:
    a = 4
elif False:
    a = 5
else:
    a = 6

def func():
    a = 0

def func(a):
    a = 0

def func():
    global a
    a = 7

class A:
    a: int
";
            var analysis = await GetAnalysisAsync(code);
            var a = analysis.Should().HaveVariable("a").Which;
            a.Locations.Should().HaveCount(7);
            a.Locations[0].Span.Should().Be(2, 1, 2, 2);
            a.Locations[1].Span.Should().Be(3, 1, 3, 2);
            a.Locations[2].Span.Should().Be(6, 5, 6, 6);
            a.Locations[3].Span.Should().Be(9, 5, 9, 6);
            a.Locations[4].Span.Should().Be(11, 5, 11, 6);
            a.Locations[5].Span.Should().Be(13, 5, 13, 6);
            a.Locations[6].Span.Should().Be(23, 5, 23, 6);
        }

        [TestMethod, Priority(0)]
        public async Task NonLocal() {
            const string code = @"
def outer():
    b = 1
    def inner():
        nonlocal b
        b = 2
";
            var analysis = await GetAnalysisAsync(code);
            var outer = analysis.Should().HaveFunction("outer").Which;
            var b = outer.Should().HaveVariable("b").Which;
            b.Locations.Should().HaveCount(2);
            b.Locations[0].Span.Should().Be(3, 5, 3, 6);
            b.Locations[1].Span.Should().Be(6, 9, 6, 10);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionParameter() {
            const string code = @"
def func(a):
    a = 1
    if True:
        a = 2
";
            var analysis = await GetAnalysisAsync(code);
            var outer = analysis.Should().HaveFunction("func").Which;
            var a = outer.Should().HaveVariable("a").Which;
            a.Locations.Should().HaveCount(3);
            a.Locations[0].Span.Should().Be(2, 10, 2, 11);
            a.Locations[1].Span.Should().Be(3, 5, 3, 6);
            a.Locations[2].Span.Should().Be(5, 9, 5, 10);
        }
    }
}
