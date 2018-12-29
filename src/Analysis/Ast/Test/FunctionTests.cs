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

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class FunctionTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Functions() {
            var code = await File.ReadAllTextAsync(Path.Combine(GetAnalysisTestDataFilesPath(), "Functions.py"));
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var mod = analysis.Document;

            mod.GetMemberNames().Should().OnlyContain("f", "f2", "g", "h", "C");
            mod.GetMember("f").Should().BeAssignableTo<IPythonFunctionType>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember("f2").Should().BeAssignableTo<IPythonFunctionType>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember("g").Should().BeAssignableTo<IPythonFunctionType>();
            mod.GetMember("h").Should().BeAssignableTo<IPythonFunctionType>();

            var c = mod.GetMember("C").Should().BeAssignableTo<IPythonClassType>().Which;
            c.GetMemberNames().Should().OnlyContain("i", "j", "C2", "__class__", "__bases__");
            c.GetMember("i").Should().BeAssignableTo<IPythonFunctionType>();
            c.GetMember("j").Should().BeAssignableTo<IPythonFunctionType>();
            c.GetMember("__class__").Should().BeAssignableTo<IPythonClassType>();
            c.GetMember("__bases__").Should().BeAssignableTo<IPythonSequence>();

            var c2 = c.GetMember("C2").Should().BeAssignableTo<IPythonClassType>().Which;
            c2.GetMemberNames().Should().OnlyContain("k", "__class__", "__bases__");
            c2.GetMember("k").Should().BeAssignableTo<IPythonFunctionType>();
            c2.GetMember("__class__").Should().BeAssignableTo<IPythonClassType>();
            c2.GetMember("__bases__").Should().BeAssignableTo<IPythonSequence>();
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/microsoft/python-language-server/issues/406")]
        public async Task NamedTupleReturnAnnotation() {
            const string code = @"
from ReturnAnnotation import *
nt = namedtuple('Point', ['x', 'y'])
pt = nt(1, 2)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("pt").OfType(BuiltinTypeId.Tuple);
        }

        [TestMethod, Priority(0)]
        public async Task TypeAnnotationConversion() {
            const string code = @"from ReturnAnnotations import *
x = f()
y = g()";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Function)
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("p").And.HaveType("int").And.HaveNoDefaultValue();
        }

        [TestMethod, Priority(0)]
        public async Task ParameterAnnotation() {
            const string code = @"
s = None
def f(s: s = 123):
    return s
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("s").OfType(BuiltinTypeId.NoneType);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("s").And.HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ParameterAnnotationLambda() {
            const string code = @"
s = None
def f(s: lambda s: s > 0 = 123):
    return s
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("s").OfType(BuiltinTypeId.NoneType)
                .And.HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("s").And.HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnValueEval() {
            const string code = @"
def f(a, b):
    return a + b

x = f('x', 'y')
y = f(1, 2)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnType(BuiltinTypeId.Unknown);

            analysis.Should()
                .HaveVariable("x").OfType(BuiltinTypeId.Str).And
                .HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnValueAnnotated() {
            const string code = @"
def f(a, b) -> str:
    return a + b

x = f('x', 'y')
y = f(1, 2)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnType(BuiltinTypeId.Str);

            analysis.Should()
                .HaveVariable("x").OfType(BuiltinTypeId.Str).And
                .HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task BadMethod() {
            const string code = @"
class cls(object):
    def f():
        'help'
        return 42

abc = cls()
fob = abc.f()
";

            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.Int);
            analysis.Should().HaveClass("cls")
                .Which.Should().HaveMethod("f")
                .Which.Documentation.Should().Be("help");
        }

        [TestMethod, Priority(0)]
        public async Task Specializations() {
            const string code = @"
class C:
    pass

a = ord('a')
b = abs(5)
c = abs(5.0)
d = eval('')
e = isinstance(d)
f = pow(1)
g = pow(3.0)
h = type(C())
i = h()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Object)
                .And.HaveVariable("e").OfType(BuiltinTypeId.Bool)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("g").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("h").OfType(BuiltinTypeId.Type)
                .And.HaveVariable("i").OfType("C");
            ;
        }

        [TestMethod, Priority(0)]
        public async Task Defaults() {
            const string code = @"
def f(x = 42):
    return x

a = f()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task OverrideFunction() {
            const string code = @"
class oar(object):
    def Call(self, xvar, yvar):
        return xvar

class baz(oar):
    def Call(self, xvar, yvar):
        return 42

class Cxxxx(object):
    def __init__(self):
        self.b = baz()
        self.o = oar()

    def CmethB(self, avar, bvar):
        return self.b.Call(avar, bvar)

    def CmethO(self, avar, bvar):
        return self.o.Call(avar, bvar)

abc = Cxxxx()
a = abc.CmethB(['fob'], 'oar')
b = abc.CmethO(['fob'], 'oar')
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task Decorators() {
            const string code = @"
class cls(object):
    @property
    def a(self): pass
    
    @staticmethod
    def b(): pass

    @abstractproperty
    def c(self): pass
    
    @classmethod
    def d(cls): pass
    
    @abstractclassmethod
    def e(cls): pass
";

            var analysis = await GetAnalysisAsync(code);
            var cls = analysis.Should().HaveClass("cls").Which;

            var a = cls.Should().HaveProperty("a").Which;
            a.IsAbstract.Should().BeFalse();
            a.IsReadOnly.Should().BeTrue();

            var b = cls.Should().HaveMethod("b").Which;
            b.IsAbstract.Should().BeFalse();
            b.IsStatic.Should().BeTrue();

            var c = cls.Should().HaveProperty("c").Which;
            c.IsAbstract.Should().BeTrue();
            c.IsReadOnly.Should().BeTrue();

            var d = cls.Should().HaveMethod("d").Which;
            d.IsAbstract.Should().BeFalse();
            d.IsStatic.Should().BeFalse();
            d.IsClassMethod.Should().BeTrue();

            var e = cls.Should().HaveMethod("e").Which;
            e.IsAbstract.Should().BeTrue();
            e.IsStatic.Should().BeFalse();
            e.IsClassMethod.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public async Task OverloadsParamTypeMatch() {
            const string code = @"

def f(a: None) -> None: ...
def f(a: int) -> float: ...
def f(a: str) -> bytes: ...

x = f()
y = f(1)
z = f('s')
";
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction("f").Which;

            f.Should().HaveOverloadAt(0)
                .Which.Should().HaveReturnType(BuiltinTypeId.NoneType)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.NoneType);

            f.Should().HaveOverloadAt(1)
                .Which.Should().HaveReturnType(BuiltinTypeId.Float)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Int);

            f.Should().HaveOverloadAt(2)
                .Which.Should().HaveReturnType(BuiltinTypeId.Bytes)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Str);

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.NoneType)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Bytes);
        }
    }
}
