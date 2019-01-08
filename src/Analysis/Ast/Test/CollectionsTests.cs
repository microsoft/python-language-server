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
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class CollectionsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ListAssign() {
            const string code = @"
l1 = [1, 'str', 3.0]
x0 = l1[0]
x1 = l1[1]
x2 = l1[2]
x3 = l1[3]
x4 = l1[x0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("x0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x2").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("x3").WithNoTypes()
                .And.HaveVariable("x4").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListCallCtor() {
            const string code = @"
import builtins as _mod_builtins
l = list()
l0 = _mod_builtins.list()
l1 = list([1, 'str', 3.0])
x0 = l1[0]
x1 = l1[1]
x2 = l1[2]
x3 = l1[3]
x4 = l1[x0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l").OfType(BuiltinTypeId.List)
                .And.HaveVariable("l0").OfType(BuiltinTypeId.List)
                .And.HaveVariable("l1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("x0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x2").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("x3").WithNoTypes()
                .And.HaveVariable("x4").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListNegativeIndex() {
            const string code = @"
l1 = [1, 'str', 3.0]
x0 = l1[-1]
x1 = l1[-2]
x2 = l1[-3]
x3 = l1[x2]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("x0").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x3").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListSlice() {
            const string code = @"
l1 = [1, 'str', 3.0, 2, 3, 4]
l2 = l1[2:4]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l1").OfType(BuiltinTypeId.List)
                .And.HaveVariable("l2").OfType(BuiltinTypeId.List);
        }

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
        public async Task ListSubclass() {
            const string code = @"
class C(list):
    pass

a = C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").Which.Should().HaveMember("count");
        }

        [TestMethod, Priority(0)]
        public async Task ConstantIndex() {
            const string code = @"
ZERO = 0
ONE = 1
TWO = 2
x = ['abc', 42, True]


some_str = x[ZERO]
some_int = x[ONE]
some_bool = x[TWO]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("some_str").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("some_int").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("some_bool").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task Slicing() {
            var code = @"
x = [2]
y = x[:-1]
z = y[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);

            code = @"
x = (2, 3, 4)
y = x[:-1]
z = y[0]
";
            analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);

            code = @"
lit = 'abc'
inst = str.lower()

slit = lit[1:2]
ilit = lit[1]
sinst = inst[1:2]
iinst = inst[1]
";
            analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"slit").OfType(BuiltinTypeId.Str)
                .And.HaveVariable(@"ilit").OfType(BuiltinTypeId.Str)
                .And.HaveVariable(@"sinst").OfType(BuiltinTypeId.Str)
                .And.HaveVariable(@"iinst").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task DictCtor() {
            const string code = @"
d1 = dict({2:3})
x1 = d1[2]

d2 = dict(x = 2)
x2 = d2['x']

d3 = dict(**{2:3})
x3 = d3[2]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x1").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x2").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x3").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task DictEnum() {
            const string code = @"
for x in {42:'abc'}:
    print(x)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task DictMethods_V3() {
            const string code = @"
x = {42:'abc'}
a = x.items()[0][0]
b = x.items()[0][1]
c = x.keys()[0]
d = x.values()[0]
e = x.pop(1)
f = x.popitem()[0]
g = x.popitem()[1]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x")
                .And.HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("e").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("g").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task DictMethods_V2() {
            const string code = @"
x = {42:'abc'}
h = x.iterkeys().next()
i = x.itervalues().next()
n = x.iteritems().next();
j = n[0]
k = n[1]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x")
                .And.HaveVariable("h").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("i").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("j").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("k").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task DictAssign() {
            const string code = @"
x = {'abc': 42}
y = x['abc']
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task DictIterator() {
            const string code = @"
x = {'a': 1, 'b': 2, 'c': 3}
y = x.keys()
k = y[1]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.List)
                .And.HaveVariable("k").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryKeyValues() {
            const string code = @"
x = {'abc': 42, 'oar': 'baz'}

i = x['abc']
s = x['oar']
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("s").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ForIterator() {
            const string code = @"
class X(object):
    def __iter__(self): return self
    def __next__(self): return 123

class Y(object):
    def __iter__(self): return X()

for i in Y():
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ForSequence() {
            const string code = @"
x = [('abc', 42, True), ('abc', 23, False),]
for some_str, some_int, some_bool in x:
    print some_str
    print some_int
    print some_bool
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("some_str").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("some_int").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("some_bool").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task Generator2X() {
            var code = @"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.next()

for c in f():
    print c
d = a.__next__()
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").WithNoTypes();

            code = @"
def f(x):
    yield x

a1 = f(42)
b1 = a1.next()
a2 = f('abc')
b2 = a2.next()

for c in f():
    print c
d = a1.__next__()
";

            analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a1").OfType(BuiltinTypeId.Generator)
                .And.HaveVariable("b1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("a2").OfType(BuiltinTypeId.Generator)
                .And.HaveVariable("b2").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c").WithNoTypes()
                .And.HaveVariable("d").WithNoTypes();

            code = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.next()
c = a.send('abc')
d = a.__next__()
";
            analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").WithNoTypes();
        }

        [TestMethod, Priority(0)]
        public async Task Generator3X() {
            var code = @"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.__next__()

for c in f():
    print(c)

d = a.next()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").WithNoTypes();

            code = @"
def f(x):
    yield x

a1 = f(42)
b1 = a1.__next__()
a2 = f('abc')
b2 = a2.__next__()

for c in f(42):
    print(c)
d = a1.next()
";

            analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a1").OfType(BuiltinTypeId.Generator)
                .And.HaveVariable("b1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("a2").OfType(BuiltinTypeId.Generator)
                .And.HaveVariable("b2").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").WithNoTypes();

            code = @"
def f():
    yield 1
    x = yield 2

a = f()
b = a.__next__()
c = a.send('abc')
d = a.next()
";
            analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("d").WithNoTypes()
                .And.HaveFunction("f");
        }
    }
}
