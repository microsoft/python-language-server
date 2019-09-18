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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintUndefinedVarsTests : LinterTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicVariables() {
            const string code = @"
y = x

class A:
    x1: int = 0
    y1: int
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 5, 2, 6);
        }

        [TestMethod, Priority(0)]
        public async Task ClassVariables() {
            const string code = @"
class A:
    x1: int = 0
    y1: int
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task Conditionals() {
            const string code = @"
z = 3
if x > 2 and y == 3 or z < 2:
    pass
";
            var d = await LintAsync(code);
            d.Should().HaveCount(2);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(3, 4, 3, 5);
            d[1].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[1].SourceSpan.Should().Be(3, 14, 3, 15);
        }

        [TestMethod, Priority(0)]
        public async Task Calls() {
            const string code = @"
z = 3
func(x, 1, y+1, z)
";
            var d = await LintAsync(code);
            d.Should().HaveCount(3);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(3, 1, 3, 5);
            d[1].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[1].SourceSpan.Should().Be(3, 6, 3, 7);
            d[2].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[2].SourceSpan.Should().Be(3, 12, 3, 13);
        }

        [TestMethod, Priority(0)]
        public async Task TupleAssignment() {
            const string code = @"
a, *b, c = range(5)
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task TupleUsage() {
            const string code = @"
a, b = 1
x = a
y = b
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FunctionNoneArgument() {
            const string code = @"
def func(a=None, b=True):
    x = a
    y = b
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FunctionReference() {
            const string code = @"
def func1():
    func2()
    return

def func2(a=None, b=True): ...
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ClassReference() {
            const string code = @"
class A(B):
    def __init__(self):
        x = B()
        return

class B(): ...
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForVariables() {
            const string code = @"
c = {}
for i in c:
    x = i
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForVariablesCondition() {
            const string code = @"
c = {}
for a, b in c if a < 0:
    x = b
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForVariablesList() {
            const string code = @"
for i, (j, k) in {}:
    x = j
    y = k
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForExpression() {
            const string code = @"
def func1(a):
    return a

def func2(a, b):
    return a + b

func1(func2(a) for a, b in {} if a < 0)
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task ListComprehension() {
            const string code = @"
NAME = ' '.join(str(x) for x in {z, 2, 3})

class C:
    EVENTS = ['x']
    x = [(e, e) for e in EVENTS]
    y = EVENTS
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 34, 2, 35);
        }

        [TestMethod, Priority(0)]
        public async Task SelfAssignment() {
            const string code = @"
def foo(m):
    m = m
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task AssignmentBefore() {
            const string code = @"
x = 1
y = x
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task AssignmentBeforeAndAfter() {
            const string code = @"
x = 1
y = x
x = 's'
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task AssignmentAfter() {
            const string code = @"
y = x
x = 1
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 5, 2, 6);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionArguments() {
            const string code = @"
def z(x):
    return x

def func(a, b, c):
    a = b
    x = c
    z(c * 3)
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task NonLocal() {
            const string code = @"
class A:
    x: int
    def func():
        nonlocal x, y
        y = 2
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.VariableNotDefinedNonLocal);
            d[0].SourceSpan.Should().Be(5, 21, 5, 22);
        }

        [TestMethod, Priority(0)]
        public async Task Global() {
            const string code = @"
x = 1

class A:
    def func():
        global x, y
        y = 2
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.VariableNotDefinedGlobally);
            d[0].SourceSpan.Should().Be(6, 19, 6, 20);
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task OptionsSwitch(bool enabled) {
            const string code = @"x = y";

            var sm = CreateServiceManager();
            var op = new AnalysisOptionsProvider();
            sm.AddService(op);

            op.Options.LintingEnabled = enabled;
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X, sm);

            var a = sm.GetService<IPythonAnalyzer>();
            var d = a.LintModule(analysis.Document);
            d.Should().HaveCount(enabled ? 1 : 0);
        }

        [TestMethod, Priority(0)]
        public async Task Builtins() {
            const string code = @"
print(1)
abs(3)
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task Enumeration() {
            const string code = @"
x = {}
for a, b in enumerate(x):
    if a:
        pass
    if b:
        pass
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ReassignInLoop() {
            const string code = @"
x = {}
for a, b in enumerate(x):
    y = a
    a = b
    b = 1
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ListComprehensionStatement() {
            const string code = @"
[a == 1 for a in {}]
x = a
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryComprehension() {
            const string code = @"
b = {str(a): a == 1 for a in {}}
x = a
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(3, 5, 3, 6);
        }

        [TestMethod, Priority(0)]
        public async Task Lambda() {
            const string code = @"
x = lambda a: a
x(1)
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinModuleVariables() {
            const string code = @"
x1 = __doc__
x2 = __file__
x3 = __name__
x4 = __package__
x5 = __path__
x6 = __dict__
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinFunctionVariables() {
            const string code = @"
def func():
    x1 = __closure__
    x2 = __code__
    x3 = __defaults__
    x4 = __dict__
    x5 = __doc__
    x6 = __func__
    x7 = __name__
    x8 = __globals__
    x9 = __name__
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinClassVariables() {
            const string code = @"
class A:
    a = __self__
    def func():
        x1 = __closure__
        x2 = __code__
        x3 = __defaults__
        x4 = __dict__
        x5 = __doc__
        x6 = __func__
        x7 = __name__
        x8 = __globals__
        x9 = __name__
        x10 = __self__
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task LambdaComprehension() {
            const string code = @"
y = lambda x: [e for e in x if e == 1]
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FromFuture() {
            const string code = @"
from __future__ import print_function
print()
";
            var d = await LintAsync(code, PythonVersions.LatestAvailable2X);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WithOpenOverUnknown() {
            const string code = @"
import xml.etree.ElementTree as ElementTree

class XXX:
    def __init__(self, path):
        self.path = path
        with (self.path / 'object.xml').open() as f:
            self.root = ElementTree.parse(f)
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task Various() {
            const string code = @"
print x
assert x
del x
";
            var d = await LintAsync(code, PythonVersions.LatestAvailable2X);
            d.Should().HaveCount(3);
        }

        [TestMethod, Priority(0)]
        public async Task FString() {
            const string code = @"
print(f'Hello, {name}. You are {age}.')
";
            var d = await LintAsync(code, PythonVersions.LatestAvailable3X);
            d.Should().HaveCount(2);
        }

        [TestMethod, Priority(0)]
        public async Task ClassMemberAssignment() {
            const string code = @"
class A:
    x: int

a = A()
a.x = 1
a.y = 2
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ListAssignment() {
            const string code = @"
from datetime import date
[year, month] = date.split(' - ')";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task NoRightSideCheck() {
            const string code = @"
x = 
y = ";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ListComprehensionFunction() {
            const string code = @"
def func_a(value):
    return value

def func_b():
    list_a = [1, 2, 3, 4]

    return [func_a(value=elem) for elem in list_a]
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task NoLeakFromComprehension() {
            const string code = @"
len([1 for e in [1, 2]]) + len([e])
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 33, 2, 34);
        }

        [TestMethod, Priority(0)]
        public async Task NestedDictComprehension() {
            const string code = @"
dmap = {}
sizes = {}
x = {srv:sum([sizes[d] for d in dbs]) for srv, dbs in dmap.items()}
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FunctionInIf() {
            const string code = @"
def foo(a, b, c):
    return a + b + c

if True:
    def bar(x, y, z):
        x += y
        return x + y + z
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task DifferentScopes() {
            const string code = @"
def func():
    var = _CONSTANT

_CONSTANT = 1
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task GlobalScope() {
            const string code = @"
var = _CONSTANT
_CONSTANT = 1
";
            var d = await LintAsync(code);
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 7, 2, 16);
        }

        [TestMethod, Priority(0)]
        public async Task ClassMemberDefinition() {
            const string code = @"
class A:
    i: int
    i = 0
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForElse() {
            const string code = @"
def py_repro():
    for n in range(10):
        print(n)
    else:
        error_code = 10
        raise NotImplementedError(f'some error: { error_code}')
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task SpecNotUndefined() {
            const string code = "__spec__";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WithAsTuple() {
            const string code = @"
class Test:
    def __enter__(self) -> Tuple[int, int]:
        return (1, 2)
    
    def __exit__(x, y, z, w):
        pass

with Test() as (hi, hello):
    pass
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WithAsTupleEnterReturnTypeMismatch() {
            const string code = @"
from typing import Tuple

class Test:
    def __enter__(self) -> str:
        return (1, 2)
    
    def __exit__(x, y, z, w):
        pass

with Test() as (test, test1):
    pass
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WithAsTupleEnterNoReturnType() {
            const string code = @"
from typing import Tuple

class Test:
    def __enter__(self):
        return (1, 2)
    
    def __exit__(x, y, z, w):
        pass

with Test() as (test, test1):
    pass
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WithList() {
            const string code = @"
from typing import List

class Test:
    def __enter__(self) -> List[int]:
        return [1, 2]
    
    def __exit__(x, y, z, w):
        pass


with Test() as [hi, hello]:
    pass
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WithSingleElementTuple() {
            const string code = @"
from typing import List

class Test:
    def __enter__(self) -> int:
        return [1, 2]
    
    def __exit__(x, y, z, w):
        pass


with Test() as (a):
    pass
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WithSingleElementList() {
            const string code = @"
from typing import List

class Test:
    def __enter__(self) -> int:
        return [1, 2]
    
    def __exit__(x, y, z, w):
        pass


with Test() as [a]:
    pass
";
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }
    }
}
