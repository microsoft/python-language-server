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
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task TypeVarSimple() {
            const string code = @"
from typing import TypeVar

T = TypeVar('T', str, bytes)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("T")
                .Which.Value.Should().HaveDocumentation("TypeVar('T', str, bytes)");
            analysis.Should().HaveGenericVariable("T");
        }

        [TestMethod, Priority(0)]
        public async Task TypeVarStringConstraint() {
            const string code = @"
from typing import TypeVar
import io

T = TypeVar('T', bound='io.TextIOWrapper')
";
            var analysis = await GetAnalysisAsync(code, runIsolated: true);
            analysis.Should().HaveVariable("T")
                .Which.Value.Should().HaveDocumentation("TypeVar('T', bound=io.TextIOWrapper)");
            analysis.Should().HaveGenericVariable("T");
        }


        [TestMethod, Priority(0)]
        public async Task TypeVarCovariantDocCheck() {
            const string code = @"
from typing import TypeVar

T = TypeVar('T', str, int, covariant=True)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("T")
                .Which.Value.Should().HaveDocumentation("TypeVar('T', str, int, covariant=True)");
            analysis.Should().HaveGenericVariable("T");
        }


        [TestMethod, Priority(0)]
        public async Task TypeVarBoundToUnknown() {
            const string code = @"
from typing import TypeVar
X = TypeVar('X', bound='hello', covariant=True)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("X")
                .Which.Value.Should().HaveDocumentation("TypeVar('X', bound=Unknown, covariant=True)");
            analysis.Should().HaveGenericVariable("X");
        }

        [TestMethod, Priority(0)]
        public async Task TypeVarBoundToStringName() {
            const string code = @"
from typing import TypeVar

X = TypeVar('X', bound='hello', covariant=True)

class hello: ...
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("X")
                .Which.Value.Should().HaveDocumentation("TypeVar('X', bound=hello, covariant=True)");
            analysis.Should().HaveGenericVariable("X");
        }

        [Ignore]
        [TestMethod, Priority(0)]
        public async Task KeywordBinOpDocCheck() {
            // TODO need to evaluate boolean binary expressions to return values inside ExpressionEval.Operators
            // before this test can pass
            const string code = @"
from typing import TypeVar

a = 2

X = TypeVar('X', bound='hello' + 'tmp', covariant= a == 2)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("X")
                .Which.Value.Should().HaveDocumentation("TypeVar('X', bound=hellotmp, covariant=True)");
            analysis.Should().HaveGenericVariable("X");
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

            analysis.Should().HaveVariable("lst_i").OfType("List[int]")
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
            analysis.Should().HaveVariable("n1").OfType("n1")
                .Which.Value.Should().HaveDocumentation("n1(x: int, y: float)");

            analysis.Should().HaveVariable("n1_x").OfType(BuiltinTypeId.Int)
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
            analysis.Should().HaveVariable("n1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task FStringIsStringType() {
            const string code = @"
x = f'{1}'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task OptionalNone() {
            const string code = @"
import typing

class C:
    def __init__(self, x: typing.Optional[typing.Mapping[str, str]] = None):
        self.X = x

c = C()
y = c.X
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var r = analysis.Should().HaveVariable("y").OfType("Mapping[str, str]");
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
        public async Task TupleOfNones() {
            const string code = @"
from typing import Tuple

x: Tuple[None, None, None]
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType("Tuple[None, None, None]");
        }

        [TestMethod, Priority(0)]
        public async Task UnionOfTuples() {
            const string code = @"
from typing import Union, Tuple, Type

class TracebackType: ...

_ExcInfo = Tuple[Type[BaseException], BaseException, TracebackType]
_OptExcInfo = Union[_ExcInfo, Tuple[None, None, None]]

x: _ExcInfo
y: _OptExcInfo

a, b, c = y
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            analysis.Should().HaveVariable("x")
                .Which.Should().HaveType("Tuple[Type[BaseException], BaseException, TracebackType]");

            analysis.Should().HaveVariable("y")
                .Which.Should().HaveType("Union[Tuple[Type[BaseException], BaseException, TracebackType], Tuple[None, None, None]]");

            analysis.Should().HaveVariable("a")
                .Which.Should().HaveType("Union[Type[BaseException], None]");

            analysis.Should().HaveVariable("b")
                .Which.Should().HaveType("Union[BaseException, None]");

            analysis.Should().HaveVariable("c")
                .Which.Should().HaveType("Union[TracebackType, None]");
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
            var ast = p.ParseTopExpression(null);
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
