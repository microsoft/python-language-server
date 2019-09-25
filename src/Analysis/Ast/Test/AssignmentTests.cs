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
    public class AssignmentTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task AssignSelf() {
            const string code = @"
class x(object):
    def __init__(self):
        self.x = 'abc'
    def f(self):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            var cls = analysis.Should().HaveClass("x").Which;

            var xType = cls.Should().HaveMethod("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0)
                .Which.Should().HaveName("self").And.HaveType("x").Which;

            xType.Should().HaveMember<IPythonInstance>("x")
                .Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task AssignToMissingMember() {
            const string code = @"
class test():
    x = 0;
    y = 1;
t = test()
t.x, t. =
";
            // This just shouldn't crash, we should handle the malformed code
            await GetAnalysisAsync(code);
        }

        [TestMethod, Priority(0)]
        public async Task Backquote() {
            var analysis = await GetAnalysisAsync(@"x = `42`", PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task BadKeywordArguments() {
            const string code = @"
def f(a, b):
    return a

x = 100
z = f(a=42, x)";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task AssignBytes() {
            const string code = @"
x = b'b'
y = u'u'
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task AssignUnicode() {
            const string code = @"
x = b'b'
y = u'u'
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task Ellipsis() {
            var analysis = await GetAnalysisAsync(@"x = ...");
            analysis.Should().HaveVariable("x").WithNoTypes();
        }
        [TestMethod, Priority(0)]
        public async Task NegativeNumbersV2() {
            const string code = @"
x = -1
y = -3.0
z = -4L
a = z
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Long)
                .And.HaveVariable("a").OfType(BuiltinTypeId.Long);
        }

        [TestMethod, Priority(0)]
        public async Task NegativeNumbersV3() {
            const string code = @"
x = -1
y = -3.0
z = -4L
a = z
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("a").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task Tuple() {
            const string code = @"
x, y, z = 1, 'str', 3.0
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task TupleUnknownReturn() {
            const string code = @"
x, y, z = func()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").And.HaveVariable("y").And.HaveVariable("z");
        }

        [TestMethod, Priority(0)]
        public async Task NestedTuple() {
            const string code = @"
((x, r), y, z) = (1, 1), '', False,
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("r").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("z").Which.Should().HaveType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task NestedTupleSingleValue() {
            const string code = @"
(x, (y, (z))) = 1,
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").And.HaveVariable("y").And.HaveVariable("z");
        }

        [TestMethod, Priority(0)]
        public async Task List() {
            const string code = @"
[year, month] = (1, 2)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("year").And.HaveVariable("month");
        }

        [TestMethod, Priority(0)]
        public async Task AnnotatedAssign() {
            const string code = @"
x : int = 42

class C:
    y : int = 42

    def __init__(self):
        self.abc : int = 42

a = C()
fob1 = a.abc
fob2 = a.y
fob3 = x
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("fob1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("fob2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("fob3").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("a")
                .Which.Should().HaveMembers("abc", "y", "__class__");
        }

        [TestMethod, Priority(0)]
        public async Task BaseInstanceVariable() {
            const string code = @"
class C:
    def __init__(self):
        self.abc = 42

class D(C):
    def __init__(self):
        self.fob = self.abc
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Should().HaveClass("D").Which;

            d.Should().HaveMethod("__init__")
            .Which.Should().HaveSingleOverload()
            .Which.Should().HaveParameterAt(0).Which.Name.Should().Be("self");

            d.Should().HaveMember<IPythonConstant>("fob")
                .Which.Should().HaveType(BuiltinTypeId.Int);
            d.Should().HaveMember<IPythonConstant>("abc")
                .Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task LambdaExpression1() {
            const string code = @"
x = lambda a: a
y = x(42)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task LambdaExpression2() {
            const string code = @"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign1() {
            const string code = @"
class C:
    def func(self):
        self.abc = 42

a = C()
a.func()
fob = a.abc
";
            var analysis = await GetAnalysisAsync(code);
            var intMemberNames = analysis.Document.Interpreter.GetBuiltinType(BuiltinTypeId.Int).GetMemberNames();

            analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.Int)
                .Which.Should().HaveMembers(intMemberNames);
            analysis.Should().HaveVariable("a")
                .Which.Should().HaveMembers("abc", "func", "__class__");
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign2() {
            const string code = @"
class D:
    def func2(self):
        a = C()
        a.func()
        return a.abc

class C:
    def func(self):
        self.abc = [2,3,4]

fob = D().func2()
";
            var analysis = await GetAnalysisAsync(code);
            var listMemberNames = analysis.Document.Interpreter.GetBuiltinType(BuiltinTypeId.List).GetMemberNames();

            analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.List)
                .Which.Should().HaveMembers(listMemberNames);
        }

        [TestMethod, Priority(0)]
        public async Task AssignBeforeClassDef() {
            const string code = @"
D = 5
class D: ...
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveClass("D");
        }

        [TestMethod, Priority(0)]
        public async Task AssignAfterClassDef() {
            const string code = @"
class D: ...
D = 5
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("D").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task AssignBeforeFunctionDef() {
            const string code = @"
D = 5
def D():
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("D");
        }

        [TestMethod, Priority(0)]
        public async Task AssignAfterFunctionDef() {
            const string code = @"
def D():
    pass
D = 5
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("D").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task StrIndex() {
            const string code = @"
a = 'abc'
x = a[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task IncompleteTuple() {
            const string code = @"
a, b = 1
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackListToTuple() {
            const string code = @"
(a, b) = [1, 2]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackSingleElementTuple() {
            const string code = @"
(foo) = 1234
((x, y)) = 1, '2'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("foo").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingTypingTuple() {
            const string code = @"
from typing import Tuple

def foo() -> Tuple[int, int]:
    return (1, -1)

result = foo()
(a, b) = result
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingTypingTupleFromList() {
            const string code = @"
from typing import Tuple, List

def foo() -> List[Tuple[int, int]]:
    return [(1, -1)]

def bar() -> List[Tuple[int, str, float]]:
    return [(1, 'str', float(2)), (1,'test',float(5))]

(a, b) = foo()[0]
(c,d,e) = bar()[1]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("e").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingNestedTupleInList() {
            const string code = @"
def foo():
    return [1,2], 3

[var1, var2], var3 = foo();
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("var1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("var2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("var3").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingTypingListFromTuple() {
            const string code = @"
from typing import Tuple, List

def foo() -> Tuple[List[int]]:
    return ([1,2,3])

def bar() -> Tuple[Tuple[int, str, float], List[str]]:
    return ((1, 'str', float(2)), ['hello', 'world'])

a = foo()
a1 = a[0]
[b, c, d] = a1

e = bar()
f = bar()[0]
g = bar()[1]

(h, j, k) = f
[l, m] = g
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("a1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("e").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("g").OfType(BuiltinTypeId.List);

            analysis.Should().HaveVariable("h").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("j").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("k").OfType(BuiltinTypeId.Float);

            analysis.Should().HaveVariable("l").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("m").OfType(BuiltinTypeId.Str);
        }


        [TestMethod, Priority(0)]
        public async Task UnpackingTypingList() {
            const string code = @"
from typing import List

def foo() -> List[int]:
    return [1, -1]

result = foo()
[a, b] = result
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingNestedTypingListInTuple() {
            const string code = @"
from typing import List, Tuple
def foo() -> List[Tuple[str, int]]:
    return [('hi', 1)]

[var1, var2] = foo()
[(a, b), (c, d)] = foo()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("var1").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("var2").OfType(BuiltinTypeId.Tuple);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Str)
                 .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                 .And.HaveVariable("c").OfType(BuiltinTypeId.Str)
                 .And.HaveVariable("d").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingNestedTypingTupleInList() {
            const string code = @"
from typing import List, Tuple
def foo() -> List[List[int], int]:
    return [1,2], 3

[var1, var2], var3 = foo()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("var1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("var2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("var3").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingComplexTypingNestedExpressions() {
            const string code = @"
from typing import List, Tuple
def foo() -> Tuple[List[Tuple[List[str], int]], int]:
    return [(['hi'], 1), (['test'], 5)], 2

[var1, var2], var3 = foo()
[(a, b), (c, d)], f = foo()
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("var1").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("var2").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("var3").OfType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.List)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.List)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingComplexNestedExpressions() {
            const string code = @"
def foo():
    return [(['hi'], 1), (['test'], 5)], 2

[var1, var2], var3 = foo()
[(a, b), (c, d)], f = foo()
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("var1").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("var2").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("var3").OfType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.List)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.List)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingMultipleAssignment() {
            const string code = @"
a = b, c = [0, 1]
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.List)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingNestedTuple() {
            const string code = @"
b, c = (1, (1,2))
d, e, f, g = (1, (1,2), 2, ('test', 'test'))
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Tuple);

            analysis.Should().HaveVariable("d").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("e").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("g").OfType(BuiltinTypeId.Tuple);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingNestedList() {
            const string code = @"
b, c = [1, [1,2]]
d, e, f, g = [1, (1,2), 2, ['test', 'test']]
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.List);

            analysis.Should().HaveVariable("d").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("e").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("g").OfType(BuiltinTypeId.List);
        }


        [TestMethod, Priority(0)]
        public async Task UnpackingTypingNestedTuple() {
            const string code = @"
from typing import Tuple
h: Tuple[int, Tuple[str, int]]
b, c = h
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Tuple);
        }

        [TestMethod, Priority(0)]
        public async Task UnpackingTypingNestedList() {
            const string code = @"
from typing import Tuple, List
t: Tuple[int, Tuple[str, int], List[int]]
b, c, d = t
e, (f, g), h = t
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("d").OfType(BuiltinTypeId.List);

            analysis.Should().HaveVariable("e").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("g").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("h").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task Uts46dataModule() {
            const string code = @"from idna.uts46data import *";
            await GetAnalysisAsync(code, runIsolated: true);
        }

        [TestMethod, Priority(0)]
        public async Task NamedExpressionIf() {
            const string code = @"
if (x := 1234) == 1234:
    pass
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.Required_Python38X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        [Ignore("Needs comprehension scoping")]
        public async Task NamedExpressionFromComprehension() {
            const string code = @"
from typing import List
a: List[int]
b = [(x := i) for i in a]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.Required_Python38X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }
    }
}
