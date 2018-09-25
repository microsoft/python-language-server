// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class TypeAnnotationTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

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

        private static void AssertTransform(string expr, params string[] steps) {
            var ta = Parse(expr);
            ta.GetTransformSteps().Should().Equal(steps);
        }

        private static void AssertConvert(string expr, string expected = null) {
            var ta = Parse(expr);
            var actual = ta.GetValue(new StringConverter());
            Assert.AreEqual(expected ?? expr, actual);
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

            public override IReadOnlyList<string> GetGenericArguments(string genericType) {
                if (!genericType.EndsWith("]")) {
                    return null;
                }

                int i = genericType.IndexOf('[');
                if (i < 0) {
                    return null;
                }

                return genericType.Substring(i + 1, genericType.Length - i - 2).Split(',').Select(n => n.Trim()).ToArray();
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleContainerAnalysis() {
            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"from typing import *

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
");

                analysis.ProjectState.Modules.TryGetImportedModule("typing", out var module).Should().BeTrue();
                module.AnalysisModule.Should().BeOfType<TypingModuleInfo>();

                analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("lst").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("lst_i").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("lst_i_0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("dct").OfType(BuiltinTypeId.Dict)
                    .And.HaveVariable("dct_s_i").OfType(BuiltinTypeId.Dict).WithDescription("dict[str, int]")
                    .And.HaveVariable("dct_s_i_a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("dct_s_i_keys").OfType(BuiltinTypeId.DictKeys).WithDescription("dict_keys[str]")
                    .And.HaveVariable("dct_s_i_key").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("dct_s_i_values").OfType(BuiltinTypeId.DictValues).WithDescription("dict_values[int]")
                    .And.HaveVariable("dct_s_i_value").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("dct_s_i_items").OfType(BuiltinTypeId.DictItems).WithDescription("dict_items[tuple[str, int]]")
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
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleProtocolAnalysis() {
            var configuration = PythonVersions.Required_Python36X;
            using (var server = await new Server().InitializeAsync(configuration)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"from typing import *

i : Iterable = ...
ii : Iterator = ...
i_int : Iterable[int] = ...
ii_int : Iterator[int] = ...
g_int : Generator[int] = ...

call_i_s : Callable[[int], str] = ...
call_i_s_ret = call_i_s()
call_iis_i : Callable[[int, int, str], int] = ...
call_iis_i_ret = call_iis_i()
");

                analysis.Should().HaveVariable("i").WithDescription("iterable")
                    .And.HaveVariable("ii").WithDescription("iterator")
                    .And.HaveVariable("i_int").WithDescription("iterable[int]")
                    .And.HaveVariable("ii_int").WithDescription("iterator[int]")
                    .And.HaveVariable("g_int").WithDescription("generator[int]")

                    .And.HaveVariable("call_i_s").OfType(BuiltinTypeId.Function)
                    .And.HaveVariable("call_i_s_ret").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("call_iis_i").OfType(BuiltinTypeId.Function)
                    .And.HaveVariable("call_iis_i_ret").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleNamedTupleAnalysis() {
            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"from typing import *

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
");

                analysis.Should().HaveVariable("n").WithDescription("tuple")
                    .And.HaveVariable("n1").WithDescription("n1(x: int, y: float)")
                    .And.HaveVariable("n2").WithDescription("n2(x: int, y: float)")

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

                    .And.HaveVariable("n1_i").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveVariable("n2_i").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleNamedTypeAlias() {
            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"from typing import *

MyInt = int
MyStrList = List[str]
MyNamedTuple = NamedTuple('MyNamedTuple', [('x', MyInt)])

i : MyInt = ...
sl : MyStrList = ...
sl_0 = sl[0]
n1 : MyNamedTuple = ...
");

                analysis.GetValues("n1.x", SourceLocation.MinValue);

                analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("sl").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("sl_0").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("n1")
                        .WithDescription("MyNamedTuple(x: int)")
                        .WithValue<ProtocolInfo>()
                    .Which.Should().HaveMember<IBuiltinInstanceInfo>("x")
                    .Which.Should().HaveType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleNestedIndex() {
            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"from typing import *

MyList = List[List[str]]

l_l_s : MyList = ...
l_s = l_l_s[0]
s = l_s[0]
");

                analysis.Should().HaveVariable("l_l_s").OfType(BuiltinTypeId.List).
                    And.HaveVariable("l_s").OfType(BuiltinTypeId.List).
                    And.HaveVariable("s").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleGenerator() {
            var code = @"from typing import *

gen : Generator[str, None, int] = ...

def g():
    x = yield from gen

g_g = g()
g_i = next(g_g)
";
            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("g_g").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("g_i").OfType(BuiltinTypeId.Str)
                    .And.HaveFunction("g")
                    .Which.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctionAnnotation() {
            var code = @"
def f(a : int, b : float) -> str: pass

x = f()
";
            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("f").WithValue<IFunctionInfo>()
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameterAt(0).WithName("a").WithType("int")
                    .And.HaveParameterAt(1).WithName("b").WithType("float")
                    .And.HaveSingleReturnType("str");
            }
        }

        private async Task TypingModuleDocumentationExampleAsync(string code, IEnumerable<string> signatures) {
            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                foreach (var sig in signatures) {
                    int i = sig.IndexOf(':');
                    Assert.AreNotEqual(-1, i, sig);
                    var f = analysis.GetSignatures(sig.Substring(0, i), SourceLocation.MinValue);
                    var actualSig = string.Join("|", f.Select(o => o.ToString()));

                    Console.WriteLine("Expected: {0}", sig.Substring(i + 1));
                    Console.WriteLine("Actual:   {0}", actualSig);

                    Assert.AreEqual(sig.Substring(i + 1), actualSig);
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_1() {
            await TypingModuleDocumentationExampleAsync(@"def greeting(name: str) -> str:
    return 'Hello ' + name
", 
                new[] {
                    "greeting:greeting(name:str=)->[str]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_2() {
            await TypingModuleDocumentationExampleAsync(@"from typing import List
Vector = List[float]

def scale(scalar: float, vector: Vector) -> Vector:
    return [scalar * num for num in vector]

# typechecks; a list of floats qualifies as a Vector.
new_vector = scale(2.0, [1.0, -4.2, 5.4])
",
                new[] {
                    "scale:scale(scalar:float=,vector:list[float, float, float], list[float]=)->[list[float]]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_3() {
            await TypingModuleDocumentationExampleAsync(@"from typing import Dict, Tuple, List

ConnectionOptions = Dict[str, str]
Address = Tuple[str, int]
Server = Tuple[Address, ConnectionOptions]

def broadcast_message(message: str, servers: List[Server]) -> None:
    ...

# The static type checker will treat the previous type signature as
# being exactly equivalent to this one.
def broadcast_message(
        message: str,
        servers: List[Tuple[Tuple[str, int], Dict[str, str]]]) -> None:
    ...
",
                new[] {
                    // Two matching functions means only one overload is returned
                    "broadcast_message:broadcast_message(message:str=,servers:list[tuple[tuple[str, int], dict[str, str]]]=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_4() {
            await TypingModuleDocumentationExampleAsync(@"from typing import NewType

UserId = NewType('UserId', int)
some_id = UserId(524313)

def get_user_name(user_id: UserId) -> str:
    ...

# typechecks
user_a = get_user_name(UserId(42351))

# does not typecheck; an int is not a UserId
user_b = get_user_name(-1)
",
                new[] {
                    "get_user_name:get_user_name(user_id:int, UserId=)->[str]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_5() {
            await TypingModuleDocumentationExampleAsync(@"from typing import NewType

UserId = NewType('UserId', int)

# Fails at runtime and does not typecheck
class AdminUserId(UserId): pass

ProUserId = NewType('ProUserId', UserId)

def f(u : UserId, a : AdminUserId, p : ProUserId):
    return p
",
                new[] {
                    "f:f(u:UserId=,a:AdminUserId=,p:ProUserId=)->[ProUserId]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_6() {
            await TypingModuleDocumentationExampleAsync(@"from typing import Callable

def feeder(get_next_item: Callable[[], str]) -> None:
    # Body
    pass

def async_query(on_success: Callable[[int], None],
                on_error: Callable[[int, Exception], None]) -> None:
    # Body
    pass

",
                new[] {
                    "feeder:feeder(get_next_item:function() -> str=)->[]",
                    "async_query:async_query(on_success:function(int)=,on_error:function(int, Exception)=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_7() {
            await TypingModuleDocumentationExampleAsync(@"from typing import Mapping, Sequence

class Employee: pass

def notify_by_email(employees: Sequence[Employee],
                    overrides: Mapping[str, str]) -> None: ...
",
                new[] {
                    "notify_by_email:notify_by_email(employees:list[Employee]=,overrides:dict[str, str]=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_8() {
            await TypingModuleDocumentationExampleAsync(@"from typing import Sequence, TypeVar

T = TypeVar('T')      # Declare type variable

def first(l: Sequence[T]) -> T:   # Generic function
    return l[0]
",
                new[] {
                    "first:first(l:list[T]=)->[T]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_9() {
            await TypingModuleDocumentationExampleAsync(@"from typing import TypeVar, Generic, Iterable
from logging import Logger

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

def zero_all_vars(vars: Iterable[LoggedVar[int]]) -> None:
    for var in vars:
        var.set(0)
",
                new[] {
                    "LoggedVar.set:set(self:LoggedVar=,new:int, T=)->[]",
                    "zero_all_vars:zero_all_vars(vars:iterable[LoggedVar]=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task TypingModuleDocumentationExample_10() {
            await TypingModuleDocumentationExampleAsync(@"from typing import TypeVar, Generic
...

T = TypeVar('T')
S = TypeVar('S', int, str)

class StrangePair(Generic[T, S]):
    ...

class Pair(Generic[T, T]):   # INVALID
    ...

class LinkedList(Sized, Generic[T]):
    ...

class MyDict(Mapping[str, T]):
    ...

def f(s: StrangePair[int, int], p: Pair, l: LinkedList, m: MyDict): ...
",
                new[] {
                    "f:f(s:StrangePair=,p:Pair=,l:LinkedList=,m:MyDict=)->[]"
                }
            );
        }
    }
}
