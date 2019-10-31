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

            mod.GetMemberNames().Should().Contain("f", "f2", "g", "h", "C");
            mod.GetMember("f").Should().BeAssignableTo<IPythonFunctionType>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember("f2").Should().BeAssignableTo<IPythonFunctionType>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember("g").Should().BeAssignableTo<IPythonFunctionType>();
            mod.GetMember("h").Should().BeAssignableTo<IPythonFunctionType>();

            var o = analysis.Document.Interpreter.GetBuiltinType(BuiltinTypeId.Object);
            var expected = new[] { "i", "j", "C2", "__class__", "__base__", "__bases__" };
            expected = expected.Concat(o.GetMemberNames()).Distinct().ToArray();

            var c = mod.GetMember("C").Should().BeAssignableTo<IPythonClassType>().Which;
            c.GetMemberNames().Should().OnlyContain(expected);
            c.GetMember("i").Should().BeAssignableTo<IPythonFunctionType>();
            c.GetMember("j").Should().BeAssignableTo<IPythonFunctionType>();
            c.GetMember("__class__").Should().BeAssignableTo<IPythonClassType>();
            c.GetMember("__bases__").Should().BeAssignableTo<IPythonCollection>();

            expected = new[] { "k", "__class__", "__base__", "__bases__" };
            expected = expected.Concat(o.GetMemberNames()).Distinct().ToArray();

            var c2 = c.GetMember("C2").Should().BeAssignableTo<IPythonClassType>().Which;
            c2.GetMemberNames().Should().OnlyContain(expected);
            c2.GetMember("k").Should().BeAssignableTo<IPythonFunctionType>();
            c2.GetMember("__class__").Should().BeAssignableTo<IPythonClassType>();
            c2.GetMember("__bases__").Should().BeAssignableTo<IPythonCollection>();
        }

        [TestMethod, Priority(0)]
        public async Task NamedTupleReturnAnnotation() {
            const string code = @"
from typing import NamedTuple

Point = NamedTuple('Point', ['x', 'y'])

def f(a, b):
    return Point(a, b)

pt = f(1, 2)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var pt = analysis.Should().HaveVariable("pt").Which;
            pt.Should().HaveType("Point").And.HaveMember("x");
            pt.Should().HaveType("Point").And.HaveMember("y");
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
            analysis.Should().HaveVariable("s").OfType(BuiltinTypeId.None);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("s").And.HaveType(BuiltinTypeId.None);
        }

        [TestMethod, Priority(0)]
        public async Task ParameterAnnotationLambda() {
            const string code = @"
s = None
def f(s: lambda s: s > 0 = 123):
    return s
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("s").OfType(BuiltinTypeId.None)
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
            analysis.Should()
                .HaveVariable("x").OfType(BuiltinTypeId.Str).And
                .HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnValueDefaultNone() {
            const string code = @"
def g(x=None):
    return x

y = g('4')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should()
                .HaveVariable("y").OfType(BuiltinTypeId.Str);
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
        public async Task ReturnValueAnnotatedQuoted() {
            const string code = @"
def f(a, b) -> 'int':
    return a + b

x = f('x', 'y')
y = f(1, 2)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnType(BuiltinTypeId.Int);

            analysis.Should()
                .HaveVariable("x").OfType(BuiltinTypeId.Int).And
                .HaveVariable("y").OfType(BuiltinTypeId.Int);
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

x = dir()
v = x[0]

va = vars()
kv = va.keys()[0]
vv = va['a']
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
                .And.HaveVariable("i").OfType("C")
                .And.HaveVariable("x").OfType("List[str]")
                .And.HaveVariable("v").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("va").OfType("Dict[str, object]")
                .And.HaveVariable("kv").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("vv").OfType(BuiltinTypeId.Object);
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
def f(a: bool) -> None: ...
def f(a: int) -> float: ...
def f(a: str) -> bytes: ...

a = True
x = f(a)
y = f(1)
z = f('s')
";
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction("f").Which;

            f.Should().HaveOverloadAt(0)
                .Which.Should().HaveReturnType(BuiltinTypeId.None)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Bool);

            f.Should().HaveOverloadAt(1)
                .Which.Should().HaveReturnType(BuiltinTypeId.Float)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Int);

            f.Should().HaveOverloadAt(2)
                .Which.Should().HaveReturnType(BuiltinTypeId.Bytes)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Str);

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.None)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Bytes);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnFunc() {
            const string code = @"
def g():
    return []

def f():
    return g
    
x = f()()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleReturnTypes() {
            const string code = @"
def f():
    if True:
        return 1
    if False:
        return 'a'

x = f()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnType("Union[int, str]");
            analysis.Should().HaveVariable("x").OfType("Union[int, str]");
        }

        [TestMethod, Priority(0)]
        public async Task ParameterDefaults() {
            const string code = @"
def f(x = None): pass
def g(x = {}): pass
def h(x = {2:3}): pass
def i(x = []): pass
def j(x = [None]): pass
def k(x = ()): pass
def l(x = (2, )): pass
def m(x = math.atan2(1,0)): pass
";
            var analysis = await GetAnalysisAsync(code);
            var tests = new[] {
                new { FuncName = "f", DefaultValue = "None" },
                new { FuncName = "g", DefaultValue = "{}" },
                new { FuncName = "h", DefaultValue = "{2:3}" },
                new { FuncName = "i", DefaultValue = "[]" },
                new { FuncName = "j", DefaultValue="[None]" },
                new { FuncName = "k", DefaultValue = "()" },
                new { FuncName = "l", DefaultValue = "(2)" },
                new { FuncName = "m", DefaultValue = "math.atan2(1,0)" },
            };

            foreach (var test in tests) {
                analysis.Should().HaveFunction(test.FuncName)
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveSingleParameter()
                    .Which.Should().HaveName("x").And.HaveDefaultValue(test.DefaultValue);
            }
        }

        [TestMethod, Priority(0)]
        [Ignore]
        public async Task SpecializedOverride() {
            const string code = @"
class simpledict(dict): pass

class getdict(dict):
    def __getitem__(self, index):
        return 'abc'


d1 = simpledict({2:3})
x1 = d1[2]

d2 = simpledict(x = 2)
x2 = d2['x']

d3 = simpledict(**{2:3})
x3 = d3[2]

d4 = getdict({2:3})
x4 = d4[2]

d5 = simpledict(**{2:'blah'})
x5 = d5[2]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x3").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x4").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x5").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnArg() {
            const string code = @"
def g(a):
    return a

x = g(1)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnArgNestedFunction() {
            const string code = @"

def f(a):
    def g():
        return a
    return g

x = f(2)()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }

        // Verifies that constructing lists / tuples from more lists/tuples doesn't cause an infinite analysis as we keep creating more lists/tuples.
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
        public async Task ReturnExpressionOnArg() {
            const string code = @"
class C:
    x = 123
class D:
    x = 3.14

def f(v):
    return v.x

c = f(C())
d = f(D())";

            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task NestedFunction() {
            const string code = @"
def outer():
    def inner():
        x = 1
        return x
    return inner

y = outer()
z = y()
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Function)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task NestedMembers() {
            const string code = @"
def outer():
    class innerClass(): ...
    def innerFunc(): ...
";
            var analysis = await GetAnalysisAsync(code);
            var outer = analysis.Should().HaveFunction("outer").Which as IPythonType;
            outer.Should().HaveMember<IPythonClassType>("innerClass");
            outer.Should().HaveMember<IPythonFunctionType>("innerFunc");
        }

        [TestMethod, Priority(0)]
        public async Task NestedPropertyMembers() {
            const string code = @"
def outer():
    @property
    def p(self):
        class innerClass(): ...
        def innerFunc(): ...
";
            var analysis = await GetAnalysisAsync(code);
            var outer = analysis.Should().HaveFunction("outer").Which as IPythonType;
            var p = outer.Should().HaveMember<IPythonPropertyType>("p").Which as IPythonType;
            p.Should().HaveMember<IPythonClassType>("innerClass");
            p.Should().HaveMember<IPythonFunctionType>("innerFunc");
        }

        [TestMethod, Priority(0)]
        public async Task Deprecated() {
            const string code = @"
@deprecation.deprecated('')
class A:
    def a(self): pass

@deprecation.deprecated('')
def func(): ...

class B:
    @deprecation.deprecated('')
    def b(self): pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("A");
            analysis.Should().HaveVariable("func");
            analysis.Should().HaveVariable("B").Which.Should().HaveMember("b");
        }

        [TestMethod, Priority(0)]
        public async Task AnnotatedParameterPriority() {
            const string code = @"
class Foo:
    def __init__(self, name: str):
        self.name = name

def func() -> int:
    return Foo

def test(foo: Foo = func()):
    return foo

x = test()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType("Foo");
        }

        [TestMethod, Priority(0)]
        public async Task AmbiguousOptionalParameterType() {
            const string code = @"
from typing import Optional
class A: ...

class B:
    def __init__(self, A: Optional[A]):
        self.name = name

    @property
    def A(self) -> int:
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var a = analysis.Should().HaveClass("A").Which;
            analysis.Should().HaveClass("B")
                .Which.Should().HaveMethod("__init__")
                .Which.Should().HaveParameterAt(1)
                .Which.Should().HaveType("A")
                .Which.Should().BeOfType(a.GetType());
        }

        [TestMethod, Priority(0)]
        public async Task PositionalOnlyParameters() {
            const string code = @"
def f(a, b, /, c, d):
    pass
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.Required_Python38X);
            analysis.Should().HaveFunction("f")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters("a", "b", "c", "d");
        }

        [TestMethod, Priority(0)]
        public async Task LiteralParameter() {
            const string code = @"
from typing import Literal

def f(handle: int, overlapped: Literal[True]):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters("handle", "overlapped");
        }
    }
}
