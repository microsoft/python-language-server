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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
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

def f(a: Iterator[T]) -> str: ...
def f(a: int) -> float: ...

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

def f(a: Iterable[T]) -> str: ...
def f(a: int) -> float: ...

a: List[str] = ['a', 'b', 'c']
b: Tuple[str, int, float]

x = f(a)
y = f(b)
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
                .And.HaveVariable("b").OfType("Tuple[str, int, float]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task SequenceParamTypeMatch() {
            const string code = @"
from typing import List, Sequence, TypeVar

T = TypeVar('T')

def f(a: Sequence[T]) -> str: ...
def f(a: int) -> float: ...

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

def f(a: Mapping[KT, KV]) -> str: ...
def f(a: int) -> float: ...

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
lst : List = ...
lst_i : List[int] = ...
lst_i_0 = lst_i[0]
dct : Union[Mapping, MappingView, MutableMapping] = ...
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
                .And.HaveVariable("lst").OfType(BuiltinTypeId.List)
                .And.HaveVariable("lst_i").OfType(BuiltinTypeId.List)
                .And.HaveVariable("lst_i_0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dct").OfType("Union[Mapping, MappingView, MutableMapping]")
                .And.HaveVariable("dct_s_i").OfType("Mapping[str, int]")
                .And.HaveVariable("dct_s_i_a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dct_s_i_keys").OfType("dict_keys[str]")
                .And.HaveVariable("dct_s_i_key").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dct_s_i_values").OfType("dict_values[int]")
                .And.HaveVariable("dct_s_i_value").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dct_s_i_items").OfType("dict_items[tuple[str, int]]")
                .And.HaveVariable("dct_s_i_item_1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dct_s_i_item_2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dctv_s_i_keys").OfType(BuiltinTypeId.DictKeys)
                .And.HaveVariable("dctv_s_i_key").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dctv_s_i_values").OfType(BuiltinTypeId.DictValues)
                .And.HaveVariable("dctv_s_i_value").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("dctv_s_i_items").OfType(BuiltinTypeId.DictItems)
                .And.HaveVariable("dctv_s_i_item_1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("dctv_s_i_item_2").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        [Ignore]
        public async Task NamedTypeAlias() {
            const string code = @"
from typing import *

MyInt = int
MyStrList = List[str]
MyNamedTuple = NamedTuple('MyNamedTuple', [('x', MyInt)])

i : MyInt = ...
sl : MyStrList = ...
sl_0 = sl[0]
n1 : MyNamedTuple = ...
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("sl").OfType(BuiltinTypeId.List)
                .And.HaveVariable("sl_0").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("n1").OfType("MyNamedTuple(x: int)")
                .Which.Should().HaveMember("x")
                .Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        [Ignore]
        public async Task NamedTuple() {
            var code = @"
from typing import *

n : NamedTuple = ...
n1 : NamedTuple('n1', [('x', int), ['y', float]]) = ...
n2 : ""NamedTuple('n2', [('x', int), ['y', float]])"" = ...

n1_x = n1.x
n1_y = n1.y
n2_x = n2.x
n2_y = n2.y

n1_0 = n1[0]
n1_1 = n1[1]
n2_0 = n2[0]
n2_1 = n2[1]

n1_m2 = n1[-2]
n1_m1 = n1[-1]
n2_m2 = n2[-2]
n2_m1 = n2[-1]

i = 0
i = 1
n1_i = n1[i]
n2_i = n2[i]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("n").OfType("tuple")
                .And.HaveVariable("n1").OfType("n1(x: int, y: float)")
                .And.HaveVariable("n2").OfType("n2(x: int, y: float)")

                .And.HaveVariable("n1_x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n1_y").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("n2_x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n2_y").OfType(BuiltinTypeId.Float)

                .And.HaveVariable("n1_0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n1_1").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("n2_0").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n2_1").OfType(BuiltinTypeId.Float)

                .And.HaveVariable("n1_m2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n1_m1").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("n2_m2").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n2_m1").OfType(BuiltinTypeId.Float)

                .And.HaveVariable("n1_i").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("n2_i").OfType(BuiltinTypeId.Int);
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
            var ops = new ParserOptions {ErrorSink = errors};
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
