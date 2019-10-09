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

        [TestMethod, Priority(0)]
        public async Task Import() {
            const string code = @"
import sys as s
x = s.path
";
            var analysis = await GetAnalysisAsync(code);
            var s = analysis.Should().HaveVariable("s").Which;
            s.Definition.Span.Should().Be(2, 15, 2, 16);
            s.References.Should().HaveCount(2);
            s.References[0].Span.Should().Be(2, 15, 2, 16);
            s.References[1].Span.Should().Be(3, 5, 3, 6);
        }

        [TestMethod, Priority(0)]
        public async Task FromImportAs() {
            const string code = @"
from sys import path as p
x = p
";
            var analysis = await GetAnalysisAsync(code);
            var p = analysis.Should().HaveVariable("p").Which;
            p.Definition.Span.Should().Be(2, 25, 2, 26);
            p.References.Should().HaveCount(2);
            p.References[0].Span.Should().Be(2, 25, 2, 26);
            p.References[1].Span.Should().Be(3, 5, 3, 6);
        }

        [TestMethod, Priority(0)]
        public async Task FromImport() {
            const string code = @"
from sys import path
x = path
";
            var analysis = await GetAnalysisAsync(code);
            var p = analysis.Should().HaveVariable("path").Which;
            p.Definition.Span.Should().Be(2, 17, 2, 21);
            p.References.Should().HaveCount(2);
            p.References[0].Span.Should().Be(2, 17, 2, 21);
            p.References[1].Span.Should().Be(3, 5, 3, 9);
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
            a.References.Should().HaveCount(3);
            a.References[0].Span.Should().Be(2, 10, 2, 11);
            a.References[1].Span.Should().Be(3, 5, 3, 6);
            a.References[2].Span.Should().Be(5, 9, 5, 10);
        }

        [TestMethod, Priority(0)]
        public async Task ClassField() {
            const string code = @"
class A:
    x = 0

def func(a: A):
    return a.x
";
            var analysis = await GetAnalysisAsync(code);
            var x = analysis.GlobalScope.Children[0].Should().HaveVariable("x").Which;
            x.Should().NotBeNull();
            x.References.Should().HaveCount(2);
            x.References[0].Span.Should().Be(3, 5, 3, 6);
            x.References[1].Span.Should().Be(6, 14, 6, 15);
        }

        [TestMethod, Priority(0)]
        public async Task ClassFieldAnnotation() {
            const string code = @"
class A:
    x: int
    x = 0
";
            var analysis = await GetAnalysisAsync(code);
            var x = analysis.GlobalScope.Children[0].Should().HaveVariable("x").Which;
            x.Should().NotBeNull();
            x.References.Should().HaveCount(2);
            x.References[0].Span.Should().Be(3, 5, 3, 6);
            x.References[1].Span.Should().Be(4, 5, 4, 6);
        }

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
            a.References.Should().HaveCount(8);
            a.References[0].Span.Should().Be(2, 1, 2, 2);
            a.References[1].Span.Should().Be(3, 1, 3, 2);
            a.References[2].Span.Should().Be(6, 5, 6, 6);
            a.References[3].Span.Should().Be(9, 5, 9, 6);
            a.References[4].Span.Should().Be(11, 5, 11, 6);
            a.References[5].Span.Should().Be(13, 5, 13, 6);
            a.References[6].Span.Should().Be(22, 12, 22, 13);
            a.References[7].Span.Should().Be(23, 5, 23, 6);
        }

        [TestMethod, Priority(0)]
        public async Task ImportSpecific() {
            const string code = @"
from MultiValues import t
x = t
";
            var analysis = await GetAnalysisAsync(code);
            var t = analysis.Should().HaveVariable("t").Which as IImportedMember;
            t.Should().NotBeNull();
            t.Definition.Span.Should().Be(2, 25, 2, 26);
            t.Definition.DocumentUri.AbsolutePath.Should().Contain("module.py");
            t.References.Should().HaveCount(2);
            t.References[0].Span.Should().Be(2, 25, 2, 26);
            t.References[0].DocumentUri.AbsolutePath.Should().Contain("module.py");
            t.References[1].Span.Should().Be(3, 5, 3, 6);
            t.References[1].DocumentUri.AbsolutePath.Should().Contain("module.py");

            var parent = t.Parent;
            parent.Should().NotBeNull();
            parent.References.Should().HaveCount(3);
            parent.References[0].Span.Should().Be(3, 1, 3, 2);
            parent.References[0].DocumentUri.AbsolutePath.Should().Contain("MultiValues.py");
            parent.References[1].Span.Should().Be(2, 25, 2, 26);
            parent.References[1].DocumentUri.AbsolutePath.Should().Contain("module.py");
            parent.References[2].Span.Should().Be(3, 5, 3, 6);
            parent.References[2].DocumentUri.AbsolutePath.Should().Contain("module.py");
        }

        [TestMethod, Priority(0)]
        public async Task ImportStar() {
            const string code = @"
from MultiValues import *
x = t
";
            var analysis = await GetAnalysisAsync(code);
            var t = analysis.Should().HaveVariable("t").Which;
            t.Definition.Span.Should().Be(3, 1, 3, 2);
            t.Definition.DocumentUri.AbsolutePath.Should().Contain("MultiValues.py");
            t.References.Should().HaveCount(2);
            t.References[0].Span.Should().Be(3, 1, 3, 2);
            t.References[0].DocumentUri.AbsolutePath.Should().Contain("MultiValues.py");
            t.References[1].Span.Should().Be(3, 5, 3, 6);
            t.References[1].DocumentUri.AbsolutePath.Should().Contain("module.py");
        }

        [TestMethod, Priority(0)]
        public async Task Conditional() {
            const string code = @"
x = 1
y = 2

if x < y:
    pass
";
            var analysis = await GetAnalysisAsync(code);
            var x = analysis.Should().HaveVariable("x").Which;
            x.Definition.Span.Should().Be(2, 1, 2, 2);
            x.References.Should().HaveCount(2);
            x.References[0].Span.Should().Be(2, 1, 2, 2);
            x.References[1].Span.Should().Be(5, 4, 5, 5);

            var y = analysis.Should().HaveVariable("y").Which;
            y.Definition.Span.Should().Be(3, 1, 3, 2);
            y.References.Should().HaveCount(2);
            y.References[0].Span.Should().Be(3, 1, 3, 2);
            y.References[1].Span.Should().Be(5, 8, 5, 9);
        }

        [TestMethod, Priority(0)]
        public async Task AugmentedAssign() {
            const string code = @"
def func(a, b):
    dest = 1
    dest += src
    dest[dest < src] = np.iinfo(dest.dtype).max
";
            var analysis = await GetAnalysisAsync(code);
            var dest = analysis.GlobalScope.Children[0].Should().HaveVariable("dest").Which;
            dest.Definition.Span.Should().Be(3, 5, 3, 9);
            dest.References.Should().HaveCount(5);
            dest.References[0].Span.Should().Be(3, 5, 3, 9);
            dest.References[1].Span.Should().Be(4, 5, 4, 9);
            dest.References[2].Span.Should().Be(5, 5, 5, 9);
            dest.References[3].Span.Should().Be(5, 10, 5, 14);
            dest.References[4].Span.Should().Be(5, 33, 5, 37);
        }

        [TestMethod, Priority(0)]
        public async Task ExtendAllAssignment() {
            const string code = @"
x_a = 1
x_b = 2
x_c = 3
x_d = 4
x_e = 5
x_f = 6
x_g = 7
__all__ = ['x_a', 'x_b']
__all__ += ['x_c']
__all__ += ['x_d'] + ['x_e']
__all__.extend(['x_f'])
__all__.append('x_g')
x_h = 8
# __all__ += ['x_h']
";
            var analysis = await GetAnalysisAsync(code);
            var all = analysis.Should().HaveVariable("__all__").Which;
            all.Definition.Span.Should().Be(9, 1, 9, 8);
            all.References.Should().HaveCount(5);
            all.References[0].Span.Should().Be(9, 1, 9, 8);
            all.References[1].Span.Should().Be(10, 1, 10, 8);
            all.References[2].Span.Should().Be(11, 1, 11, 8);
            all.References[3].Span.Should().Be(12, 1, 12, 8);
            all.References[4].Span.Should().Be(13, 1, 13, 8);
        }

        [TestMethod, Priority(0)]
        public async Task VariableInCallParameters() {
            const string code = @"
from constants import *
import constants

print(VARIABLE1)
print(constants.VARIABLE1)
x = print(VARIABLE1)
";
            await TestData.CreateTestSpecificFileAsync("constants.py", @"VARIABLE1 = 'afad'");
            var analysis = await GetAnalysisAsync(code);
            var v1 = analysis.Should().HaveVariable("VARIABLE1").Which;

            v1.Definition.Span.Should().Be(1, 1, 1, 10);
            v1.Definition.DocumentUri.AbsolutePath.Should().Contain("constants.py");

            v1.References.Should().HaveCount(4);
            v1.References[0].Span.Should().Be(1, 1, 1, 10);
            v1.References[0].DocumentUri.AbsolutePath.Should().Contain("constants.py");

            v1.References[1].Span.Should().Be(5, 7, 5, 16);
            v1.References[1].DocumentUri.AbsolutePath.Should().Contain("module.py");

            v1.References[2].Span.Should().Be(6, 17, 6, 26);
            v1.References[2].DocumentUri.AbsolutePath.Should().Contain("module.py");

            v1.References[3].Span.Should().Be(7, 11, 7, 20);
            v1.References[3].DocumentUri.AbsolutePath.Should().Contain("module.py");
        }

        [TestMethod, Priority(0)]
        public async Task LibraryFunction() {
            const string code = @"
print(1)
print(2)
";
            var analysis = await GetAnalysisAsync(code);
            var b = analysis.Document.Interpreter.ModuleResolution.BuiltinsModule;
            var print = b.Analysis.Should().HaveVariable("print").Which;

            print.Definition.Span.Should().Be(1, 1, 1, 1);

            print.References.Should().HaveCount(3);
            print.References[0].Span.Should().Be(1, 1, 1, 1);
            print.References[0].DocumentUri.AbsolutePath.Should().Contain("python.pyi");

            print.References[1].Span.Should().Be(2, 1, 2, 6);
            print.References[1].DocumentUri.AbsolutePath.Should().Contain("module.py");

            print.References[2].Span.Should().Be(3, 1, 3, 6);
            print.References[2].DocumentUri.AbsolutePath.Should().Contain("module.py");
        }
    }
}
