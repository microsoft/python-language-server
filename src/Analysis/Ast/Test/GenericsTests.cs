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

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class GenericsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ListContent() {
            const string code = @"
from typing import List

lstr: List[str]
x = lstr[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"lstr").OfType("List[str]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListOfLists() {
            const string code = @"
from typing import List

lst: List[List[int]]
x = lst[0]
y = x[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"lst").OfType("List[List[int]]")
                .And.HaveVariable("x").OfType("List[int]")
                .And.HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericListArg() {
            const string code = @"
from typing import List

def func(a: List[str]):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("func").Which
                .Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0)
                .Which.Should().HaveName("a").And.HaveType("List[str]");
        }

        [TestMethod, Priority(0)]
        public async Task FunctionAnnotatedToList() {
            const string code = @"
from typing import List

def f() -> List[str]: ...
x = f()[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnDocumentation("List[str]");

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionWithListArgument() {
            const string code = @"
from typing import List

def f(a: List[str]):
    return a

x = f(1)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType("List[str]");
        }

        [TestMethod, Priority(0)]
        public async Task FunctionFetchingFromList() {
            const string code = @"
from typing import List

def f(a: List[str], index: int):
    return a[index]

x = f(1)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task TypeVarFunc() {
            const string code = @"
from typing import Sequence, TypeVar

T = TypeVar('T') # Declare type variable

def first(l: Sequence[T]) -> T: # Generic function
    return l[0]

arr = [1, 2, 3]
x = first(arr)  # should be int
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericArguments() {
            const string code = @"
from typing import TypeVar

T = TypeVar('T', str, bytes)

def longest(x: T, y: T):
    return x if len(x) >= len(y) else y

x = longest('a', 'bc')
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("longest")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0)
                .Which.Should().HaveName("x")
                .And.HaveType("T");

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task TupleContent() {
            const string code = @"
from typing import Tuple

t: Tuple[int, str]
x = t[0]
y = t[1]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("t").OfType("Tuple[int, str]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task TupleOfTuple() {
            const string code = @"
from typing import Tuple

t: Tuple[Tuple[int, str], bool]
x = t[0]
y = t[1]
z0 = x[0]
z1 = x[1]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("t").OfType("Tuple[Tuple[int, str], bool]")
                .And.HaveVariable("x").OfType("Tuple[int, str]")
                .And.HaveVariable("y").OfType(BuiltinTypeId.Bool)
                .And.HaveVariable("z0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("z1").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task IteratorParamTypeMatch() {
            const string code = @"
from typing import Iterator, List, TypeVar

T = TypeVar('T')

@overload
def f(a: Iterator[T]) -> str: ...
@overload
def f(a: int) -> float: ...
def f(a): ...

a: List[str] = ['a', 'b', 'c']
x = f(iter(a))
";
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction("f").Which;

            f.Should().HaveOverloadAt(0)
                .Which.Should().HaveReturnType(BuiltinTypeId.Str)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType("Iterator[T]");

            f.Should().HaveOverloadAt(1)
                .Which.Should().HaveReturnType(BuiltinTypeId.Float)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("a").OfType("List[str]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str);
        }


        [TestMethod, Priority(0)]
        public async Task IterableParamTypeMatch() {
            const string code = @"
from typing import Iterable, List, Tuple, TypeVar

T = TypeVar('T')

@overload
def f(a: Iterable[T]) -> str: ...
@overload
def f(a: int) -> float: ...
def f(a): ...

a: List[str] = ['a', 'b', 'c']

x = f(a)
y = f(1)
";
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction("f").Which;

            f.Should().HaveOverloadAt(0)
                .Which.Should().HaveReturnType(BuiltinTypeId.Str)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType("Iterable[T]");

            f.Should().HaveOverloadAt(1)
                .Which.Should().HaveReturnType(BuiltinTypeId.Float)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("a").OfType("List[str]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task SequenceParamTypeMatch() {
            const string code = @"
from typing import List, Sequence, TypeVar

T = TypeVar('T')

@overload
def f(a: Sequence[T]) -> str: ...
@overload
def f(a: int) -> float: ...
def f(a): pass

a: List[str] = ['a', 'b', 'c']
x = f(a)
";
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction("f").Which;

            f.Should().HaveOverloadAt(0)
                .Which.Should().HaveReturnType(BuiltinTypeId.Str)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType("Sequence[T]");

            f.Should().HaveOverloadAt(1)
                .Which.Should().HaveReturnType(BuiltinTypeId.Float)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("a").OfType("List[str]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task MappingParamTypeMatch() {
            const string code = @"
from typing import Dict, Mapping, TypeVar

KT = TypeVar('KT')
KV = TypeVar('KV')

@overload
def f(a: Mapping[KT, KV]) -> str: ...
@overload
def f(a: int) -> float: ...
def f(a): ...

a: Dict[str, int]
x = f(a)
";
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction("f").Which;

            f.Should().HaveOverloadAt(0)
                .Which.Should().HaveReturnType(BuiltinTypeId.Str)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType("Mapping[KT, KV]");

            f.Should().HaveOverloadAt(1)
                .Which.Should().HaveReturnType(BuiltinTypeId.Float)
                .Which.Should().HaveSingleParameter()
                .Which.Should().HaveName("a").And.HaveType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("a").OfType("Dict[str, int]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListOfTuples() {
            const string code = @"
from typing import List

def ls() -> List[tuple]:
    pass

x = ls()[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("ls")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnDocumentation("List[tuple]");

            analysis.Should().HaveVariable("x").Which
                .Should().HaveType(BuiltinTypeId.Tuple);
        }

        [TestMethod, Priority(0)]
        public async Task DictContent() {
            const string code = @"
from typing import Dict

d: Dict[str, int]
x = d['a']
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"d").OfType("Dict[str, int]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task DictOfDicts() {
            const string code = @"
from typing import Dict

a: Dict[int, Dict[str, float]]
x = a[0]
y = x['a']
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"a").OfType("Dict[int, Dict[str, float]]")
                .And.HaveVariable("x").OfType("Dict[str, float]")
                .And.HaveVariable("y").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task GenericDictArg() {
            const string code = @"
from typing import Dict

def func(a: Dict[int, str]):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("func")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(0)
                .Which.Should().HaveName("a").And.HaveType("Dict[int, str]");
        }

        [TestMethod, Priority(0)]
        public async Task GenericIterator() {
            const string code = @"
from typing import Iterator, List

a: List[str] = ['a', 'b', 'c']
ia = iter(a);
x = next(ia);
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType("List[str]")
                .And.HaveVariable("ia").OfType(BuiltinTypeId.ListIterator)
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        // TODO handle when can make Union type generic, at the moment defining a h = Union[T,str]
        // and trying to make it specific h[int] doesn't work
        /// <returns></returns>
        [Ignore, TestMethod, Priority(0)]
        public async Task GenericUnionTypeAlias() {
            const string code = @"
from typing import TypeVar, Union, 
S = TypeVar('S')
UInt = Union[S, int]
def response(query: str) -> UInt[str]:  
    return UInt[str]

x = response('test')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").Which
                .Should().HaveType("Union[str, int]");
        }

        [TestMethod, Priority(0)]
        public async Task GenericTypeInstance() {
            const string code = @"
from typing import List

l = List[str]()
x = l[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("l").Which
                .Should().HaveType("List[str]");
            analysis.Should().HaveVariable("x").Which
                .Should().HaveType(BuiltinTypeId.Str);
        }


        [TestMethod, Priority(0)]
        public async Task GenericClassBaseForwardRef() {
            const string code = @"
from typing import TypeVar, Generic, List

_E = TypeVar('_E')

class B(Generic[_E]):
    a: A[_E]
    def func(self) -> A[_E]: ...
    def __init__(self, v: _E): ...

class A(Generic[_E]): ...

b = B('a')
x = b.func()
y = b.a
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("b")
                .Which.Should().HaveType("B[str]");

            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType("A[str]");

            analysis.Should().HaveVariable("y")
                .Which.Should().HaveType("A[str]");
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassInstantiationByValue() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T]):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxed = Box(1234)
x = boxed.get()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassSpecificTypeFillingMultipleTypeVariables() {
            const string code = @"
from typing import TypeVar, Generic

T = TypeVar('T')
U = TypeVar('U')

class A(Generic[T]):
    a: T
    def __init__(self, a: T):
        self.a = a

    def get(self) -> T:
        return self.a

class B(A[U]):
    b: U
    def __init__(self, b: U):
        self.b = b
        super().__init__(b)

    def get1(self) -> U:
        return self.b

test = B(5)
x = test.get()
y = test.get1()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            analysis.Should().HaveVariable("test").Which.Should().HaveMembers("get", "get1");
            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericFunctionSequence() {
            const string code = @"
from typing import Typevar, Sequence

T = TypeVar('T')

def first(seq: Sequence[T]) -> T:
    return seq[0]

s = first('foo')
n = first([1, 2, 3])
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("s").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("n").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericMultipleFunctionSequenceSameTypeVariable() {
            const string code = @"
from typing import Typevar, Sequence

T = TypeVar('T')

def first(seq: Sequence[T]) -> T:
    return seq[0]

def last(seq: Sequence[T]) -> T:
    return seq[-1]

s = first('foo')
n = first([1, 2, 3])

s_last = last('foo')
n_last = last([1, 2, 3])
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("s").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("n").Which.Should().HaveType(BuiltinTypeId.Int);

            analysis.Should().HaveVariable("s_last").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("n_last").Which.Should().HaveType(BuiltinTypeId.Int);
        }


        [TestMethod, Priority(0)]
        public async Task GenericClassHalfFilledParameters() {
            const string code = @"
from typing import TypeVar, Generic, List

T = TypeVar('T')
U = TypeVar('U')

class A(Generic[T, U]):
    a: U

    def get(self) -> U:
        return self.a

    def get1(self) -> T:
        return self.a


class B(A[T, int], Generic[T]):
    b: T

    def get2(self) -> T:
        return self.b


b = B('a')
x = b.get()
y = b.get1()
z = b.get2()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("z").Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassRegularBase() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T], list):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxed = Box(1234)
x = boxed.get()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var boxed = analysis.Should().HaveVariable("boxed").Which;
            boxed.Should().HaveMembers("append", "index");
            boxed.Should().NotHaveMember("bit_length");
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassGenericListBase() {
            const string code = @"
from typing import TypeVar, Generic, List

_T = TypeVar('_T')

class Box(Generic[_T], List[_T]):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxed = Box(1)
x = boxed.get()
y = boxed[0]

boxed_int = Box[int](1)
z = boxed_int[0]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            var boxed = analysis.Should().HaveVariable("boxed").Which;
            boxed.Should().HaveMembers("append", "index");
            boxed.Should().NotHaveMember("bit_length");

            var boxedInt = analysis.Should().HaveVariable("boxed_int").Which;
            boxedInt.Should().HaveMembers("append", "index");
            boxedInt.Should().NotHaveMember("bit_length");


            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("z").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassGenericClassBase() {
            const string code = @"
from typing import TypeVar, Generic, List

_T = TypeVar('_T')

class Box(Generic[_T]):
    v: _T
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

class Cube(Box[int]):
    def __init__(self, v: int):
        super().__init__(v)

    def tmp(self):
        return self.get()

c = Cube(5)
x = c.tmp()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);

            var cube = analysis.Should().HaveVariable("c").Which;
            cube.Should().HaveMembers("get");

            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassMultipleGenericClassBase() {
            const string code = @"
from typing import TypeVar, Generic, List

_T = TypeVar('_T')

class A(Generic[_T]):
    v: _T
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

class B(Generic[_T]):
    y: _T
    def __init__(self, y: _T):
        self.y = y

    def get(self) -> _T:
        return self.y

class C(A[int], B[str]):
    def __init__(self, v: int):
        super().__init__(v)

    def tmp(self):
        return self.get()

c = C(5)
x = c.tmp()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var c = analysis.Should().HaveVariable("c").Which;
            c.Should().HaveMembers("get");

            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task NonGenericClassSubclassDict() {
            const string code = @"
from typing import Dict

class StrDict(Dict[str, str]):  # Non-generic subclass 
    def __str__(self) -> str:
        return 'StrDict({})'.format(super().__str__())

d: StrDict[int, int]  # StrDict is not generic, so should not update methods
d = StrDict()
h = d['t']
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("d").Which.Should().HaveType("StrDict");
            analysis.Should().HaveVariable("h").Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassChainMiddleClassSpecific() {
            const string code = @"
from typing import TypeVar, Generic, List

_T = TypeVar('_T')

class A(Generic[_T]):
    v: _T
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

class B(A[str], Generic[_T]):
    y: _T
    def __init__(self, y: _T):
        super.__init__(y)
        self.y = y

    def get1(self) -> _T:
        return self.y

class C(B[int]):
    def __init__(self, v: int):
        super().__init__(v)

    def tmp(self):
        return self.get()

c = C(5)
x = c.tmp()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var c = analysis.Should().HaveVariable("c").Which;
            c.Should().HaveMembers("get", "get1");

            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassTypeVariableOrderGenericBase() {
            const string code = @"
from typing import TypeVar, Generic, List

T = TypeVar('T')
S = TypeVar('S')
U = TypeVar('U')

class One(Generic[T]): ...
class Two(Generic[T]): ...

class First(One[T], Two[S]): 
    def getT(self) -> T:
        pass

    def getS(self) -> S:
        pass

class Second(One[T], Another[S], Generic[S, U, T]): 
    def getS(self) -> S:
        pass

    def getU(self) -> U:
        pass

    def getT(self) -> T:
        pass

x: First[int, str]
x = First(1, 'hi')
x1 = x.getT()
x2 = x.getS()

y: Second[int, str, Any]
y = Second(1, 'hi', 2.0)
y1 = y.getS()
y2 = y.getU()
y3 = y.getT()

";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").Which.Should().HaveMembers("getS", "getT");
            analysis.Should().HaveVariable("x1").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("x2").Which.Should().HaveType(BuiltinTypeId.Str);

            analysis.Should().HaveVariable("y").Which.Should().HaveMembers("getS", "getU", "getT");
            analysis.Should().HaveVariable("y1").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y2").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("y3").Which.Should().HaveType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassBaseChain() {
            const string code = @"
from typing import TypeVar, Generic, List

_T = TypeVar('_T')

class A(Generic[_T]):
    v: _T
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

class B(A[_T]):
    y: _T
    def __init__(self, y: _T):
        self.y = y
        super().__init__(y)

    def get1(self) -> _T:
        return self.y

class C(B[int]):
    def __init__(self, v: int):
        super().__init__(v)

c = C(5)
x = c.get()
y = c.get1()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().BeEmpty();
            var c = analysis.Should().HaveVariable("c").Which;
            c.Should().HaveMembers("get", "get1");

            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassMultipleTypeVarsDefinedByOneTypeVar() {
            const string code = @"
from typing import TypeVar, Generic, List

T = TypeVar('T')
K = TypeVar('K')

class A(Generic[T, K]):
    v: T
    def __init__(self, v: T):
        self.v = v

    def getT(self) -> T:
        return self.v

    def getK(self) -> K:
        return self.v

class B(A[T, T]):
    y: T
    def __init__(self, y: T):
        self.y = y
        super().__init__(y)

    def get1(self) -> T:
        return self.y

c = B(5)
x = c.getT()
y = c.getK()
z = c.get1()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().BeEmpty();
            var c = analysis.Should().HaveVariable("c").Which;
            c.Should().HaveMembers("getT", "getK", "get1");

            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("z").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassMultipleGenericClassBaseDifferentOrder() {
            const string code = @"
from typing import TypeVar, Generic, List

_T = TypeVar('_T')

class A(Generic[_T]):
    y: _T
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

class B(Generic[_T]):
    y: _T
    def __init__(self, y: _T):
        self.y = y

    def get(self) -> _T:
        return self.y

class C(A[str], B[str]):
    def __init__(self, v: int):
        super().__init__(v)

    def tmp(self):
        return self.get()

c = C('str')
x = c.tmp()
y = c.get()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var c = analysis.Should().HaveVariable("c").Which;
            c.Should().HaveMembers("get");

            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassDictBase() {
            const string code = @"
from typing import TypeVar, Generic, Dict

_T = TypeVar('_T')
_E = TypeVar('_E')

class D(Generic[_T, _E], Dict[_T, _E]): ...

di = {1:'a', 2:'b'}
d = D(di)
x = d.get()
y = d[0]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            var d = analysis.Should().HaveVariable("d").Which;
            d.Should().HaveMembers("get", "keys", "values");

            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassSelf() {
            const string code = @"
from typing import TypeVar

T = TypeVar('T', bound='Shape')

class Shape:
    def set_scale(self: T, scale: float) -> T:
        self.scale = scale
        return self

class Circle(Shape):
    def set_radius(self, r: float) -> 'Circle':
        self.radius = r
        return self

class Square(Shape):
    def set_width(self, w: float) -> 'Square':
        self.width = w
        return self

circle = Circle().set_scale(0.5).set_radius(2.7)  
square = Square().set_scale(0.5).set_width(3.2)  
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("circle").Which.Should().HaveType("Circle");
            analysis.Should().HaveVariable("square").Which.Should().HaveType("Square");
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassToDifferentTypes() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T]):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxedint = Box(1234)
x = boxedint.get()

boxedstr = Box('str')
y = boxedstr.get()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("y")
                .Which.Should().HaveType(BuiltinTypeId.Str);
        }


        [TestMethod, Priority(0)]
        public async Task GenericFunctionArguments() {
            const string code = @"
import unittest

class Simple(unittest.TestCase):
    def test_exception(self):
        return self.assertRaises(TypeError).exception

x = Simple().test_exception();
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").Which.Should().HaveType("TypeError");
        }

        [TestMethod, Priority(0)]
        public async Task GenericConstructorArguments() {
            const string code = @"
from typing import TypeVar, Generic
from logging import Logger, getLogger

T = TypeVar('T')

class LoggedVar(Generic[T]):
    def __init__(self, value: T, name: str, logger: Logger) -> None:
        self.name = name
        self.logger = logger
        self.value = value

    def set(self, new: T) -> None:
        self.log('Set ' + repr(self.value))
        self.value = new

    def get(self) -> T:
        self.log('Get ' + repr(self.value))
        return self.value

    def log(self, message: str) -> None:
        self.logger.info('%s: %s', self.name, message)

v = LoggedVar(1234, 'name', getLogger('oh_no'))
x = v.get()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }
    }
}
