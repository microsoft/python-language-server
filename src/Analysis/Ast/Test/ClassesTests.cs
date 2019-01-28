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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ClassesTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Classes() {
            var code = await File.ReadAllTextAsync(Path.Combine(GetAnalysisTestDataFilesPath(), "Classes.py"));
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var names = analysis.GlobalScope.Variables.Names;
            var all = analysis.GlobalScope.Variables.ToArray();

            names.Should().OnlyContain("C1", "C2", "C3", "C4", "C5",
                "D", "E",
                "F1",
                "f"
            );

            all.First(x => x.Name == "C1").Value.Should().BeAssignableTo<IPythonClassType>();
            all.First(x => x.Name == "C2").Value.Should().BeAssignableTo<IPythonClassType>();
            all.First(x => x.Name == "C3").Value.Should().BeAssignableTo<IPythonClassType>();
            all.First(x => x.Name == "C4").Value.Should().BeAssignableTo<IPythonClassType>();

            all.First(x => x.Name == "C5")
                .Value.Should().BeAssignableTo<IPythonClassType>()
                .Which.Name.Should().Be("C1");

            all.First(x => x.Name == "D").Value.Should().BeAssignableTo<IPythonClassType>();
            all.First(x => x.Name == "E").Value.Should().BeAssignableTo<IPythonClassType>();

            all.First(x => x.Name == "f").Value.Should().BeAssignableTo<IPythonFunctionType>();

            var f1 = all.First(x => x.Name == "F1");
            var c = f1.Value.Should().BeAssignableTo<IPythonClassType>().Which;

            c.GetMemberNames().Should().OnlyContain("F2", "F3", "F6", "__class__", "__bases__");
            c.GetMember("F6").Should().BeAssignableTo<IPythonClassType>()
                .Which.Documentation.Should().Be("C1");

            c.GetMember("F2").Should().BeAssignableTo<IPythonClassType>();
            c.GetMember("F3").Should().BeAssignableTo<IPythonClassType>();
            c.GetMember("__class__").Should().BeAssignableTo<IPythonClassType>();
            c.GetMember("__bases__").Should().BeAssignableTo<IPythonCollection>();
        }

        [TestMethod, Priority(0)]
        public async Task Mro() {
            using (var s = await CreateServicesAsync(null)) {
                var interpreter = s.GetService<IPythonInterpreter>();
                var m = new SentinelModule("test", s);

                var O = new PythonClassType("O", m);
                var A = new PythonClassType("A", m);
                var B = new PythonClassType("B", m);
                var C = new PythonClassType("C", m);
                var D = new PythonClassType("D", m);
                var E = new PythonClassType("E", m);
                var F = new PythonClassType("F", m);

                F.SetBases(new[] { O });
                E.SetBases(new[] { O });
                D.SetBases(new[] { O });
                C.SetBases(new[] { D, F });
                B.SetBases(new[] { D, E });
                A.SetBases(new[] { B, C });

                PythonClassType.CalculateMro(A).Should().Equal(new[] { "A", "B", "C", "D", "E", "F", "O" }, (p, n) => p.Name == n);
                PythonClassType.CalculateMro(B).Should().Equal(new[] { "B", "D", "E", "O" }, (p, n) => p.Name == n);
                PythonClassType.CalculateMro(C).Should().Equal(new[] { "C", "D", "F", "O" }, (p, n) => p.Name == n);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ComparisonTypeInference() {
            const string code = @"
class BankAccount(object):
    def __init__(self, initial_balance=0):
        self.balance = initial_balance
    def withdraw(self, amount):
        self.balance -= amount
    def overdrawn(self):
        return self.balance < 0
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            analysis.Should().HaveClass("BankAccount")
                .Which.Should().HaveMethod("overdrawn")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnType("bool");
        }

        [TestMethod, Priority(0)]
        public async Task MembersAfterError() {
            const string code = @"
class X(object):
    def f(self):
        return self.
        
    def g(self):
        pass
        
    def h(self):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            var objectMemberNames = analysis.Document.Interpreter.GetBuiltinType(BuiltinTypeId.Object).GetMemberNames();

            var cls = analysis.Should().HaveClass("X").Which;

            cls.Should().HaveMethod("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0)
                .Which.Should().HaveName("self");

            cls.Should().HaveMembers(objectMemberNames)
                .And.HaveMembers("f", "g", "h");
        }

        [TestMethod, Priority(0)]
        public async Task Property() {
            const string code = @"
class x(object):
    @property
    def SomeProp(self):
        return 42

a = x().SomeProp
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task StaticMethod() {
            const string code = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

a = x().StaticMethod(4.0)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task InheritedStaticMethod() {
            const string code = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

class y(x):
    pass

a = y().StaticMethod(4.0)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task InheritedClassMethod() {
            const string code = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

class y(x):
    pass

a = y().ClassMethod()
b = y.ClassMethod()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType("y")
                .And.HaveVariable("b").OfType("y");
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethod() {
            const string code = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

a = x().ClassMethod()
b = x.ClassMethod()
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("a").OfType("x");
            analysis.Should().HaveVariable("b").OfType("x");
            analysis.Should().HaveClass("x")
                .Which.Should().HaveMethod("ClassMethod")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0).Which.Should().HaveName("cls").And.HaveType("x");
        }

        [TestMethod, Priority(0)]
        public async Task ClassInit() {
            const string code = @"
class X:
    def __init__(self, value):
        self.value = value

a = X(2)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a")
                .Which.Value.Should().BeAssignableTo<IPythonInstance>()
                .Which.Type.Name.Should().Be("X");

            analysis.Should().HaveClass("X")
                .Which.Should().HaveMethod("__init__")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0).Which.Should().HaveName("self").And.HaveType("X");
        }

        [TestMethod, Priority(0)]
        [Ignore]
        public async Task ClassNew() {
            const string code = @"
class X:
    def __new__(cls, value: int):
        res = object.__new__(cls)
        res.value = value
        return res

a = X(2)
";
            var analysis = await GetAnalysisAsync(code);

            var cls = analysis.Should().HaveClass("X").Which;
            var o = cls.Should().HaveMethod("__new__").Which.Should().HaveSingleOverload().Which;

            o.Should().HaveParameterAt(0).Which.Should().HaveName("cls").And.HaveType("X");
            o.Should().HaveParameterAt(1).Which.Should().HaveName("value").And.HaveType(BuiltinTypeId.Int);
            cls.Should().HaveMember<IPythonConstant>("res").Which.Should().HaveType("X");

            var v = analysis.Should().HaveVariable("a").OfType("X").Which;
            v.Should().HaveMember("value").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task InstanceCall() {
            const string code = @"
class X:
    def __call__(self, value):
        return value

x = X()

a = x(2)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task InstanceMembers() {
            const string code = @"
class C:
    def f(self): pass

x = C
y = x()

f1 = C.f
c = C()
f2 = c.f
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").Which.Value.Should().BeAssignableTo<IPythonType>();
            analysis.Should().HaveVariable("y")
                .Which.Value.Should().BeAssignableTo<IPythonInstance>()
                .And.HaveType(typeof(IPythonClassType));

            analysis.Should()
                .HaveVariable("f1").OfType(BuiltinTypeId.Function).And
                .HaveVariable("f2").OfType(BuiltinTypeId.Method);
        }

        [TestMethod, Priority(0)]
        public async Task UnfinishedDot() {
            // the partial dot should be ignored and we shouldn't see g as a member of D
            const string code = @"
class D(object):
    def func(self):
        self.
        
def g(a, b, c): pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveClass("D")
                    .Which.Should().HaveMember<IPythonFunctionType>("func")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameterAt(0)
                    .Which.Name.Should().Be("self");

            analysis.Should().HaveFunction("g").Which.DeclaringType.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task CtorSignatures() {
            const string code = @"
class C: pass

class D(object): pass

class E(object):
    def __init__(self): pass

class F(object):
    def __init__(self, one): pass

class G(object):
    def __new__(cls): pass

class H(object):
    def __new__(cls, one): pass
"; ;

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveClass("C")
                .Which.Should().NotHaveMembers();

            analysis.Should().HaveClass("D")
                .Which.Should().NotHaveMembers();

            analysis.Should().HaveClass("E")
                .Which.Should().HaveMember<IPythonFunctionType>("__init__")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameters("self");

            analysis.Should().HaveClass("F")
                .Which.Should().HaveMember<IPythonFunctionType>("__init__")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameters("self", "one");

            analysis.Should().HaveClass("G")
                .Which.Should().HaveMember<IPythonFunctionType>("__new__")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameters("cls");

            analysis.Should().HaveClass("H")
                .Which.Should().HaveMember<IPythonFunctionType>("__new__")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameters("cls", "one");
        }

        [TestMethod, Priority(0)]
        public async Task NestedMethod() {
            const string code = @"
class MyClass:
    def func1(self):
        def func2(a, b):
            return a
        return func2('abc', 123)

x = MyClass().func1()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task UnassignedClassMembers() {
            const string code = @"
class Employee:
    name: str
    id: int = 3

e = Employee('Guido')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("e")
                .Which.Should().HaveMembers("name", "id", "__class__");
        }

        [TestMethod, Priority(0)]
        public async Task AnnotateToSelf() {
            const string code = @"
class A:
    def func() -> A: ...

x = A().func()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType("A");
        }

        [TestMethod, Priority(0)]
        public async Task MutualRecursion() {
            const string code = @"
class C:
    def f(self, other, depth):
        if depth == 0:
            return 'abc'
        return other.g(self, depth - 1)

class D:
    def g(self, other, depth):
        if depth == 0:
            return ['d', 'e', 'f']
        
        return other.f(self, depth - 1)

x = D().g(C(), 42)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task OverrideMember() {
            const string code = @"
class x(object):
    x: int
    def a(self):
        return self.x

class y(x):
    x: float
    pass

a = y().a()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task InheritedMember() {
            const string code = @"
class x(object):
    x: int

class y(x):
    def a(self):
        return self.x

a = y().a()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int);
        }

    }
}
