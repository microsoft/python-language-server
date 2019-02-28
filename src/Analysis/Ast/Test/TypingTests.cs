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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class TypingTests : AnalysisTestBase {
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
        public async Task TypeVarSimple() {
            const string code = @"
from typing import TypeVar

T = TypeVar('T', str, bytes)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("T")
                .Which.Value.Should().HaveDocumentation("TypeVar('T', str, bytes)");
            analysis.Should().HaveVariable("T").OfType(typeof(IGenericTypeParameter));
        }

        [TestMethod, Priority(0)]
        public async Task TypeVarStringConstraint() {
            const string code = @"
from typing import TypeVar
import io

T = TypeVar('T', bound='io.TextIOWrapper')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("T")
                .Which.Value.Should().HaveDocumentation("TypeVar('T', TextIOWrapper)");
            analysis.Should().HaveVariable("T").OfType(typeof(IGenericTypeParameter));
        }


        [TestMethod, Priority(0)]
        [Ignore]
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
        public async Task TypeVarIncomplete() {
            const string code = @"
from typing import TypeVar

_ = TypeVar()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("_").WithNoTypes();
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
        public async Task TypeAlias() {
            const string code = @"
Url = str

def f(a: Url) -> Url: ...
def f(a: int) -> float: ...

u: Url
x = f('s')
y = f(u)
z = f(1)
";

            var analysis = await GetAnalysisAsync(code);
            // TODO: should it be Url? Should we rename type copy on assignment? How to match types then?
            analysis.Should().HaveVariable("u").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Float);
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
        public async Task TypingListOfTuples() {
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

        [TestMethod, Priority(0)]
        public async Task NewType() {
            const string code = @"
from typing import NewType

Foo = NewType('Foo', dict)
foo: Foo = Foo({ })
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("Foo").OfType("Foo")
                .And.HaveVariable("foo").OfType("Foo");
            analysis.Should().HaveVariable("Foo").Which.Should().HaveMembers("keys", "values");
        }

        [TestMethod, Priority(0)]
        public async Task Containers() {
            const string code = @"
from typing import *

i : SupportsInt = ...
lst_i : List[int] = ...
lst_i_0 = lst_i[0]

u : Union[Mapping[int, str], MappingView[str, float], MutableMapping[int, List[str]]] = ...

dct_s_i : Mapping[str, int] = ...
dct_s_i_a = dct_s_i['a']
dct_s_i_keys = dct_s_i.keys()
dct_s_i_key = next(iter(dct_s_i_keys))
dct_s_i_values = dct_s_i.values()
dct_s_i_value = next(iter(dct_s_i_values))
dct_s_i_items = dct_s_i.items()
dct_s_i_item_1, dct_s_i_item_2 = next(iter(dct_s_i_items))

dctv_s_i_keys : KeysView[str] = ...
dctv_s_i_key = next(iter(dctv_s_i_keys))
dctv_s_i_values : ValuesView[int] = ...
dctv_s_i_value = next(iter(dctv_s_i_values))
dctv_s_i_items : ItemsView[str, int] = ...
dctv_s_i_item_1, dctv_s_i_item_2 = next(iter(dctv_s_i_items))
";
            ;
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("lst_i").OfType("List[int]")
                .And.HaveVariable("lst_i_0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("u").OfType("Union[Mapping[int, str], MappingView[str, float], MutableMapping[int, List[str]]]")
                .And.HaveVariable("dct_s_i").OfType("Mapping[str, int]")
                .And.HaveVariable("dct_s_i_a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dct_s_i_keys").OfType("KeysView[str]")
                .And.HaveVariable("dct_s_i_key").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dct_s_i_values").OfType("ValuesView[int]")
                .And.HaveVariable("dct_s_i_value").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dct_s_i_items").OfType("ItemsView[str, int]")
                .And.HaveVariable("dct_s_i_item_1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dct_s_i_item_2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dctv_s_i_keys").OfType("KeysView[str]")
                .And.HaveVariable("dctv_s_i_key").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dctv_s_i_values").OfType("ValuesView[int]")
                .And.HaveVariable("dctv_s_i_value").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dctv_s_i_items").OfType("ItemsView[str, int]")
                .And.HaveVariable("dctv_s_i_item_1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dctv_s_i_item_2").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task NamedTypeAlias() {
            const string code = @"
from typing import *

MyList = List[str]
MyTuple = Tuple[int, str]

sl : MyList = ...
sl_0 = sl[0]

t : MyTuple = ...
t_0 = t[0]

";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("sl").OfType("List[str]")
                .And.HaveVariable("sl_0").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("t").OfType("Tuple[int, str]")
                .And.HaveVariable("t_0").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task NamedTuple() {
            const string code = @"
from typing import *

n1 : NamedTuple('n1', [('x', int), ('y', float)]) = ...

n1_x = n1.x
n1_y = n1.y

n1_0 = n1[0]
n1_1 = n1[1]

n1_m2 = n1[-2]
n1_m1 = n1[-1]

i = 0
i = 1
n1_i = n1[i]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("n1").OfType("n1(x: int, y: float)")

                .And.HaveVariable("n1_x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n1_y").OfType(BuiltinTypeId.Float)

                .And.HaveVariable("n1_0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n1_1").OfType(BuiltinTypeId.Float)

                .And.HaveVariable("n1_m2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n1_m1").OfType(BuiltinTypeId.Float)

                .And.HaveVariable("n1_i").OfType(BuiltinTypeId.Float);

            var n1 = analysis.Should().HaveVariable("n1").Which;
            n1.Should().HaveMember("x").Which.Should().HaveType(BuiltinTypeId.Int);
            n1.Should().HaveMember("y").Which.Should().HaveType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task AnyStr() {
            const string code = @"
from typing import AnyStr

n1 : AnyStr = 'abc'
x = n1[0]

n2 : AnyStr = b'abc'
y = n2[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("n1").OfType("AnyStr")
                .And.HaveVariable("x").OfType("AnyStr")
                .And.HaveVariable("y").OfType("AnyStr");
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

class A(Generic[_E], List[_E]): ...

b = B[str]()
x = b.func()
y = b.a
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("b")
                .Which.Should().HaveType("B[str]");

            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType("A[str]")
                .Which.Should().HaveMembers("append", "index");

            analysis.Should().HaveVariable("y")
                .Which.Should().HaveType("A[str]")
                .Which.Should().HaveMembers("append", "index");
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassInstantiation() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T]):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxed = Box[int]()
x = boxed.get()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType(BuiltinTypeId.Int);
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

boxed = Box(1234)
x = boxed.get()
y = boxed[0]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);

            var boxed = analysis.Should().HaveVariable("boxed").Which;
            boxed.Should().HaveMembers("append", "index");
            boxed.Should().NotHaveMember("bit_length");

            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassMultipleArgumentsListBase() {
            const string code = @"
from typing import TypeVar, Generic, List

_T = TypeVar('_T')
_E = TypeVar('_E')

class Box(Generic[_T, _E], List[_E]):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxed = Box(1234, 'abc')
x = boxed.get()
y = boxed[0]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").Which.Should().HaveType(BuiltinTypeId.Int);

            var boxed = analysis.Should().HaveVariable("boxed").Which;
            boxed.Should().HaveMembers("append", "index");
            boxed.Should().NotHaveMember("bit_length");

            analysis.Should().HaveVariable("y").Which.Should().HaveType(BuiltinTypeId.Str);
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
        public async Task UnionMembers() {
            const string code = @"
from typing import Union, List

class A:
    def m1(self): ...

u = Union[A, List[str]]
z = u[0]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("u")
                .Which.Should().HaveType("Union[A, List[str]]")
                .Which.Should().HaveMembers("m1", "index", "append");

            analysis.Should().HaveVariable("z")
                .Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public void AnnotationParsing() {
            AssertTransform("List", "NameOp:List");
            AssertTransform("List[Int]", "NameOp:List", "NameOp:Int", "MakeGenericOp");
            AssertTransform("Dict[Int, Str]", "NameOp:Dict", "StartListOp", "NameOp:Int", "NameOp:Str", "MakeGenericOp");

            AssertTransform("'List'", "NameOp:List");
            AssertTransform("List['Int']", "NameOp:List", "NameOp:Int", "MakeGenericOp");
            AssertTransform("Dict['Int, Str']", "NameOp:Dict", "StartListOp", "NameOp:Int", "NameOp:Str", "MakeGenericOp");
        }

        [TestMethod, Priority(0)]
        public void AnnotationConversion() {
            AssertConvert("List");
            AssertConvert("List[Int]");
            AssertConvert("Dict[Int, Str]");
            AssertConvert("typing.Container[typing.Iterable]");

            AssertConvert("List");
            AssertConvert("'List[Int]'", "List[Int]");
            AssertConvert("Dict['Int, Str']", "Dict[Int, Str]");
            AssertConvert("typing.Container['typing.Iterable']", "typing.Container[typing.Iterable]");
        }

        private static void AssertTransform(string expr, params string[] steps) {
            var ta = Parse(expr);
            ta.GetTransformSteps().Should().Equal(steps);
        }

        private static void AssertConvert(string expr, string expected = null) {
            var ta = Parse(expr);
            var actual = ta.GetValue<string>(new StringConverter());
            Assert.AreEqual(expected ?? expr, actual);
        }

        private static TypeAnnotation Parse(string expr, PythonLanguageVersion version = PythonLanguageVersion.V36) {
            var errors = new CollectingErrorSink();
            var ops = new ParserOptions { ErrorSink = errors };
            var p = Parser.CreateParser(new StringReader(expr), version, ops);
            var ast = p.ParseTopExpression();
            if (errors.Errors.Any()) {
                foreach (var e in errors.Errors) {
                    Console.WriteLine(e);
                }

                Assert.Fail(string.Join("\n", errors.Errors.Select(e => e.ToString())));
                return null;
            }

            var node = Statement.GetExpression(ast.Body);
            return new TypeAnnotation(version, node);
        }

        private class StringConverter : TypeAnnotationConverter<string> {
            public override string LookupName(string name) => name;
            public override string GetTypeMember(string baseType, string member) => $"{baseType}.{member}";
            public override string MakeUnion(IReadOnlyList<string> types) => string.Join(", ", types);
            public override string MakeGeneric(string baseType, IReadOnlyList<string> args) => $"{baseType}[{string.Join(", ", args)}]";

            public override IReadOnlyList<string> GetUnionTypes(string unionType) => unionType.Split(',').Select(n => n.Trim()).ToArray();

            public override string GetBaseType(string genericType) {
                int i = genericType.IndexOf('[');
                if (i < 0) {
                    return null;
                }

                return genericType.Remove(i);
            }
        }
    }
}
