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

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ReferencesTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicReferences() {
            const string code = @"
z1 = 1
z1 = 2
z2 = 3

def func():
    return 1

class A:
    x: int
    def methodA(self):
        return func()

    @property
    def propA(self): ...

class B(A):
    y: str
    def methodB(self):
        z1 = 3
        global z2
        z2 = 4
        return self.methodA()

func()
a = A()
a.methodA()

b = B()
b.methodB()
b.methodA()

";
            var analysis = await GetAnalysisAsync(code);

            var z1 = analysis.Should().HaveVariable("z1").Which;
            z1.Definition.Span.Should().Be(2, 1, 2, 3);
            z1.References.Should().HaveCount(2);
            z1.References[0].Span.Should().Be(2, 1, 2, 3);
            z1.References[1].Span.Should().Be(3, 1, 3, 3);

            var z2 = analysis.Should().HaveVariable("z2").Which;
            z2.Definition.Span.Should().Be(4, 1, 4, 3);
            z2.References.Should().HaveCount(3);
            z2.References[0].Span.Should().Be(4, 1, 4, 3);
            z2.References[1].Span.Should().Be(21, 16, 21, 18);
            z2.References[2].Span.Should().Be(22, 9, 22, 11);

            var func = analysis.Should().HaveVariable("func").Which;
            func.Definition.Span.Should().Be(6, 5, 6, 9);
            func.References.Should().HaveCount(3);
            func.References[0].Span.Should().Be(6, 5, 6, 9);
            func.References[1].Span.Should().Be(12, 16, 12, 20);
            func.References[2].Span.Should().Be(25, 1, 25, 5);

            var classA = analysis.Should().HaveVariable("A").Which;
            classA.Definition.Span.Should().Be(9, 7, 9, 8);
            classA.References.Should().HaveCount(3);
            classA.References[0].Span.Should().Be(9, 7, 9, 8);
            classA.References[1].Span.Should().Be(17, 9, 17, 10);
            classA.References[2].Span.Should().Be(26, 5, 26, 6);
        }
    }
}
