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

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
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
        public async Task Methods() {
            const string code = @"
class A:
    def methodA(self):
        return func()

class B(A):
    def methodB(self):
        return self.methodA()

a = A()
a.methodA()

b = B()
b.methodB()
b.methodA()

ma1 = a.methodA
ma2 = b.methodA
mb1 = b.methodB
";
            var analysis = await GetAnalysisAsync(code);

            var classA = analysis.Should().HaveVariable("A").Which;
            var methodA = classA.Should().HaveMember<IPythonFunctionType>("methodA").Which;
            methodA.Definition.Span.Should().Be(3, 9, 3, 16);
            methodA.References.Should().HaveCount(6);
            methodA.References[0].Span.Should().Be(3, 9, 3, 16);
            methodA.References[1].Span.Should().Be(8, 21, 8, 28);
            methodA.References[2].Span.Should().Be(11, 3, 11, 10);
            methodA.References[3].Span.Should().Be(15, 3, 15, 10);
            methodA.References[4].Span.Should().Be(17, 9, 17, 16);
            methodA.References[5].Span.Should().Be(18, 9, 18, 16);

            var classB = analysis.Should().HaveVariable("B").Which;
            var methodB = classB.Should().HaveMember<IPythonFunctionType>("methodB").Which;
            methodB.Definition.Span.Should().Be(7, 9, 7, 16);
            methodB.References.Should().HaveCount(3);
            methodB.References[0].Span.Should().Be(7, 9, 7, 16);
            methodB.References[1].Span.Should().Be(14, 3, 14, 10);
            methodB.References[2].Span.Should().Be(19, 9, 19, 16);
        }

        [TestMethod, Priority(0)]
        public async Task Function() {
            const string code = @"
def func():
    return 1

class A:
    def methodA(self):
        return func()

func()
a = func
";
            var analysis = await GetAnalysisAsync(code);

            var func = analysis.Should().HaveVariable("func").Which;
            func.Definition.Span.Should().Be(2, 5, 2, 9);
            func.References.Should().HaveCount(4);
            func.References[0].Span.Should().Be(2, 5, 2, 9);
            func.References[1].Span.Should().Be(7, 16, 7, 20);
            func.References[2].Span.Should().Be(9, 1, 9, 5);
            func.References[3].Span.Should().Be(10, 5, 10, 9);
        }

        [TestMethod, Priority(0)]
        public async Task Property() {
            const string code = @"
class A:
    @property
    def propA(self): ...

class B(A): ...

x = B().propA
";
            var analysis = await GetAnalysisAsync(code);

            var classA = analysis.Should().HaveVariable("A").Which;
            var propA = classA.Should().HaveMember<IPythonPropertyType>("propA").Which;
            propA.Definition.Span.Should().Be(4, 9, 4, 14);
            propA.References.Should().HaveCount(2);
            propA.References[0].Span.Should().Be(4, 9, 4, 14);
            propA.References[1].Span.Should().Be(8, 9, 8, 14);
        }

        [TestMethod, Priority(0)]
        public async Task ClassBase() {
            const string code = @"
class A: ...
class B(A): ...
";
            var analysis = await GetAnalysisAsync(code);

            var classA = analysis.Should().HaveVariable("A").Which;
            classA.Definition.Span.Should().Be(2, 7, 2, 8);
            classA.References.Should().HaveCount(2);
            classA.References[0].Span.Should().Be(2, 7, 2, 8);
            classA.References[1].Span.Should().Be(3, 9, 3, 10);
        }

        [TestMethod, Priority(0)]
        public async Task Global() {
            const string code = @"
z1 = 1
z2 = 1

class A:
    def method(self):
        z1 = 3
        global z2
        z2 = 4
";
            var analysis = await GetAnalysisAsync(code);

            var z1 = analysis.Should().HaveVariable("z1").Which;
            z1.Definition.Span.Should().Be(2, 1, 2, 3);
            z1.References.Should().HaveCount(1);
            z1.References[0].Span.Should().Be(2, 1, 2, 3);

            var z2 = analysis.Should().HaveVariable("z2").Which;
            z2.Definition.Span.Should().Be(3, 1, 3, 3);
            z2.References.Should().HaveCount(3);
            z2.References[0].Span.Should().Be(3, 1, 3, 3);
            z2.References[1].Span.Should().Be(8, 16, 8, 18);
            z2.References[2].Span.Should().Be(9, 9, 9, 11);
        }

        [TestMethod, Priority(0)]
        public async Task Nonlocal() {
            const string code = @"
z1 = 1

class A:
    def method(self):
        z1 = 2
        def inner(self):
            nonlocal z1
            z1 = 3
";
            var analysis = await GetAnalysisAsync(code);

            var z1 = analysis.Should().HaveVariable("z1").Which;
            z1.Definition.Span.Should().Be(2, 1, 2, 3);
            z1.References.Should().HaveCount(1);
            z1.References[0].Span.Should().Be(2, 1, 2, 3);

            var classA = analysis.Should().HaveVariable("A").Which;
            var method = classA.Should().HaveMember<IPythonFunctionType>("method").Which;
            var z1Method = method.Should().HaveVariable("z1").Which;
            z1Method.Definition.Span.Should().Be(6, 9, 6, 11);
            z1Method.References.Should().HaveCount(3);
            z1Method.References[0].Span.Should().Be(6, 9, 6, 11);
            z1Method.References[1].Span.Should().Be(8, 22, 8, 24);
            z1Method.References[2].Span.Should().Be(9, 13, 9, 15);
        }
    }
}
