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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.UnitTests.Core.MSTest;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public partial class AnalysisTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        #region Test Cases

        [TestMethod, Priority(0)]
        public void CheckInterpreterV2() {
            using (var interp = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7)).CreateInterpreter()) {
                try {
                    interp.GetBuiltinType((BuiltinTypeId)(-1));
                    Assert.Fail("Expected KeyNotFoundException");
                } catch (KeyNotFoundException) {
                }
                var intType = interp.GetBuiltinType(BuiltinTypeId.Int);
                Assert.IsTrue(intType.ToString() != "");
            }
        }

        [TestMethod, Priority(0)]
        public void CheckInterpreterV3() {
            using (var interp = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(3, 6)).CreateInterpreter()) {
                try {
                    interp.GetBuiltinType((BuiltinTypeId)(-1));
                    Assert.Fail("Expected KeyNotFoundException");
                } catch (KeyNotFoundException) {
                }
                var intType = interp.GetBuiltinType(BuiltinTypeId.Int);
                Assert.IsTrue(intType.ToString() != "");
            }
        }

        [TestMethod, Priority(0)]
        public async Task SpecialArgTypes() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var code = @"def f(*fob, **oar):
    pass
";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should()
                        .HaveParameter("fob").OfType(BuiltinTypeId.Tuple)
                    .And.HaveParameter("oar").OfType(BuiltinTypeId.Dict);

                code = @"def f(*fob):
    pass

f(42)
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("fob").OfType(BuiltinTypeId.Tuple).WithValue<SequenceInfo>()
                    .Which.Should().HaveIndexType(0, BuiltinTypeId.Int);

                code = @"def f(*fob):
    pass

f(42, 'abc')
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("fob").OfType(BuiltinTypeId.Tuple).WithValue<SequenceInfo>()
                    .Which.Should().HaveIndexTypes(0, BuiltinTypeId.Int, BuiltinTypeId.Str);

                code = @"def f(*fob):
    pass

f(42, 'abc')
f('abc', 42)
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("fob").OfType(BuiltinTypeId.Tuple).WithValue<SequenceInfo>()
                    .Which.Should().HaveIndexTypes(0, BuiltinTypeId.Int, BuiltinTypeId.Str);

                code = @"def f(**oar):
    y = oar['fob']
    pass

f(x=42)
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should()
                        .HaveVariable("y").OfResolvedType(BuiltinTypeId.Int)
                    .And.HaveParameter("oar").OfType(BuiltinTypeId.Dict).WithValue<DictionaryInfo>()
                    .Which.Should().HaveValueType(BuiltinTypeId.Int);

                code = @"def f(**oar):
    z = oar['fob']
    pass

f(x=42, y = 'abc')
";

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunction("f")
                    .Which.Should()
                        .HaveVariable("z").OfResolvedTypes(BuiltinTypeId.Int, BuiltinTypeId.Str)
                    .And.HaveParameter("oar").OfType(BuiltinTypeId.Dict).WithValue<DictionaryInfo>()
                    .Which.Should().HaveValueTypes(BuiltinTypeId.Int, BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TestPackageImportStar() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var fob = server.AddModuleWithContent("fob", "fob\\__init__.py", "from oar import *");
                var oar = server.AddModuleWithContent("fob.oar", "fob\\oar\\__init__.py", "from .baz import *");
                var baz = server.AddModuleWithContent("fob.oar.baz", "fob\\oar\\baz.py", "import fob.oar.quox as quox\r\nfunc = quox.func");
                var quox = server.AddModuleWithContent("fob.oar.quox", "fob\\oar\\quox.py", "def func(): return 42");

                var fobAnalysis = await fob.GetAnalysisAsync();
                var oarAnalysis = await oar.GetAnalysisAsync();
                var bazAnalysis = await baz.GetAnalysisAsync();
                var quoxAnalysis = await quox.GetAnalysisAsync();

                fobAnalysis.Should().HaveVariable("func").WithDescription("fob.oar.quox.func() -> int");
                oarAnalysis.Should().HaveVariable("func").WithDescription("fob.oar.quox.func() -> int");
                bazAnalysis.Should().HaveVariable("func").WithDescription("fob.oar.quox.func() -> int");
                quoxAnalysis.Should().HaveVariable("func").WithDescription("fob.oar.quox.func() -> int");
            }
        }

        [TestMethod, Priority(0)]
        public async Task TestClassAssignSameName() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var code = @"x = 123

class A:
    x = x
    pass

class B:
    x = 3.1415
    x = x
";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);

                analysis.Should().HaveClass("A")
                    .Which.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);

                // Arguably this should only be float, but since we don't support
                // definite assignment having both int and float is correct now.
                //
                // It also means we handle this case consistently:
                //
                // class B(object):
                //     if False:
                //         x = 3.1415
                //     x = x
                analysis.Should().HaveClass("B")
                    .Which.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TestFunctionAssignSameName() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var code = @"x = 123

def f():
    x = x
    return x

y = f()
";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfTypes(BuiltinTypeId.Int)
                    .And.HaveFunction("f")
                    .Which.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Int)
                    .And.HaveReturnValue().OfTypes(BuiltinTypeId.Int);
            }
        }

        /// <summary>
        /// Binary operators should assume their result type
        /// https://pytools.codeplex.com/workitem/1575
        /// 
        /// Slicing should assume the incoming type
        /// https://pytools.codeplex.com/workitem/1581
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task TestBuiltinOperatorsFallback() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var code = @"import array

slice = array.array('b', b'abcdef')[2:3]
add = array.array('b', b'abcdef') + array.array('b', b'fob')
";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("slice").OfType("array")
                    .And.HaveVariable("add").OfType("array");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ExcessPositionalArguments() {
            var code = @"def f(a, *args):
    return args[0]

x = f('abc', 1)
y = f(1, 'abc')
z = f(None, 'abc', 1)
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("z").OfTypes(BuiltinTypeId.Str, BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ExcessNamedArguments() {
            var code = @"def f(a, **args):
    return args[a]

x = f(a='b', b=1)
y = f(a='c', c=1.3)
z = f(a='b', b='abc')
w = f(a='p', p=1, q='abc')
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("z").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("w").OfTypes(BuiltinTypeId.Str, BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0), Timeout(5000)]
        public async Task RecursiveListComprehensionV32() {
            var code = @"
def f(x):
    x = []
    x = [i for i in x]
    x = (i for i in x)
    f(x)
";


            // If we complete processing then we have succeeded
            using (var server = await CreateServerAsync(PythonVersions.Required_Python32X)) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
            }
        }

        //[TestMethod, Priority(2)]
        //[TestCategory("ExpectFail")]
        public async Task CartesianStarArgs() {
            // TODO: Figure out whether this is useful behaviour
            // It currently does not work because we no longer treat
            // the dict created by **args as a lasting object - it
            // exists solely for receiving arguments.

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var code = @"def f(a, **args):
    args['fob'] = a
    return args['fob']


x = f(42)
y = f('abc')";

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Str);


                code = @"def f(a, **args):
    for i in xrange(2):
        if i == 1:
            return args['fob']
        else:
            args['fob'] = a

x = f(42)
y = f('abc')";

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CartesianRecursive() {
            var code = @"def f(a, *args):
    f(a, args)
    return a


x = f(42)";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CartesianSimple() {
            var code = @"def f(a):
    return a


x = f(42)
y = f('fob')";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
            }
        }


        [TestMethod, Priority(0)]
        public async Task CartesianLocals() {
            var code = @"def f(a):
    b = a
    return b


x = f(42)
y = f('fob')";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CartesianClosures() {
            var code = @"def f(a):
    def g():
        return a
    return g()


x = f(42)
y = f('fob')";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CartesianContainerFactory() {
            var code = @"def list_fact(ctor):
    x = []
    for abc in xrange(10):
        x.append(ctor(abc))
    return x


a = list_fact(int)[0]
b = list_fact(str)[0]
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CartesianLocalsIsInstance() {
            var code = @"def f(a, c):
    if isinstance(c, int):
        b = a
        return b
    else:
        b = a
        return b


x = f(42, 'oar')
y = f('fob', 'oar')";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
            }
        }

        //        [TestMethod, Priority(0)]
        //        public void CartesianMerge() {
        //            var limits = GetLimits();
        //            // Ensure we include enough calls
        //            var callCount = limits.CallDepth * limits.DecreaseCallDepth + 1;
        //            var code = new StringBuilder(@"def f(a):
        //    return g(a)

        //def g(b):
        //    return h(b)

        //def h(c):
        //    return c

        //");
        //            for (int i = 0; i < callCount; ++i) {
        //                code.AppendLine("x = g(123)");
        //            }
        //            code.AppendLine("y = f(3.1415)");

        //            var text = code.ToString();
        //            Console.WriteLine(text);
        //            var entry = ProcessTextV2(text);

        //            entry.AssertIsInstance("x", BuiltinTypeId.Int, BuiltinTypeId.Float);
        //            entry.AssertIsInstance("y", BuiltinTypeId.Int, BuiltinTypeId.Float);
        //        }

        [TestMethod, Priority(0)]
        public async Task ImportAs() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"import sys as s, array as a");

                analysis.Should().HavePythonModuleVariable("s")
                    .Which.Should().HaveMember<AstPythonStringLiteral>("winver");

                analysis.Should().HavePythonModuleVariable("a")
                    .Which.Should().HaveMember<AstPythonConstant>("ArrayType");

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"import sys as s");

                analysis.Should().HavePythonModuleVariable("s")
                    .Which.Should().HaveMember<AstPythonStringLiteral>("winver");
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryKeyValues() {
            var code = @"x = {'abc': 42, 'oar': 'baz'}

i = x['abc']
s = x['oar']
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("s").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task RecursiveLists() {
            var code = @"x = []
x.append(x)

y = []
y.append(y)

def f(a):
    return a[0]

x2 = f(x)
y2 = f(y)
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("x2").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("y2").OfType(BuiltinTypeId.List);
            }
        }

        [TestMethod, Priority(0)]
        public async Task RecursiveDictionaryKeyValues() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetTempPathUri("test-module.py");
                var code = @"x = {'abc': 42, 'oar': 'baz'}
x['abc'] = x
x[x] = 'abc'

i = x['abc']
s = x['abc']['abc']['abc']['oar']
t = x[x]
";

                await server.SendDidOpenTextDocument(uri, code);
                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("i").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Dict)
                    .And.HaveVariable("s").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("t").OfType(BuiltinTypeId.Str);

                code = @"x = {'y': None, 'value': 123 }
y = { 'x': x, 'value': 'abc' }
x['y'] = y

i = x['y']['x']['value']
s = y['x']['y']['value']
";
                server.SendDidChangeTextDocument(uri, code);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("i").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("s").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/50")]
        public async Task RecursiveTuples() {
            var code = @"class A(object):
    def __init__(self):
        self.top = None

    def fn(self, x, y):
        top = self.top
        if x > y:
            self.fn(y, x)
            return
        self.top = x, y, top

    def pop(self):
        self.top = self.top[2]

    def original(self, item=None):
        if item == None:
            item = self.top
        if item[2] != None:
            self.original(item[2])

        x, y, _ = item

a=A()
a.fn(1, 2)
a.fn(3, 4)
a.fn(5, 6)
a.fn(7, 8)
a.fn(9, 10)
a.fn(11, 12)
a.fn(13, 14)
x1, y1, _1 = a.top
a.original()
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x1").OfResolvedType(BuiltinTypeId.Int)
                    .And.HaveVariable("y1").OfResolvedType(BuiltinTypeId.Int)
                    .And.HaveVariable("_1").OfResolvedTypes(BuiltinTypeId.Tuple, BuiltinTypeId.NoneType)
                    .And.HaveClass("A").WithFunction("original")
                    .Which.Should().HaveVariable("item").OfResolvedTypes(BuiltinTypeId.Tuple, BuiltinTypeId.NoneType)
                    .And.HaveVariable("x").OfResolvedType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfResolvedType(BuiltinTypeId.Int)
                    .And.HaveVariable("_").OfResolvedTypes(BuiltinTypeId.Tuple, BuiltinTypeId.NoneType)
                    .And.HaveParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfTypes("top", BuiltinTypeId.Tuple, BuiltinTypeId.NoneType);
            }
        }

        [TestMethod, Priority(0)]
        public async Task RecursiveSequences() {
            var code = @"
x = []
x.append(x)
x.append(1)
x.append(3.14)
x.append('abc')
x.append(x)
y = x[0]
";
            // Completing analysis is the main test, but we'll also ensure that
            // the right types are in the list.
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("y").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Int, BuiltinTypeId.Float, BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CombinedTupleSignatures() {
            var code = @"def a():
    if x:
        return (1, True)
    elif y:
        return (1, True)
    else:
        return (2, False)

x = a()
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("a")
                    .Which.Should().HaveReturnValue().OfType(BuiltinTypeId.Tuple);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportStar() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
from nt import *
            ");
                analysis.Should().HaveVariable("abort");

                // make sure abort hasn't become a builtin, if so this test needs to be updated
                // with a new name
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"");
                analysis.Should().NotHaveVariable("abort");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportTrailingComma() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
import nt,
            ");
                analysis.Should().HavePythonModuleVariable("nt")
                    .Which.Should().HaveMembers("abort");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportStarCorrectRefs() {
            var text1 = @"
from mod2 import *

a = D()
";
            var text2 = @"
class D(object):
    pass
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri1 = TestData.GetTempPathUri("mod1.py");
                var uri2 = TestData.GetTempPathUri("mod2.py");
                await server.SendDidOpenTextDocument(uri1, text1);
                await server.SendDidOpenTextDocument(uri2, text2);

                await server.GetAnalysisAsync(uri1);
                await server.GetAnalysisAsync(uri2);
                var references = await server.SendFindReferences(uri2, 1, 7);

                references.Should().OnlyHaveReferences(
                    (uri1, (3, 4, 3, 5), ReferenceKind.Reference),
                    (uri2, (1, 0, 2, 8), ReferenceKind.Value),
                    (uri2, (1, 6, 1, 7), ReferenceKind.Definition)
                );
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/47")]
        public async Task MutatingReferences() {
            var text1 = @"
import mod2

class C(object):
    def SomeMethod(self):
        pass

mod2.D(C())
";

            var text2 = @"
class D(object):
    def __init__(self, value):
        self.value = value
        self.value.SomeMethod()
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri1 = TestData.GetNextModuleUri();
                var uri2 = TestData.GetNextModuleUri();
                await server.SendDidOpenTextDocument(uri1, text1);
                await server.SendDidOpenTextDocument(uri2, text2);

                await server.GetAnalysisAsync(uri1);
                await server.GetAnalysisAsync(uri2);
                var references = await server.SendFindReferences(uri1, 4, 9);

                references.Should().OnlyHaveReferences(
                    (uri1, (4, 4, 5, 12), ReferenceKind.Value),
                    (uri1, (4, 8, 4, 18), ReferenceKind.Definition),
                    (uri2, (4, 19, 4, 29), ReferenceKind.Reference)
                );

                text1 = text1.Substring(0, text1.IndexOf("    def")) + Environment.NewLine + text1.Substring(text1.IndexOf("    def"));
                server.SendDidChangeTextDocument(uri1, text1);
                await server.GetAnalysisAsync(uri1);

                references = await server.SendFindReferences(uri1, 5, 9);

                references.Should().OnlyHaveReferences(
                    (uri1, (5, 4, 6, 12), ReferenceKind.Value),
                    (uri1, (5, 8, 5, 18), ReferenceKind.Definition),
                    (uri2, (4, 19, 4, 29), ReferenceKind.Reference)
                );

                text2 = Environment.NewLine + text2;

                server.SendDidChangeTextDocument(uri2, text2);
                await server.GetAnalysisAsync(uri2);

                references = await server.SendFindReferences(uri1, 5, 9);

                references.Should().OnlyHaveReferences(
                    (uri1, (5, 4, 6, 12), ReferenceKind.Value),
                    (uri1, (5, 8, 5, 18), ReferenceKind.Definition),
                    (uri2, (5, 19, 5, 29), ReferenceKind.Reference)
                );
            }
        }


        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/46")]
        public async Task MutatingCalls() {
            var text1 = @"
def f(abc):
    return abc
";

            var text2 = @"
import mod1
z = mod1.f(42)
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri1 = TestData.GetTempPathUri("mod1.py");
                var uri2 = TestData.GetTempPathUri("mod2.py");
                await server.SendDidOpenTextDocument(uri1, text1);
                await server.SendDidOpenTextDocument(uri2, text2);

                var analysis1 = await server.GetAnalysisAsync(uri1);
                var analysis2 = await server.GetAnalysisAsync(uri2);

                analysis1.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("abc").OfType(BuiltinTypeId.Int);
                analysis2.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);

                //change caller in text2
                text2 = @"
import mod1
z = mod1.f('abc')
";

                server.SendDidChangeTextDocument(uri2, text2);
                analysis2 = await server.GetAnalysisAsync(uri2);

                analysis1.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("abc").OfType(BuiltinTypeId.Str);
                analysis2.Should().HaveVariable("z").OfType(BuiltinTypeId.Str);
            }
        }

        /* Doesn't pass, we don't have a way to clear the assignments across modules...
        [TestMethod, Priority(0)]
        public void MutatingVariables() {
            using (var state = PythonAnalyzer.CreateSynchronously(InterpreterFactory, Interpreter)) {

                var text1 = @"
print(x)
";

                var text2 = @"
import mod1
mod1.x = x
";


                var text3 = @"
import mod2
mod2.x = 42
";
                var mod1 = state.AddModule("mod1", "mod1", null);
                Prepare(mod1, GetSourceUnit(text1, "mod1"));
                var mod2 = state.AddModule("mod2", "mod2", null);
                Prepare(mod2, GetSourceUnit(text2, "mod2"));
                var mod3 = state.AddModule("mod3", "mod3", null);
                Prepare(mod3, GetSourceUnit(text3, "mod3"));

                mod3.Analyze(CancellationToken.None);
                mod2.Analyze(CancellationToken.None);
                mod1.Analyze(CancellationToken.None);

                state.AnalyzeQueuedEntries(CancellationToken.None);

                AssertUtil.ContainsExactly(
                    mod1.Analysis.GetDescriptionsByIndex("x", text1.IndexOf("x")),
                    "int"
                );
                
                text3 = @"
import mod2
mod2.x = 'abc'
";

                Prepare(mod3, GetSourceUnit(text3, "mod3"));
                mod3.Analyze(CancellationToken.None);
                state.AnalyzeQueuedEntries(CancellationToken.None);
                state.AnalyzeQueuedEntries(CancellationToken.None);

                AssertUtil.ContainsExactly(
                    mod1.Analysis.GetDescriptionsByIndex("x", text1.IndexOf("x")),
                    "str"
                );
            }
        }
        */

        [TestMethod, Priority(0)]
        public async Task PrivateMembers() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetTempPathUri("test-module.py");

                string code = @"
class C:
    def __init__(self):
        self._C__X = 'abc'	# Completions here should only ever show __X
        self.__X = 42

class D(C):
    def __init__(self):        
        print(self.__X)		# self. here shouldn't have __X or _C__X (could be controlled by Text Editor->Python->general->Hide advanced members to show _C__X)
";

                await server.SendDidOpenTextDocument(uri, code);
                await server.GetAnalysisAsync(uri);

                var completions = await server.SendCompletion(uri, 4, 13);
                completions.Should().OnlyHaveLabels("__X", "__init__", "__doc__", "__class__");

                completions = await server.SendCompletion(uri, 8, 19);
                completions.Should().OnlyHaveLabels("_C__X", "__init__", "__doc__", "__class__");

                    code = @"
class C(object):
    def __init__(self):
        self.f(_C__A = 42)		# sig help should be _C__A
    
    def f(self, __A):
        pass


class D(C):
    def __init__(self):
        self.f(_C__A=42)		# sig help should be _C__A
";

                server.SendDidChangeTextDocument(uri, code);
                await server.GetAnalysisAsync(uri);

                var signatures = await server.SendSignatureHelp(uri, 3, 15);
                signatures.Should().HaveSingleSignature()
                    .Which.Should().OnlyHaveParameterLabels("_C__A");

                signatures = await server.SendSignatureHelp(uri, 11, 15);
                signatures.Should().HaveSingleSignature()
                    .Which.Should().OnlyHaveParameterLabels("_C__A");

                code = @"
class C(object):
    def __init__(self):
        self.__f(_C__A = 42)		# member should be __f

    def __f(self, __A):
        pass


class D(C):
    def __init__(self):
        self._C__f(_C__A=42)		# member should be _C__f

";
                
                server.SendDidChangeTextDocument(uri, code);
                await server.GetAnalysisAsync(uri);

                completions = await server.SendCompletion(uri, 3, 13);
                completions.Should().HaveLabels("__f", "__init__");

                completions = await server.SendCompletion(uri, 11, 13);
                completions.Should().HaveLabels("_C__f", "__init__");

                code = @"
class C(object):
    __FOB = 42

    def f(self):
        abc = C.__FOB  # Completion should work here


xyz = C._C__FOB  # Advanced members completion should work here
";

                server.SendDidChangeTextDocument(uri, code);
                await server.GetAnalysisAsync(uri);

                completions = await server.SendCompletion(uri, 5, 16);
                completions.Should().HaveLabels("__FOB", "f");

                completions = await server.SendCompletion(uri, 8, 8);
                completions.Should().HaveLabels("_C__FOB", "f");
            }
        }

        [TestMethod, Priority(0)]
        public async Task BaseInstanceVariable() {
            var code = @"
class C:
    def __init__(self):
        self.abc = 42


class D(C):
    def __init__(self):        
        self.fob = self.abc
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveClass("D").WithFunction("__init__")
                    .Which.Should().HaveParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfType("fob", BuiltinTypeId.Int)
                    .And.HaveMemberOfType("abc", BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Mro() {
            var uri = TestData.GetTempPathUri("test-module.py");
            string code;
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                // Successful: MRO is A B C D E F object
                code = @"
O = object
class F(O): pass
class E(O): pass
class D(O): pass
class C(D,F): pass
class B(D,E): pass
class A(B,C): pass

a = A()
";

                await server.SendDidOpenTextDocument(uri, code);
                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveClassInfo("A")
                    .WithMethodResolutionOrder("A", "B", "C", "D", "E", "F", "type object");

                // Unsuccessful: cannot order X and Y
                code = @"
O = object
class X(O): pass
class Y(O): pass
class A(X, Y): pass
class B(Y, X): pass
class C(A, B): pass

c = C()
";

                server.SendDidChangeTextDocument(uri, code);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveClassInfo("C")
                    .Which.Should().HaveInvalidMethodResolutionOrder();

                // Unsuccessful: cannot order F and E
                code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(F,E): pass
G.remember2buy
";

                server.SendDidChangeTextDocument(uri, code);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveClassInfo("G")
                    .Which.Should().HaveInvalidMethodResolutionOrder();


                // Successful: exchanging bases of G fixes the ordering issue
                code = @"
class F(object): remember2buy='spam'
class E(F): remember2buy='eggs'
class G(E,F): pass
G.remember2buy
";

                server.SendDidChangeTextDocument(uri, code);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveClassInfo("G")
                    .WithMethodResolutionOrder("G", "E", "F", "type object");

                // Successful: MRO is Z K1 K2 K3 D A B C E object
                code = @"
class A(object): pass
class B(object): pass
class C(object): pass
class D(object): pass
class E(object): pass
class K1(A,B,C): pass
class K2(D,B,E): pass
class K3(D,A):   pass
class Z(K1,K2,K3): pass
z = Z()
";

                server.SendDidChangeTextDocument(uri, code);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveClassInfo("Z")
                    .WithMethodResolutionOrder("Z", "K1", "K2", "K3", "D", "A", "B", "C", "E", "type object");

                // Successful: MRO is Z K1 K2 K3 D A B C E object
                code = @"
class A(int): pass
class B(float): pass
class C(str): pass
z = None
";

                server.SendDidChangeTextDocument(uri, code);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveClassInfo("A").WithMethodResolutionOrder("A", "type int", "type object")
                    .And.HaveClassInfo("B").WithMethodResolutionOrder("B", "type float", "type object")
                    .And.HaveClassInfo("C").WithMethodResolutionOrder("C", "type str", "type basestring", "type object");
            }

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.SendDidOpenTextDocument(uri, code);
                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveClassInfo("A").WithMethodResolutionOrder("A", "type int", "type object")
                    .And.HaveClassInfo("B").WithMethodResolutionOrder("B", "type float", "type object")
                    .And.HaveClassInfo("C").WithMethodResolutionOrder("C", "type str", "type object");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportStarMro() {
            await PermutedTestAsync(
                "mod",
                new[] {
                    @"
class Test_test1(object):
    def test_A(self):
        pass
",
 @"from mod1 import *

class Test_test2(Test_test1):
    def test_newtest(self):pass"
                },
                analysis => {
                    analysis[1].Should().HaveClassInfo("Test_test2")
                        .WithMethodResolutionOrder("Test_test2", "Test_test1", "type object");
                }
            );
        }

        [TestMethod, Priority(0)]
        public async Task Iterator_Python27() {
            var uri = TestData.GetTempPathUri("test-module.py");
            using (var server = await CreateServerAsync(PythonVersions.Required_Python27X)) {
                await server.SendDidOpenTextDocument(uri, @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = iter(A)
iB = iter(B)
iC = iter(C)
");
                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("iA").OfType(BuiltinTypeId.ListIterator)
                    .And.HaveVariable("B").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("iB").OfType(BuiltinTypeId.StrIterator)
                    .And.HaveVariable("iC").OfType(BuiltinTypeId.ListIterator);

                server.SendDidChangeTextDocument(uri, @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA = A.__iter__()
iB = B.__iter__()
iC = C.__iter__()
");
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("iA").OfType(BuiltinTypeId.ListIterator)
                    .And.HaveVariable("B").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("iB").OfType(BuiltinTypeId.StrIterator)
                    .And.HaveVariable("iC").OfType(BuiltinTypeId.ListIterator);

                server.SendDidChangeTextDocument(uri, @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA, iB, iC = A.__iter__(), B.__iter__(), C.__iter__()
a = iA.next()
b = next(iB)
_next = next
c = _next(iC)
");

                analysis = await server.GetAnalysisAsync(uri);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("c").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Float);

                server.SendDidChangeTextDocument(uri, @"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
");

                analysis = await server.GetAnalysisAsync(uri);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Iterator_Python3X() {
            var uri = TestData.GetTempPathUri("test-module.py");
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.SendDidOpenTextDocument(uri, @"
A = [1, 2, 3]
B = 'abc'
C = [1.0, 'a', 3]

iA, iB, iC = A.__iter__(), B.__iter__(), C.__iter__()
a = iA.__next__()
b = next(iB)
_next = next
c = _next(iC)
");

                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("c").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Unicode, BuiltinTypeId.Float);

                server.SendDidChangeTextDocument(uri, @"
iA = iter(lambda: 1, 2)
iB = iter(lambda: 'abc', None)
iC = iter(lambda: 1, 'abc')

a = next(iA)
b = next(iB)
c = next(iC)
");
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Generator2X() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.next()

for c in f():
    print c
d = a.__next__()
            ");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").WithNoTypes();

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def f(x):
    yield x

a1 = f(42)
b1 = a1.next()
a2 = f('abc')
b2 = a2.next()

for c in f():
    print c
d = a1.__next__()
            ");

                analysis.Should().HaveVariable("a1").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b1").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("a2").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b2").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("c").WithNoTypes()
                    .And.HaveVariable("d").WithNoTypes();

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def f():
    yield 1
    x = yield 2

a = f()
b = a.next()
c = a.send('abc')
d = a.__next__()");
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").WithNoTypes();
            }
        }

        [TestMethod, Priority(0)]
        public async Task Generator3X() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
def f():
    yield 1
    yield 2
    yield 3

a = f()
b = a.__next__()

for c in f():
    print(c)

d = a.next()");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").WithNoTypes();

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def f(x):
    yield x

a1 = f(42)
b1 = a1.__next__()
a2 = f('abc')
b2 = a2.__next__()

for c in f(42):
    print(c)
d = a1.next()");

                analysis.Should().HaveVariable("a1").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b1").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("a2").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b2").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").WithNoTypes();

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def f():
    yield 1
    x = yield 2

a = f()
b = a.__next__()
c = a.send('abc')
d = a.next()");
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").WithNoTypes()
                    .And.HaveFunction("f")
                    .Which.Should().HaveVariable("x").WithMergedType(BuiltinTypeId.Unicode);
            }
        }

        [TestMethod, Priority(0)]
        public async Task GeneratorDelegation() {
            //            var text = @"
            //def f():
            //    yield 1
            //    yield 2
            //    yield 3
            //    return 3.14

            //def g():
            //    x = yield from f()

            //a = g()
            //a2 = iter(a)
            //b = next(a)

            //for c in g():
            //    print(c)
            //";
            //            var entry = ProcessTextV3(text);

            //            entry.AssertIsInstance("a", BuiltinTypeId.Generator);
            //            entry.AssertIsInstance("a2", BuiltinTypeId.Generator);
            //            entry.AssertIsInstance("b", BuiltinTypeId.Int);
            //            entry.AssertIsInstance("c", BuiltinTypeId.Int);
            //            entry.AssertIsInstance("x", text.IndexOf("x ="), BuiltinTypeId.Float);

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
def f(x):
    yield from x

a = f([42, 1337])
b = a.__next__()

#for c in f([42, 1337]):
#    print(c)
");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int);
                    //.And.HaveVariable("c").OfType(BuiltinTypeId.Int);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def g():
    yield 1
    x = yield 2

def f(fn):
    yield from fn()

a = f(g)
b = a.__next__()
c = a.send('abc')
");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveFunction("g")
                    .Which.Should().HaveVariable("x").WithMergedType(BuiltinTypeId.Str);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def g():
    yield 1
    return 'abc'

def f(fn):
    x = yield from fn()

a = f(g)
b = a.__next__()
");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveFunction("f")
                    .Which.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def g():
    yield 1
    return 'abc', 1.5

def h(fn):
    return (yield from fn())

def f(fn):
    x, y = yield from h(fn)

a = f(g)
b = next(a)
");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveFunction("f")
                    .Which.Should().HaveVariable("x").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Float);
            }
        }


        [TestMethod, Priority(0)]
        public async Task ListComprehensions() {
//            var entry = ProcessText(@"
//x = [2,3,4]
//y = [a for a in x]
//z = y[0]
//            ");

//            AssertUtil.ContainsExactly(entry.GetTypesFromName("z", 0), IntType);

            string text = @"
def f(abc):
    print abc

[f(x) for x in [2,3,4]]
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 4, 2);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 2, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );
            }
        }

        [DataRow(PythonLanguageVersion.V27)]
        [DataRow(PythonLanguageVersion.V31)]
        [DataRow(PythonLanguageVersion.V33)]
        [DataTestMethod, Priority(0)]
        public async Task LambdaInComprehension(PythonLanguageVersion version) {
            var text = "x = [(lambda a:[a**i for i in range(a+1)])(j) for j in range(5)]";

            using (var server = await CreateServerAsync(PythonVersions.GetRequiredCPythonConfiguration(version))) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Comprehensions() {
            var text = @"
x = 10; g = (i for i in range(x)); x = 5
x = 10; t = False; g = ((i,j) for i in range(x) if t for j in range(x))
x = 5; t = True;
[(i,j) for i in range(10) for j in range(5)]
[ x for x in range(10) if x % 2 if x % 3 ]
list(x for x in range(10) if x % 2 if x % 3)
[x for x, in [(4,), (5,), (6,)]]
list(x for x, in [(7,), (8,), (9,)])
";
            using (var server = await CreateServerAsync(PythonVersions.Required_Python27X)) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
            }

            using (var server = await CreateServerAsync(PythonVersions.Required_Python32X)) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ExecReferences() {
            string text = @"
a = {}
b = """"
exec b in a
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var referencesA = await server.SendFindReferences(uri, 1, 1);
                var referencesB = await server.SendFindReferences(uri, 2, 1);

                referencesA.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 1), ReferenceKind.Definition),
                    (uri, (3, 10, 3, 11), ReferenceKind.Reference)
                );

                referencesB.Should().OnlyHaveReferences(
                    (uri, (2, 0, 2, 1), ReferenceKind.Definition),
                    (uri, (3, 5, 3, 6), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task PrivateMemberReferences() {
            var text = @"
class C:
    def __x(self):
        pass

    def y(self):
        self.__x()

    def g(self):
        self._C__x()
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                var references = await server.SendFindReferences(uri, 6, 14);

                references.Should().OnlyHaveReferences(
                    (uri, (2, 4, 3, 12), ReferenceKind.Value),
                    (uri, (2, 8, 2, 11), ReferenceKind.Definition),
                    (uri, (6, 13, 6, 16), ReferenceKind.Reference),
                    (uri, (9, 13, 9, 18), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task GeneratorComprehensions() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var text = @"
x = [2,3,4]
y = (a for a in x)
for z in y:
    print z
";

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("z").OfResolvedType(BuiltinTypeId.Int);

                text = @"
x = [2,3,4]
y = (a for a in x)

def f(iterable):
    for z in iterable:
        print z

f(y)
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveVariable("z").OfResolvedType(BuiltinTypeId.Int);

                text = @"
x = [True, False, None]

def f(iterable):
    result = None
    for i in iterable:
        if i:
            result = i
    return result

y = f(i for i in x)
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("y").OfTypes(BuiltinTypeId.Bool, BuiltinTypeId.NoneType);

                text = @"
def f(abc):
    print abc

(f(x) for x in [2,3,4])
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(text);
                var uri = TestData.GetDefaultModuleUri();
                var references = await server.SendFindReferences(uri, 1, 5);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("abc").OfType(BuiltinTypeId.Int);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 2, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );

            }
        }

        [TestMethod, Priority(0)]
        public async Task ForSequence() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = [('abc', 42, True), ('abc', 23, False),]
for some_str, some_int, some_bool in x:
    print some_str
    print some_int
    print some_bool
");
                analysis.Should().HaveVariable("some_str").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("some_int").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("some_bool").OfType(BuiltinTypeId.Bool);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ForIterator() {
            using (var server = await CreateServerAsync(PythonVersions.Required_Python34X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class X(object):
    def __iter__(self): return self
    def __next__(self): return 123

class Y(object):
    def __iter__(self): return X()

for i in Y():
    pass
");
                analysis.Should().HaveVariable("i").OfResolvedType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DynamicAttributes() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class x(object):
    def __getattr__(self, name):
        return 42
    def f(self): 
        return 'abc'
        
a = x().abc
b = x().f()

class y(object):
    def __getattribute__(self, x):
        return 'abc'
        
c = y().abc
");
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task GetAttr() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class x(object):
    def __init__(self, value):
        self.value = value
        
a = x(42)
b = getattr(a, 'value')
c = getattr(a, 'dne', 'fob')
d = getattr(a, 'value', 'fob')
");
                analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("d").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task SetAttr() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class X(object):
    pass
x = X()

setattr(x, 'a', 123)
object.__setattr__(x, 'b', 3.1415)

a = x.a
b = x.b
");
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task NoGetAttrForSlots() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"class A(object):
    def __getattr__(self, key):
        return f

def f(x, y):
    x # should be unknown
    y # should be int

a = A()
a(123, None)
a.__call__(None, 123)
");
                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("x").OfType(BuiltinTypeId.NoneType)
                    .And.HaveParameter("y").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task VarsSpecialization() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = vars()
k = x.keys()[0]
v = x['a']
");

                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Dict)
                    .And.HaveVariable("k").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("v").OfType(BuiltinTypeId.Object);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DirSpecialization() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = dir()
v = x[0]
");
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("v").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinSpecializations() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
expect_int = abs(1)
expect_float = abs(2.3)
expect_object = abs(object())
expect_str = abs('')

expect_bool = all()
expect_bool = any()
expect_str = ascii()
expect_str = bin()
expect_bool = callable()
expect_str = chr()
expect_list = dir()
expect_str = dir()[0]
expect_object = eval()
expect_str = format()
expect_dict = globals()
expect_object = globals()['']
expect_bool = hasattr()
expect_int = hash()
expect_str = hex()
expect_int = id()
expect_bool = isinstance()
expect_bool = issubclass()
expect_int = len()
expect_dict = locals()
expect_object = locals()['']
expect_str = oct()
expect_TextIOWrapper = open('')
expect_BufferedIOBase = open('', 'b')
expect_int = ord()
expect_int = pow(1, 1)
expect_float = pow(1.0, 1.0)
expect_str = repr()
expect_int = round(1)
expect_float = round(1.1)
expect_float = round(1, 1)
expect_list = sorted([0, 1, 2])
expect_int = sum(1, 2)
expect_float = sum(2.0, 3.0)
expect_dict = vars()
expect_object = vars()['']
");
                analysis.Should().HaveVariable("expect_object").OfType(BuiltinTypeId.Object)
                    .And.HaveVariable("expect_bool").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("expect_int").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("expect_float").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("expect_str").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("expect_list").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("expect_dict").OfType(BuiltinTypeId.Dict);


                analysis.Should().HaveVariable("expect_TextIOWrapper").WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveClassName("TextIOWrapper");
                analysis.Should().HaveVariable("expect_BufferedIOBase").WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveClassName("BufferedIOBase");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ListAppend() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = []
x.append('abc')
y = x[0]
");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Str);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
x = []
x.extend(('abc', ))
y = x[0]
");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Str);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
x = []
x.insert(0, 'abc')
y = x[0]
");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Str);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
x = []
x.append('abc')
y = x.pop()
");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Str);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
class ListTest(object):
    def reset(self):
        self.items = []
        self.pushItem(self)
    def pushItem(self, item):
        self.items.append(item)

a = ListTest().items
b = a[0]");
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("b").OfResolvedType("ListTest");
            }
        }

        [TestMethod, Priority(0)]
        public async Task Slicing() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = [2]
y = x[:-1]
z = y[0]
");
                analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
x = (2, 3, 4)
y = x[:-1]
z = y[0]
");
                analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
lit = 'abc'
inst = str.lower()

slit = lit[1:2]
ilit = lit[1]
sinst = inst[1:2]
iinst = inst[1]
");
                analysis.Should().HaveVariable("slit").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("ilit").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("sinst").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("iinst").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ConstantIndex() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
ZERO = 0
ONE = 1
TWO = 2
x = ['abc', 42, True]


some_str = x[ZERO]
some_int = x[ONE]
some_bool = x[TWO]
");
                analysis.Should().HaveVariable("some_str").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("some_int").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("some_bool").OfType(BuiltinTypeId.Bool);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CtorSignatures() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
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

            ");

                analysis.Should().HaveClassInfo("C")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters();

                analysis.Should().HaveClassInfo("D")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters();

                analysis.Should().HaveClassInfo("E")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters();

                analysis.Should().HaveClassInfo("F")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters("one");

                analysis.Should().HaveClassInfo("G")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters();

                analysis.Should().HaveClassInfo("H")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters("one");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ListSubclassSignatures() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.SendDidOpenTextDocument(uri, @"
class C(list):
    pass

a = C()
a.count(");
                var analysis = await server.GetAnalysisAsync(uri);
                var signatures = await server.SendSignatureHelp(uri, 5, 8);

                analysis.Should().HaveVariable("a").OfType("C");
                signatures.Should().HaveSingleSignature()
                    .Which.Should().OnlyHaveParameterLabels("x");
            }
        }


        [TestMethod, Priority(0)]
        public async Task DocStrings() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
def f():
    '''func doc'''


def funicode():
    u'''unicode func doc'''

class C:
    '''class doc'''

class CUnicode:
    u'''unicode class doc'''

class CNewStyle(object):
    '''new-style class doc'''

class CInherited(CNewStyle):
    pass

class CInit:
    '''class doc'''
    def __init__(self):
        '''init doc'''
        pass

class CUnicodeInit:
    u'''unicode class doc'''
    def __init__(self):
        u'''unicode init doc'''
        pass

class CNewStyleInit(object):
    '''new-style class doc'''
    def __init__(self):
        '''new-style init doc'''
        pass

class CInheritedInit(CNewStyleInit):
    pass
");

                analysis.Should().HaveFunctionInfo("f")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("func doc");

                analysis.Should().HaveClassInfo("C")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("class doc");

                analysis.Should().HaveFunctionInfo("funicode")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("unicode func doc");

                analysis.Should().HaveClassInfo("CUnicode")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("unicode class doc");

                analysis.Should().HaveClassInfo("CNewStyle")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("new-style class doc");

                analysis.Should().HaveClassInfo("CInherited")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("new-style class doc");

                analysis.Should().HaveClassInfo("CInit")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("init doc");

                analysis.Should().HaveClassInfo("CUnicodeInit")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("unicode init doc");

                analysis.Should().HaveClassInfo("CNewStyleInit")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("new-style init doc");

                analysis.Should().HaveClassInfo("CInheritedInit")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveDocumentation("new-style init doc");
            }
        }

        [TestMethod, Priority(0)]
        public async Task Ellipsis() {
            using (var server = await CreateServerAsync(PythonVersions.EarliestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = ...
            ");

                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Ellipsis);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Backquote() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"x = `42`");
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinMethodSignatures() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
const = """".capitalize
constructed = str().capitalize
");
                analysis.Should().HaveVariable("const").WithValue<BoundBuiltinMethodInfo>()
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveNoParameters();

                analysis.Should().HaveVariable("constructed").WithValue<BoundBuiltinMethodInfo>()
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveNoParameters();

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
const = [].append
constructed = list().append
");
                analysis.Should().HaveVariable("const").WithValue<SpecializedCallable>()
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters("value");

                analysis.Should().HaveVariable("constructed").WithValue<BoundBuiltinMethodInfo>()
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameters("value");
            }
        }

        [TestMethod, Priority(0)]
        public async Task Del() {
            using (var server = await CreateServerAsync()) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
del fob
del fob[2]
del fob.oar
del (fob)
del fob, oar
");
            }

            // We do no analysis on del statements, nothing to test
        }

        [TestMethod, Priority(0)]
        public async Task TryExcept() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class MyException(Exception): pass

def f():
    try:
        pass
    except TypeError, e1:
        pass

def g():
    try:
        pass
    except MyException, e2:
        pass
");
                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveVariable("e1").OfType("TypeError");

                analysis.Should().HaveFunction("g")
                    .Which.Should().HaveVariable("e2").OfType("MyException");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ConstantMath() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
e = 1 + 1L # e is a 'float', should be 'long' under v2.x (error under v3.x)
f = 1 / 2 # f is 'int', should be 'float' under v3.x");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("d").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("e").OfType(BuiltinTypeId.Long)
                    .And.HaveVariable("f").OfType(BuiltinTypeId.Int);
            }
            
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
f = 1 / 2 # f is 'int', should be 'float' under v3.x");

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("d").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("f").OfType(BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task StringConcatenation() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = u'abc'
y = x + u'dEf'


x1 = 'abc'
y1 = x1 + 'def'

fob = 'abc'.lower()
oar = fob + 'Def'

fob2 = u'ab' + u'cd'
oar2 = fob2 + u'ef'");

                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
            }
        }

        [TestMethod, Priority(0)]
        public async Task StringFormatting() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = u'abc %d'
y = x % (42, )


x1 = 'abc %d'
y1 = x1 % (42, )

fob = 'abc %d'.lower()
oar = fob % (42, )

fob2 = u'abc' + u'%d'
oar2 = fob2 % (42, )");

                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
            }
        }

        [TestMethod, Priority(0)]
        public async Task StringFormattingV36() {
            using (var server = await CreateServerAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
y = f'abc {42}'
ry = rf'abc {42}'
yr = fr'abc {42}'
fadd = f'abc{42}' + f'{42}'

def f(val):
    print(val)
f'abc {f(42)}'
");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("ry").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("yr").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("fadd").OfType(BuiltinTypeId.Str);
                // TODO: Enable analysis of f-strings
                //    .And.HaveVariable("val",  BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task StringMultiply() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = u'abc %d'
y = x * 100


x1 = 'abc %d'
y1 = x1 * 100

fob = 'abc %d'.lower()
oar = fob * 100

fob2 = u'abc' + u'%d'
oar2 = fob2 * 100");
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
            }
        }

        [TestMethod, Priority(0)]
        public async Task StringMultiply_2() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = u'abc %d'
y = 100 * x


x1 = 'abc %d'
y1 = 100 * x1

fob = 'abc %d'.lower()
oar = 100 * fob

fob2 = u'abc' + u'%d'
oar2 = 100 * fob2");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                    .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
            }
        }

        [TestMethod, Priority(0)]
        public async Task NotOperator() {
            var text = @"

class C(object):
    def __nonzero__(self):
        pass

    def __bool__(self):
        pass

a = not C()
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Bool);
            }

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Bool);
            }
        }
        
        [TestMethod, Priority(0)]
        public async Task UnaryOperatorPlus() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class Result(object):
    pass

class C(object):
    def __pos__(self):
        return Result()

a = +C()
b = ++C()
");
                analysis.Should().HaveVariable("a").OfTypes("Result")
                    .And.HaveVariable("b").OfTypes("Result");
            }
        }
        
        [TestMethod, Priority(0)]
        public async Task UnaryOperatorMinus() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class Result(object):
    pass

class C(object):
    def __neg__(self):
        return Result()

a = -C()
b = --C()
");
                analysis.Should().HaveVariable("a").OfTypes("Result")
                    .And.HaveVariable("b").OfTypes("Result");
            }
        }
        
        [TestMethod, Priority(0)]
        public async Task UnaryOperatorTilde() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class Result(object):
    pass

class C(object):
    def __invert__(self):
        return Result()

a = ~C()
b = ~~C()
");
                analysis.Should().HaveVariable("a").OfTypes("Result")
                    .And.HaveVariable("b").OfTypes("Result");
            }
        }


        [TestMethod, Priority(0)]
        public async Task TrueDividePython3X() {
            var text = @"
class C:
    def __truediv__(self, other):
        return 42
    def __rtruediv__(self, other):
        return 3.0

a = C()
b = a / 'abc'
c = 'abc' / a
";

            using (var server = await CreateServerAsync(PythonVersions.Required_Python35X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task BinaryOperators() {
            var operators = new[] {
                new { Method = "add", Operator = "+", Version = PythonVersions.Required_Python27X },
                new { Method = "sub", Operator = "-", Version = PythonVersions.Required_Python27X },
                new { Method = "mul", Operator = "*", Version = PythonVersions.Required_Python27X },
                new { Method = "div", Operator = "/", Version = PythonVersions.Required_Python27X },
                new { Method = "mod", Operator = "%", Version = PythonVersions.Required_Python27X },
                new { Method = "and", Operator = "&", Version = PythonVersions.Required_Python27X },
                new { Method = "or", Operator = "|", Version = PythonVersions.Required_Python27X },
                new { Method = "xor", Operator = "^", Version = PythonVersions.Required_Python27X },
                new { Method = "lshift", Operator = "<<", Version = PythonVersions.Required_Python27X },
                new { Method = "rshift", Operator = ">>", Version = PythonVersions.Required_Python27X },
                new { Method = "pow", Operator = "**", Version = PythonVersions.Required_Python27X },
                new { Method = "floordiv", Operator = "//", Version = PythonVersions.Required_Python27X },
                new { Method = "matmul", Operator = "@", Version = PythonVersions.Required_Python35X },
            };

            var text = @"
class ForwardResult(object):
    pass

class ReverseResult(object):
    pass

class C(object):
    def __{0}__(self, other):
        return ForwardResult()

    def __r{0}__(self, other):
        return ReverseResult()

a = C() {1} 42
b = 42 {1} C()
c = [] {1} C()
d = C() {1} []
e = () {1} C()
f = C() {1} ()
g = C() {1} 42.0
h = 42.0 {1} C()
i = C() {1} 42L
j = 42L {1} C()
k = C() {1} 42j
l = 42j {1} C()
m = C()
m {1}= m
";

            foreach (var test in operators) {
                Console.WriteLine(test.Operator);
                var code = string.Format(text, test.Method, test.Operator);
                if (test.Version.Version.ToLanguageVersion().Is3x()) {
                    code = code.Replace("42L", "42");
                }

                using (var server = await CreateServerAsync(test.Version)) {
                    var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                    analysis.Should().HaveVariable("a").OfType("ForwardResult")
                    .And.HaveVariable("b").OfType("ReverseResult")
                    .And.HaveVariable("c").OfType("ReverseResult")
                    .And.HaveVariable("d").OfType("ForwardResult")
                    .And.HaveVariable("e").OfType("ReverseResult")
                    .And.HaveVariable("f").OfType("ForwardResult")
                    .And.HaveVariable("g").OfType("ForwardResult")
                    .And.HaveVariable("h").OfType("ReverseResult")
                    .And.HaveVariable("i").OfType("ForwardResult")
                    .And.HaveVariable("j").OfType("ReverseResult")
                    .And.HaveVariable("k").OfType("ForwardResult")
                    .And.HaveVariable("l").OfType("ReverseResult")
                    // We assume that augmented assignments keep their type
                    .And.HaveVariable("m").OfType("C");
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task SequenceConcat() {
            var text = @"
x1 = ()
y1 = x1 + ()
y1v = y1[0]

x2 = (1,2,3)
y2 = x2 + (4.0,5.0,6.0)
y2v = y2[0]

x3 = [1,2,3]
y3 = x3 + [4.0,5.0,6.0]
y3v = y3[0]
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("x1").OfType(BuiltinTypeId.Tuple)
                    .And.HaveVariable("y1").OfType(BuiltinTypeId.Tuple)
                    .And.HaveVariable("y1v").WithNoTypes()
                    .And.HaveVariable("y2").OfType(BuiltinTypeId.Tuple)
                    .And.HaveVariable("y2v").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveVariable("y3").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("y3v").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task SequenceMultiply() {
            var text = @"
x = ()
y = x * 100

x1 = (1,2,3)
y1 = x1 * 100

fob = [1,2,3]
oar = fob * 100

fob2 = []
oar2 = fob2 * 100";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("y").WithDescription("tuple")
                    .And.HaveVariable("y1").WithDescription("tuple[int]")
                    .And.HaveVariable("oar").WithDescription("list[int]")
                    .And.HaveVariable("oar2").WithDescription("list");
            }
        }

        [TestMethod, Priority(0)]
        public async Task SequenceMultiply_2() {
            var text = @"
x = ()
y = 100 * x

x1 = (1,2,3)
y1 = 100 * x1

fob = [1,2,3]
oar = 100 * fob 

fob2 = []
oar2 = 100 * fob2";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("y").WithDescription("tuple")
                    .And.HaveVariable("y1").WithDescription("tuple[int]")
                    .And.HaveVariable("oar").WithDescription("list[int]")
                    .And.HaveVariable("oar2").WithDescription("list");
            }
        }

        [TestMethod, Priority(0)]
        public async Task SequenceContains() {
            var text = @"
a_tuple = ()
a_list = []
a_set = { 1 }
a_dict = {}
a_string = 'abc'

t1 = 100 in a_tuple
t2 = 100 not in a_tuple

l1 = 100 in a_list
l2 = 100 not in a_list

s1 = 100 in a_set
s2 = 100 not in a_set

d1 = 100 in a_dict
d2 = 100 not in a_dict

r1 = 100 in a_string
r2 = 100 not in a_string
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("t1").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("t2").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("l1").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("l2").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("s1").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("s2").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("d1").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("d2").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("r1").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("r2").OfType(BuiltinTypeId.Bool);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DescriptorNoDescriptor() {
            var text = @"
class NoDescriptor:
       def nodesc_method(self): pass

class SomeClass:
    fob = NoDescriptor()

    def f(self):
        self.fob
        pass
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveClass("SomeClass").WithFunction("f")
                    .Which.Should().HaveParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMember<IInstanceInfo>("fob")
                    .Which.Should().HaveMember<IBoundMethodInfo>("nodesc_method");
            }
        }

        /// <summary>
        /// Verifies that a line in triple quoted string which ends with a \ (eating the newline) doesn't throw
        /// off our newline tracking.
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task ReferencesTripleQuotedStringWithBackslash() {
            // instance variables
            var text = @"
'''this is a triple quoted string\
that ends with a backslash on a line\
and our line info should remain correct'''

# add ref w/o type info
class C(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var referencesAbc = await server.SendFindReferences(uri, 8, 15);
                var referencesFob = await server.SendFindReferences(uri, 8, 21);

                referencesAbc.Should().OnlyHaveReferences(
                    (uri, (8, 13, 8, 16), ReferenceKind.Definition),
                    (uri, (9, 17, 9, 20), ReferenceKind.Reference),
                    (uri, (10, 19, 10, 22), ReferenceKind.Reference)
                );
                referencesFob.Should().OnlyHaveReferences(
                    (uri, (7, 23, 7, 26), ReferenceKind.Definition),
                    (uri, (8, 19, 8, 22), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_InstanceVariables() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();

                var text = @"
# add ref w/o type info
class C(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

";

                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var referencesAbc = await server.SendFindReferences(uri, 4, 15);
                var referencesFob = await server.SendFindReferences(uri, 4, 21);

                referencesAbc.Should().OnlyHaveReferences(
                    (uri, (4, 13, 4, 16), ReferenceKind.Definition),
                    (uri, (5, 17, 5, 20), ReferenceKind.Reference),
                    (uri, (6, 19, 6, 22), ReferenceKind.Reference)
                );
                referencesFob.Should().OnlyHaveReferences(
                    (uri, (3, 23, 3, 26), ReferenceKind.Definition),
                    (uri, (4, 19, 4, 22), ReferenceKind.Reference)
                );

                text = @"
# add ref w/ type info
class D(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

D(42)";
                server.SendDidChangeTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                referencesAbc = await server.SendFindReferences(uri, 4, 15);
                referencesFob = await server.SendFindReferences(uri, 4, 21);
                var referencesD = await server.SendFindReferences(uri, 8, 1);

                referencesAbc.Should().OnlyHaveReferences(
                    (uri, (4, 13, 4, 16), ReferenceKind.Definition),
                    (uri, (5, 17, 5, 20), ReferenceKind.Reference),
                    (uri, (6, 19, 6, 22), ReferenceKind.Reference)
                );
                referencesFob.Should().OnlyHaveReferences(
                    (uri, (3, 23, 3, 26), ReferenceKind.Definition),
                    (uri, (4, 19, 4, 22), ReferenceKind.Reference)
                );
                referencesD.Should().OnlyHaveReferences(
                    (uri, (2, 6, 2, 7), ReferenceKind.Definition),
                    (uri, (2, 0, 6, 22), ReferenceKind.Value),
                    (uri, (8, 0, 8, 1), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_FunctionDefinitions() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(): pass

x = f()";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var referencesF = await server.SendFindReferences(uri, 3, 5);

                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );

                text = @"
def f(): pass

x = f";
                server.SendDidChangeTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                referencesF = await server.SendFindReferences(uri, 3, 5);

                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_ClassVariables() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"

class D(object):
    abc = 42
    print abc
    del abc
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 3, 5);

                references.Should().OnlyHaveReferences(
                    (uri, (3, 4, 3, 7), ReferenceKind.Definition),
                    (uri, (4, 10, 4, 13), ReferenceKind.Reference),
                    (uri, (5, 8, 5, 11), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_ClassDefinition() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
class D(object): pass

a = D
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 3, 5);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 21), ReferenceKind.Value),
                    (uri, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_MethodDefinition() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
class D(object): 
    def f(self): pass

a = D().f()
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 4, 9);

                references.Should().OnlyHaveReferences(
                    (uri, (2, 4, 2, 21), ReferenceKind.Value),
                    (uri, (2, 8, 2, 9), ReferenceKind.Definition),
                    (uri, (4, 8, 4, 9), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_Globals() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
abc = 42
print abc
del abc
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 2, 7);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 3), ReferenceKind.Definition),
                    (uri, (2, 6, 2, 9), ReferenceKind.Reference),
                    (uri, (3, 4, 3, 7), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_Parameters() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(abc):
    print abc
    abc = 42
    del abc
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 2, 11);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 7), ReferenceKind.Definition),
                    (uri, (2, 10, 2, 13), ReferenceKind.Reference),
                    (uri, (4, 8, 4, 11), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_NamedArguments() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(abc):
    print abc

f(abc = 123)
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 2, 11);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),
                    (uri, (2, 10, 2, 13), ReferenceKind.Reference),
                    (uri, (4, 2, 4, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/40")]
        public async Task References_GrammarTest_Statements() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(abc):
    try: pass
    except abc: pass

    try: pass
    except TypeError, abc: pass

    abc, oar = 42, 23
    abc[23] = 42
    abc.fob = 42
    abc += 2

    class D(abc): pass

    for x in abc: print x

    import abc
    from xyz import abc
    from xyz import oar as abc

    if abc: print 'hi'
    elif abc: print 'bye'
    else: abc

    with abc:
        return abc

    print abc
    assert abc, abc

    raise abc
    raise abc, abc, abc

    while abc:
        abc
    else:
        abc

    for x in fob: 
        print x
    else:
        print abc

    try: pass
    except TypeError: pass
    else:
        abc
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 3, 12);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),
                    (uri, (3, 11, 3, 14), ReferenceKind.Reference),
                    (uri, (6, 22, 6, 25), ReferenceKind.Definition),

                    (uri, (8, 4, 8, 7), ReferenceKind.Definition),
                    (uri, (9, 4, 9, 7), ReferenceKind.Reference),
                    (uri, (10, 4, 10, 7), ReferenceKind.Reference),
                    (uri, (11, 4, 11, 7), ReferenceKind.Reference),

                    (uri, (13, 12, 13, 15), ReferenceKind.Reference),

                    (uri, (15, 13, 15, 16), ReferenceKind.Reference),

                    (uri, (17, 11, 17, 14), ReferenceKind.Reference),
                    (uri, (18, 20, 18, 23), ReferenceKind.Reference),
                    (uri, (19, 27, 19, 30), ReferenceKind.Reference),

                    (uri, (21, 7, 21, 10), ReferenceKind.Reference),
                    (uri, (22, 9, 22, 12), ReferenceKind.Reference),
                    (uri, (23, 10, 23, 13), ReferenceKind.Reference),

                    (uri, (25, 9, 25, 12), ReferenceKind.Reference),
                    (uri, (26, 15, 26, 18), ReferenceKind.Reference),

                    (uri, (28, 10, 28, 13), ReferenceKind.Reference),
                    (uri, (29, 11, 29, 14), ReferenceKind.Reference),
                    (uri, (29, 16, 29, 19), ReferenceKind.Reference),

                    (uri, (31, 10, 31, 13), ReferenceKind.Reference),
                    (uri, (32, 15, 32, 18), ReferenceKind.Reference),
                    (uri, (32, 20, 32, 23), ReferenceKind.Reference),
                    (uri, (32, 10, 32, 13), ReferenceKind.Reference),

                    (uri, (34, 10, 34, 13), ReferenceKind.Reference),
                    (uri, (35, 8, 35, 11), ReferenceKind.Reference),
                    (uri, (37, 8, 37, 11), ReferenceKind.Reference),

                    (uri, (42, 14, 42, 17), ReferenceKind.Reference),

                    (uri, (47, 8, 47, 11), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task References_GrammarTest_Expressions() {
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var text = @"
def f(abc):
    x = abc + 2
    x = 2 + abc
    x = l[abc]
    x = abc[l]
    x = abc.fob
    
    g(abc)

    abc if abc else abc

    {abc:abc},
    [abc, abc]
    (abc, abc)
    {abc}

    yield abc
    [x for x in abc]
    (x for x in abc)

    abc or abc
    abc and abc

    +abc
    x[abc:abc:abc]

    abc == abc
    not abc

    lambda : abc
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 3, 12);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),

                    (uri, (2, 8, 2, 11), ReferenceKind.Reference),
                    (uri, (3, 12, 3, 15), ReferenceKind.Reference),

                    (uri, (4, 10, 4, 13), ReferenceKind.Reference),
                    (uri, (5, 8, 5, 11), ReferenceKind.Reference),
                    (uri, (6, 8, 6, 11), ReferenceKind.Reference),
                    (uri, (8, 6, 8, 9), ReferenceKind.Reference),

                    (uri, (10, 11, 10, 14), ReferenceKind.Reference),
                    (uri, (10, 4, 10, 7), ReferenceKind.Reference),
                    (uri, (10, 20, 10, 23), ReferenceKind.Reference),

                    (uri, (12, 5, 12, 8), ReferenceKind.Reference),
                    (uri, (12, 9, 12, 12), ReferenceKind.Reference),
                    (uri, (13, 5, 13, 8), ReferenceKind.Reference),
                    (uri, (13, 10, 13, 13), ReferenceKind.Reference),
                    (uri, (14, 5, 14, 8), ReferenceKind.Reference),
                    (uri, (14, 10, 14, 13), ReferenceKind.Reference),
                    (uri, (15, 5, 15, 8), ReferenceKind.Reference),

                    (uri, (17, 10, 17, 13), ReferenceKind.Reference),
                    (uri, (18, 16, 18, 19), ReferenceKind.Reference),
                    (uri, (21, 4, 21, 7), ReferenceKind.Reference),

                    (uri, (21, 11, 21, 14), ReferenceKind.Reference),
                    (uri, (22, 4, 22, 7), ReferenceKind.Reference),
                    (uri, (22, 12, 22, 15), ReferenceKind.Reference),
                    (uri, (24, 5, 24, 8), ReferenceKind.Reference),

                    (uri, (25, 6, 25, 9), ReferenceKind.Reference),
                    (uri, (25, 10, 25, 13), ReferenceKind.Reference),
                    (uri, (25, 14, 25, 17), ReferenceKind.Reference),
                    (uri, (27, 4, 27, 7), ReferenceKind.Reference),

                    (uri, (27, 11, 27, 14), ReferenceKind.Reference),
                    (uri, (28, 8, 28, 11), ReferenceKind.Reference),
                    (uri, (19, 16, 19, 19), ReferenceKind.Reference),

                    (uri, (30, 13, 30, 16), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/39")]
        public async Task References_Parameters_NestedFunction() {
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var text = @"
def f(a):
    def g():
        print(a)
        assert isinstance(a, int)
        a = 200
";
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);

                var references = await server.SendFindReferences(uri, 3, 15);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uri, (3, 14, 3, 15), ReferenceKind.Reference),
                    (uri, (4, 26, 4, 27), ReferenceKind.Reference),
                    (uri, (5, 8, 5, 9), ReferenceKind.Definition)
                );

                references = await server.SendFindReferences(uri, 5, 9);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uri, (3, 14, 3, 15), ReferenceKind.Reference),
                    (uri, (4, 26, 4, 27), ReferenceKind.Reference),
                    (uri, (5, 8, 5, 9), ReferenceKind.Definition)
                );
            }
        }
        
        [TestMethod, Priority(0)]
        public async Task ListDictArgReferences() {
            var text = @"
def f(*a, **k):
    x = a[1]
    y = k['a']

#out
a = 1
k = 2
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);

                var referencesA = await server.SendFindReferences(uri, 2, 8);
                var referencesK = await server.SendFindReferences(uri, 3, 8);
                referencesA.Should().OnlyHaveReferences(
                    (uri, (1, 7, 1, 8), ReferenceKind.Definition),
                    (uri, (2, 8, 2, 9), ReferenceKind.Reference)
                );
                referencesK.Should().OnlyHaveReferences(
                    (uri, (1, 12, 1, 13), ReferenceKind.Definition),
                    (uri, (3, 8, 3, 9), ReferenceKind.Reference)
                );

                referencesA = await server.SendFindReferences(uri, 6, 1);
                referencesK = await server.SendFindReferences(uri, 7, 1);
                referencesA.Should().OnlyHaveReference(uri, (6, 0, 6, 1), ReferenceKind.Definition);
                referencesK.Should().OnlyHaveReference(uri, (7, 0, 7, 1), ReferenceKind.Definition);
            }
        }

        [TestMethod, Priority(0)]
        public async Task KeywordArgReferences() {
            var text = @"
def f(a):
    pass

f(a=1)
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var references = await server.SendFindReferences(uri, 4, 3);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uri, (4, 2, 4, 3), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReferencesCrossModule() {
            var fobText = @"
from oar import abc

abc()
";
            var oarText = "class abc(object): pass";
            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);
                await server.GetAnalysisAsync(fobUri);
                await server.GetAnalysisAsync(oarUri);
                var references = await server.SendFindReferences(oarUri, 0, 7);

                references.Should().OnlyHaveReferences(
                    (oarUri, (0, 0, 0, 23), ReferenceKind.Value),
                    (oarUri, (0, 6, 0, 9), ReferenceKind.Definition),
                    (fobUri, (1, 16, 1, 19), ReferenceKind.Reference),
                    (fobUri, (3, 0, 3, 3), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task SuperclassMemberReferencesCrossModule() {
            // https://github.com/Microsoft/PTVS/issues/2271

            var fobText = @"
from oar import abc

class bcd(abc):
    def test(self):
        self.x
";
            var oarText = @"class abc(object):
    def __init__(self):
        self.x = 123
";

            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);
                await server.GetAnalysisAsync(fobUri);
                await server.GetAnalysisAsync(oarUri);
                var references = await server.SendFindReferences(oarUri, 2, 14);

                references.Should().OnlyHaveReferences(
                    (oarUri, (2, 13, 2, 14), ReferenceKind.Definition),
                    (fobUri, (5, 13, 5, 14), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReferencesCrossMultiModule() {
            var fobText = @"
from oarbaz import abc

abc()
";
            var oarText = "class abc1(object): pass";
            var bazText = "\n\n\n\nclass abc2(object): pass";
            var oarBazText = @"from oar import abc1 as abc
from baz import abc2 as abc";

            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            var bazUri = TestData.GetTempPathUri("baz.py");
            var oarBazUri = TestData.GetTempPathUri("oarbaz.py");
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);
                await server.SendDidOpenTextDocument(bazUri, bazText);
                await server.SendDidOpenTextDocument(oarBazUri, oarBazText);
                await server.GetAnalysisAsync(fobUri);
                await server.GetAnalysisAsync(oarUri);
                await server.GetAnalysisAsync(bazUri);
                await server.GetAnalysisAsync(oarBazUri);

                var referencesAbc1 = await server.SendFindReferences(oarUri, 0, 7);
                var referencesAbc2 = await server.SendFindReferences(bazUri, 4, 7);
                var referencesAbc = await server.SendFindReferences(fobUri, 3, 1);

                referencesAbc1.Should().OnlyHaveReferences(
                    (oarUri, (0, 0, 0, 24), ReferenceKind.Value),
                    (oarUri, (0, 6, 0, 10), ReferenceKind.Definition),
                    (oarBazUri, (0, 16, 0, 20), ReferenceKind.Reference),
                    (oarBazUri, (0, 24, 0, 27), ReferenceKind.Reference)
                );

                referencesAbc2.Should().OnlyHaveReferences(
                    (bazUri, (4, 0, 4, 24), ReferenceKind.Value),
                    (bazUri, (4, 6, 4, 10), ReferenceKind.Definition),
                    (oarBazUri, (1, 16, 1, 20), ReferenceKind.Reference),
                    (oarBazUri, (1, 24, 1, 27), ReferenceKind.Reference)
                );

                referencesAbc.Should().OnlyHaveReferences(
                    (oarUri, (0, 0, 0, 24), ReferenceKind.Value),
                    (bazUri, (4, 0, 4, 24), ReferenceKind.Value),
                    (oarBazUri, (0, 16, 0, 20), ReferenceKind.Reference),
                    (oarBazUri, (0, 24, 0, 27), ReferenceKind.Reference),
                    (oarBazUri, (1, 16, 1, 20), ReferenceKind.Reference),
                    (oarBazUri, (1, 24, 1, 27), ReferenceKind.Reference),
                    (fobUri, (1, 19, 1, 22), ReferenceKind.Reference),
                    (fobUri, (3, 0, 3, 3), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportStarReferences() {
            var fobText = @"
CONSTANT = 1
class Class: pass
def fn(): pass";
            var oarText = @"from fob import *



x = CONSTANT
c = Class()
f = fn()";
            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);
                await server.GetAnalysisAsync(fobUri);
                await server.GetAnalysisAsync(oarUri);

                var referencesConstant = await server.SendFindReferences(oarUri, 4, 5);
                var referencesClass = await server.SendFindReferences(oarUri, 5, 5);
                var referencesfn = await server.SendFindReferences(oarUri, 6, 5);

                referencesConstant.Should().OnlyHaveReferences(
                    (fobUri, (1, 0, 1, 8), ReferenceKind.Definition),
                    (oarUri, (4, 4, 4, 12), ReferenceKind.Reference)
                );

                referencesClass.Should().OnlyHaveReferences(
                    (fobUri, (2, 0, 2, 17), ReferenceKind.Value),
                    (fobUri, (2, 6, 2, 11), ReferenceKind.Definition),
                    (oarUri, (5, 4, 5, 9), ReferenceKind.Reference)
                );

                referencesfn.Should().OnlyHaveReferences(
                    (fobUri, (3, 0, 3, 14), ReferenceKind.Value),
                    (fobUri, (3, 4, 3, 6), ReferenceKind.Definition),
                    (oarUri, (6, 4, 6, 6), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportAsReferences() {
            var fobText = @"
CONSTANT = 1
class Class: pass
def fn(): pass";
            var oarText = @"from fob import CONSTANT as CO, Class as Cl, fn as f



x = CO
c = Cl()
g = f()";

            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);
                await server.GetAnalysisAsync(fobUri);
                await server.GetAnalysisAsync(oarUri);

                var referencesConstant = await server.SendFindReferences(fobUri, 1, 1);
                var referencesClass = await server.SendFindReferences(fobUri, 2, 7);
                var referencesFn = await server.SendFindReferences(fobUri, 3, 5);
                var referencesC0 = await server.SendFindReferences(oarUri, 4, 5);
                var referencesC1 = await server.SendFindReferences(oarUri, 5, 5);
                var referencesF = await server.SendFindReferences(oarUri, 6, 5);

                referencesConstant.Should().OnlyHaveReferences(
                    (fobUri, (1, 0, 1, 8), ReferenceKind.Definition),
                    (oarUri, (0, 16, 0, 24), ReferenceKind.Reference),
                    (oarUri, (0, 28, 0, 30), ReferenceKind.Reference),
                    (oarUri, (4, 4, 4, 6), ReferenceKind.Reference)
                );

                referencesClass.Should().OnlyHaveReferences(
                    (fobUri, (2, 0, 2, 17), ReferenceKind.Value),
                    (fobUri, (2, 6, 2, 11), ReferenceKind.Definition),
                    (oarUri, (0, 32, 0, 37), ReferenceKind.Reference),
                    (oarUri, (0, 41, 0, 43), ReferenceKind.Reference),
                    (oarUri, (5, 4, 5, 6), ReferenceKind.Reference)
                );

                referencesFn.Should().OnlyHaveReferences(
                    (fobUri, (3, 0, 3, 14), ReferenceKind.Value),
                    (fobUri, (3, 4, 3, 6), ReferenceKind.Definition),
                    (oarUri, (0, 45, 0, 47), ReferenceKind.Reference),
                    (oarUri, (0, 51, 0, 52), ReferenceKind.Reference),
                    (oarUri, (6, 4, 6, 5), ReferenceKind.Reference)
                );

                referencesC0.Should().OnlyHaveReferences(
                    (fobUri, (1, 0, 1, 8), ReferenceKind.Definition),
                    (oarUri, (0, 16, 0, 24), ReferenceKind.Reference),
                    (oarUri, (0, 28, 0, 30), ReferenceKind.Reference),
                    (oarUri, (4, 4, 4, 6), ReferenceKind.Reference)
                );

                referencesC1.Should().OnlyHaveReferences(
                    (fobUri, (2, 0, 2, 17), ReferenceKind.Value),
                    (fobUri, (2, 6, 2, 11), ReferenceKind.Definition),
                    (oarUri, (0, 32, 0, 37), ReferenceKind.Reference),
                    (oarUri, (0, 41, 0, 43), ReferenceKind.Reference),
                    (oarUri, (5, 4, 5, 6), ReferenceKind.Reference)
                );

                referencesF.Should().OnlyHaveReferences(
                    (fobUri, (3, 0, 3, 14), ReferenceKind.Value),
                    (fobUri, (3, 4, 3, 6), ReferenceKind.Definition),
                    (oarUri, (0, 45, 0, 47), ReferenceKind.Reference),
                    (oarUri, (0, 51, 0, 52), ReferenceKind.Reference),
                    (oarUri, (6, 4, 6, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReferencesGeneratorsV3() {
            var text = @"
[f for f in x]
[x for x in f]
(g for g in y)
(y for y in g)
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var referencesF = await server.SendFindReferences(uri, 1, 8);
                var referencesX = await server.SendFindReferences(uri, 2, 8);
                var referencesG = await server.SendFindReferences(uri, 3, 8);
                var referencesY = await server.SendFindReferences(uri, 4, 8);

                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 7, 1, 8), ReferenceKind.Definition),
                    (uri, (1, 1, 1, 2), ReferenceKind.Reference)
                );
                referencesX.Should().OnlyHaveReferences(
                    (uri, (2, 7, 2, 8), ReferenceKind.Definition),
                    (uri, (2, 1, 2, 2), ReferenceKind.Reference)
                );
                referencesG.Should().OnlyHaveReferences(
                    (uri, (3, 7, 3, 8), ReferenceKind.Definition),
                    (uri, (3, 1, 3, 2), ReferenceKind.Reference)
                );
                referencesY.Should().OnlyHaveReferences(
                    (uri, (4, 7, 4, 8), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/52")]
        public async Task ReferencesGeneratorsV2() {
            var text = @"
[f for f in x]
[x for x in f]
(g for g in y)
(y for y in g)
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                await server.SendDidOpenTextDocument(uri, text);
                await server.GetAnalysisAsync(uri);
                var referencesF = await server.SendFindReferences(uri, 1, 8);
                var referencesX = await server.SendFindReferences(uri, 2, 8);
                var referencesG = await server.SendFindReferences(uri, 3, 8);
                var referencesY = await server.SendFindReferences(uri, 4, 8);

                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 7, 1, 8), ReferenceKind.Definition),
                    (uri, (1, 1, 1, 2), ReferenceKind.Reference),
                    (uri, (2, 12, 2, 13), ReferenceKind.Reference)
                );
                referencesX.Should().OnlyHaveReferences(
                    (uri, (2, 7, 2, 8), ReferenceKind.Definition),
                    (uri, (1, 12, 1, 13), ReferenceKind.Reference),
                    (uri, (2, 1, 2, 2), ReferenceKind.Reference)
                );
                referencesG.Should().OnlyHaveReferences(
                    (uri, (3, 7, 3, 8), ReferenceKind.Definition),
                    (uri, (3, 1, 3, 2), ReferenceKind.Reference)
                );
                referencesY.Should().OnlyHaveReferences(
                    (uri, (4, 7, 4, 8), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task SignatureDefaults() {
            var code = @"
def f(x = None): pass

def g(x = {}): pass

def h(x = {2:3}): pass

def i(x = []): pass

def j(x = [None]): pass

def k(x = ()): pass

def l(x = (2, )): pass

def m(x = math.atan2(1, 0)): pass
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                
                var tests = new[] {
                    new { FuncName = "f", DefaultValue = "None" },
                    new { FuncName = "g", DefaultValue = "{}" },
                    new { FuncName = "h", DefaultValue = "{...}" },
                    new { FuncName = "i", DefaultValue = "[]" },
                    new { FuncName = "j", DefaultValue="[...]" },
                    new { FuncName = "k", DefaultValue = "()" },
                    new { FuncName = "l", DefaultValue = "(...)" },
                    new { FuncName = "m", DefaultValue = "math.atan2(1, 0)" },
                };

                foreach (var test in tests) {
                    analysis.Should().HaveFunctionWithSingleOverload(test.FuncName)
                        .Which.Should().HaveSingleParameter()
                        .Which.Should().HaveName("x").And.HaveDefaultValue(test.DefaultValue);
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task SpecialDictMethodsCrossUnitAnalysis() {
            using (var server = await CreateServerAsync()) {

                // dict methods which return lists
                var code = @"x = {}
iters = x.itervalues()
ks = x.keys()
iterks = x.iterkeys()
vs = x.values()

def f(z):
    z[42] = 100

f(x)
for iter in iters:
    print(iter)

for k in ks:
    print(k)

for iterk in iterks:
    print(iterk)

for v in vs:
    print(v)";


                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("iter").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("k").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("iterk").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("v").OfType(BuiltinTypeId.Int);
            

                // dict methods which return a key or value
                code = @"x = {}
xget = x.get(42)
xpop = x.pop()
def f(z):
    z[42] = 100

f(x)
y = xget
z = xpop";

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("z").OfType(BuiltinTypeId.Int);

                // dict methods which return a key/value tuple
                // dict methods which return a key or value
                code = @"x = {}
abc = x.popitem()
def f(z):
    z[42] = 100

f(x)
fob = abc";

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("fob")
                    .OfType(BuiltinTypeId.Tuple)
                    .WithDescription("tuple[int]");

                // dict methods which return a list of key/value tuple
                code = @"x = {}
iters = x.iteritems()
itms = x.items()
def f(z):
    z[42] = 100

f(x)
for iter in iters:
    print(iter)
for itm in itms:
    print(itm)";

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("iter").OfType(BuiltinTypeId.Tuple).WithDescription("tuple[int]")
                    .And.HaveVariable("itm").OfType(BuiltinTypeId.Tuple).WithDescription("tuple[int]");
            }
        }
/*
        /// <summary>
        /// Verifies that list indicies don't accumulate classes across multiple analysis
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task ListIndiciesCrossModuleAnalysis() {
            for (int i = 0; i < 2; i++) {
                var code1 = "l = []";
                var code2 = @"class C(object):
    pass

a = C()
import mod1
mod1.l.append(a)
";

                var state = CreateAnalyzer();
                var mod1 = state.AddModule("mod1", code1);
                var mod2 = state.AddModule("mod2", code2);
                state.ReanalyzeAll();

                if (i == 0) {
                    // re-preparing shouldn't be necessary
                    state.UpdateModule(mod2, code2);
                }

                mod2.Analyze(CancellationToken.None, true);
                state.WaitForAnalysis();

                state.AssertDescription("l", "list[C]");
                state.AssertIsInstance("l[0]", "C");
            }
        }
*/
        [TestMethod, Priority(0)]
        public async Task SpecialListMethodsCrossUnitAnalysis() {
            var code = @"x = []
def f(z):
    z.append(100)
    
f(x)
for fob in x:
    print(fob)


oar = x.pop()
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("oar").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("fob").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task SetLiteral() {
            var code = @"
x = {2, 3, 4}
for abc in x:
    print(abc)
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x").WithDescription("set[int]")
                    .And.HaveVariable("abc").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task SetOperators() {
            var code = @"
x = {1, 2, 3}
y = {3.14, 2.718}

x_or_y = x | y
x_and_y = x & y
x_sub_y = x - y
x_xor_y = x ^ y

y_or_x = y | x
y_and_x = y & x
y_sub_x = y - x
y_xor_x = y ^ x

x_or_y_0 = next(iter(x_or_y))
x_and_y_0 = next(iter(x_and_y))
x_sub_y_0 = next(iter(x_sub_y))
x_xor_y_0 = next(iter(x_xor_y))

y_or_x_0 = next(iter(y_or_x))
y_and_x_0 = next(iter(y_and_x))
y_sub_x_0 = next(iter(y_sub_x))
y_xor_x_0 = next(iter(y_xor_x))
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("x_or_y").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("y_or_x").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("x_and_y").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("y_and_x").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("x_sub_y").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("y_sub_x").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("x_xor_y").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("y_xor_x").OfType(BuiltinTypeId.Set)
                    .And.HaveVariable("x_or_y_0").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveVariable("y_or_x_0").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveVariable("x_and_y_0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y_and_x_0").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("x_sub_y_0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y_sub_x_0").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("x_xor_y_0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y_xor_x_0").OfType(BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task GetVariablesDictionaryGet() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"x = {42:'abc'}");
                analysis.Should().HaveVariable("x").WithValue<DictionaryInfo>()
                    .Which.Should().HaveMember<SpecializedCallable>("get")
                    .Which.Should().HaveDescription("bound built-in method get");
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictMethods() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"x = {42:'abc'}
a = x.items()[0][0]
b = x.items()[0][1]
c = x.keys()[0]
d = x.values()[0]
e = x.pop(1)
f = x.popitem()[0]
g = x.popitem()[1]
h = x.iterkeys().next()
i = x.itervalues().next()
j = x.iteritems().next()[0]
k = x.iteritems().next()[1]
");

                analysis.Should().HaveVariable("x").WithValue<DictionaryInfo>()
                    .And.HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("e").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("f").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("g").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("h").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("i").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("j").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("k").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictUpdate() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
a = {42:100}
b = {}
b.update(a)
c = b.items()[0][0]
d = b.items()[0][1]
");

                analysis.Should().HaveVariable("b").WithValue<DictionaryInfo>()
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictEnum() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
for x in {42:'abc'}:
    print(x)
");

                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FutureDivision() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
from __future__ import division
x = 1/2
            ");

                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task BoundMethodDescription() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
class C:
    def f(self):
        'doc string'

a = C()
b = a.f
            ");
                analysis.Should().HaveVariable("b").WithDescription("method f of test-module.C objects");

                await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
class C(object):
    def f(self):
        'doc string'

a = C()
b = a.f
            ");
                analysis.Should().HaveVariable("b").WithDescription("method f of test-module.C objects");
            }
        }

        [TestMethod, Priority(0)]
        public async Task LambdaExpression() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
x = lambda a: a
y = x(42)
");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);

                await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
def f(a):
    return a

x = lambda b: f(b)
y = x(42)
");
                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task LambdaScoping() {
            var code = @"def f(l1, l2):
    l1('abc')
    l2(42)


x = []
y = ()
f(lambda x=x:x, lambda x=y:x)";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                var function = analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.Tuple)
                    .And.HaveFunction("f")
                    .Which;

                function.Should().HaveParameter("l1").WithValue<IFunctionInfo>()
                    .Which.Should().HaveFunctionScope()
                    .Which.Should().HaveParameter("x").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Str);

                function.Should().HaveParameter("l2").WithValue<IFunctionInfo>()
                    .Which.Should().HaveFunctionScope()
                    .Which.Should().HaveParameter("x").OfTypes(BuiltinTypeId.Tuple, BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctionScoping() {
            var code = @"x = 100

def f(x = x):
    x

f('abc')
";

            using (var server = await CreateServerAsync()) {
                var uri = TestData.GetDefaultModuleUri();
                await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var referencesx1 = await server.SendFindReferences(uri, 2, 6);
                var referencesx2 = await server.SendFindReferences(uri, 2, 10);
                var referencesx3 = await server.SendFindReferences(uri, 3, 4);

                referencesx1.Should().OnlyHaveReferences(
                    (uri, (2, 6, 2, 7), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );

                referencesx2.Should().OnlyHaveReferences(
                    (uri, (0, 0, 0, 1), ReferenceKind.Definition),
                    (uri, (2, 10, 2, 11), ReferenceKind.Reference)
                );

                referencesx3.Should().OnlyHaveReferences(
                    (uri, (2, 6, 2, 7), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task RecursiveClass() {
            var code = @"
cls = object

class cls(cls):
    abc = 42

a = cls().abc
b = cls.abc
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var completion = await server.SendCompletion(TestData.GetDefaultModuleUri(), 8, 0);

                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Int);

                completion.Should().HaveLabels("cls", "object");
            }
        }

        [TestMethod, Priority(0)]
        public async Task BadMethod() {
            var code = @"
class cls(object): 
    def f(): 
        'help'
        return 42

abc = cls()
fob = abc.f()
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var signatures = await server.SendSignatureHelp(TestData.GetDefaultModuleUri(), 7, 11);

                analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.Int);
                signatures.Should().HaveSingleSignature()
                    .Which.Should().HaveMarkdownDocumentation("help");
            }
        }

        

        [MatrixRow(@"
def f(a, b, c): 
    pass", 3, "f", DisplayName = "def f")]
        [MatrixRow(@"
class f(object):
    def __init__(self, a, b, c):
        pass", 4, "f", DisplayName = "class f, def __init__")]
        [MatrixRow(@"
class f(object):
    def __new__(cls, a, b, c):
        pass", 4, "f", DisplayName = "class f, def __new__")]
        [MatrixRow(@"
class x(object):
    def g(self, a, b, c):
        pass

f = x().g", 6, "g", DisplayName = "class x, def g")]
        [MatrixColumn("f(c = 'abc', b = 42, a = 3j)")]
        [MatrixColumn("f(3j, c = 'abc', b = 42)")]
        [MatrixColumn("f(3j, 42, c = 'abc')")]
        [MatrixColumn("f(c = 'abc', b = 42, a = 3j, d = 42)")] // extra argument
        [MatrixColumn("f(3j, 42, 'abc', d = 42)")]
        [MatrixTestMethod(NameFormat = "{0}: {1}, {2}"), Priority(0)]
        public async Task KeywordArguments(string functionDeclaration, int signatureLine, string expectedName, string functionCall) {
            var code = functionDeclaration + Environment.NewLine + functionCall;

            using (var server = await CreateServerAsync()) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var signatures = await server.SendSignatureHelp(TestData.GetDefaultModuleUri(), signatureLine, 1);

                signatures.Should().OnlyHaveSignature($"{expectedName}(a: complex, b: int, c: str)");
            }
        }

        [TestMethod, Priority(0)]
        public async Task BadKeywordArguments() {
            var code = @"def f(a, b):
    return a

x = 100
z = f(a=42, x)";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("z").OfType(BuiltinTypeId.Int);
            }
        }

        [MatrixRow(@"def f(a, b, c, *d): 
    pass", 3, "f", DisplayName = "def f")]
        [MatrixRow(@"class f(object):
    def __init__(self, a, b, c, *d):
        pass", 4, "f", DisplayName = "class f, def __init__")]
        [MatrixRow(@"class f(object):
    def __new__(cls, a, b, c, *d):
        pass", 4, "f", DisplayName = "class f, def __new__")]
        [MatrixRow(@"class x(object):
    def g(self, a, b, c, *d):
        pass

f = x().g", 6, "g", DisplayName = "class x, def g")]
        [MatrixColumn("f(*(3j, 42, 'abc'))", "tuple")]
        [MatrixColumn("f(*[3j, 42, 'abc'])", "tuple")]
        [MatrixColumn("f(*(3j, 42, 'abc', 4L))", "tuple[long]")]
        [MatrixColumn("f(*[3j, 42, 'abc', 4L])", "tuple[long]")]
        [MatrixColumn("f(3j, *(42, 'abc'))", "tuple")]
        [MatrixColumn("f(3j, 42, *('abc',))", "tuple")]
        [MatrixColumn("f(3j, *(42, 'abc', 4L))", "tuple[long]")]
        [MatrixColumn("f(3j, 42, *('abc', 4L))", "tuple[long]")]
        [MatrixColumn("f(3j, 42, 'abc', *[4L])", "tuple[long]")]
        [MatrixColumn("f(3j, 42, 'abc', 4L)", "tuple[long]")]
        [MatrixTestMethod(NameFormat = "{0}: {1}, {2}"), Priority(0)]
        public async Task PositionalSplat(string functionDeclaration, int signatureLine, string expectedName, string functionCall, string expectedDType) {
            var code = functionDeclaration + Environment.NewLine + functionCall;

            using (var server = await CreateServerAsync()) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var signatures = await server.SendSignatureHelp(TestData.GetDefaultModuleUri(), signatureLine, 1);

                signatures.Should().OnlyHaveSignature($"{expectedName}(a: complex, b: int, c: str, *d: {expectedDType})");
            }
        }

        [MatrixRow(@"def f(a, b, c, **d): 
    pass", 3, "f", DisplayName = "def f")]
        [MatrixRow(@"class f(object):
    def __init__(self, a, b, c, **d):
        pass", 4, "f", DisplayName = "class f, def __init__")]
        [MatrixRow(@"class f(object):
    def __new__(cls, a, b, c, **d):
        pass", 4, "f", DisplayName = "class f, def __new__")]
        [MatrixRow(@"class x(object):
    def g(self, a, b, c, **d):
        pass

f = x().g", 6, "g", DisplayName = "class x, def g")]
        [MatrixColumn("f(**{'a': 3j, 'b': 42, 'c': 'abc'})")]
        [MatrixColumn("f(**{'c': 'abc', 'b': 42, 'a': 3j})")]
        [MatrixColumn("f(**{'a': 3j, 'b': 42, 'c': 'abc', 'x': 4L})")]
        [MatrixColumn("f(3j, **{'b': 42, 'c': 'abc'})")]
        [MatrixColumn("f(3j, 42, **{'c': 'abc'})")]
        [MatrixTestMethod(NameFormat = "{0}: {1}, {2}"), Priority(0)]
        public async Task KeywordSplat(string functionDeclaration, int signatureLine, string expectedName, string functionCall) {
            var code = functionDeclaration + Environment.NewLine + functionCall;

            using (var server = await CreateServerAsync()) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var signatures = await server.SendSignatureHelp(TestData.GetDefaultModuleUri(), signatureLine, 1);

                signatures.Should().OnlyHaveSignature($"{expectedName}(a: complex, b: int, c: str, **d: dict)");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRef() {
            var code = @"

class D(object):
    def oar(self, x):
        abc = C()
        abc.fob(2)
        a = abc.fob(2.0)
        a.oar(('a', 'b', 'c', 'd'))

class C(object):
    def fob(self, x):
        D().oar('abc')
        D().oar(['a', 'b', 'c'])
        return D()
    def baz(self): pass
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                var completionInD = await server.SendCompletion(TestData.GetDefaultModuleUri(), 3, 4);
                var completionInOar = await server.SendCompletion(TestData.GetDefaultModuleUri(), 5, 8);
                var completionForAbc = await server.SendCompletion(TestData.GetDefaultModuleUri(), 5, 12);

                completionInD.Should().HaveLabels("C", "D", "oar")
                    .And.NotContainLabels("a", "abc", "self", "x", "fob", "baz");

                completionInOar.Should().HaveLabels("C", "D", "a", "oar", "abc", "self", "x")
                    .And.NotContainLabels("fob", "baz");

                completionForAbc.Should().HaveLabels("baz", "fob");

                analysis.Should().HaveClass("D")
                    .Which.Should().HaveFunction("oar")
                    .Which.Should().HaveParameter("x").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Str, BuiltinTypeId.Tuple);
            };
        }

/*
        [TestMethod, Priority(0)]
        public async Task Builtins() {
            var text = @"
booltypetrue = True
booltypefalse = False
";
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("booltypetrue", BuiltinTypeId.Bool);
            entry.AssertIsInstance("booltypefalse", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryFunctionTable() {
            var text = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x['fob'](42, [])
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.List);
            entry.AssertIsInstance("a", text.IndexOf("x, y"));
            entry.AssertIsInstance("b", text.IndexOf("x, y"));
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryAssign() {
            var text = @"
x = {'abc': 42}
y = x['fob']
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("y", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryFunctionTableGet2() {
            var text = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x.get('fob')(42, [])
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.List);
            entry.AssertIsInstance("a", text.IndexOf("x, y"));
            entry.AssertIsInstance("b", text.IndexOf("x, y"));
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryFunctionTableGet() {
            var text = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
y = x.get('fob', None)
if y is not None:
    y(42, [])
";
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.List);
            entry.AssertIsInstance("a", text.IndexOf("x, y"));
            entry.AssertIsInstance("b", text.IndexOf("x, y"));
        }

        [TestMethod, Priority(0)]
        public async Task SimpleGlobals() {
            var text = @"
class x(object):
    def abc(self):
        pass
        
a = x()
x.abc()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(includeDunder: false), "a", "x");
            entry.AssertHasAttr("x", "abc");
            entry.AssertHasAttr("x", entry.ObjectMembers);
        }

        [TestMethod, Priority(0)]
        public async Task FuncCallInIf() {
            var text = @"
def Method(a, b, c):
    print a, b, c
    
if not Method(42, 'abc', []):
    pass
";
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("a", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("b", text.IndexOf("print"), BuiltinTypeId.Str);
            entry.AssertIsInstance("c", text.IndexOf("print"), BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task WithStatement() {
            var text = @"
class X(object):
    def x_method(self): pass
    def __enter__(self): return self
    def __exit__(self, exc_type, exc_value, traceback): return False
       
class Y(object):
    def y_method(self): pass
    def __enter__(self): return 123
    def __exit__(self, exc_type, exc_value, traceback): return False
 
with X() as x:
    pass #x

with Y() as y:
    pass #y
    
with X():
    pass
";
            var entry = ProcessText(text);
            entry.AssertHasAttr("x", text.IndexOf("pass #x"), "x_method");
            entry.AssertIsInstance("y", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task OverrideFunction() {
            var text = @"
class oar(object):
    def Call(self, xvar, yvar):
        pass

class baz(oar):
    def Call(self, xvar, yvar):
        x = 42
        pass

class Cxxxx(object):
    def __init__(self):
        self.fob = baz()
        
    def Cmeth(self, avar, bvar):
        self.fob.Call(avar, bvar)
        


abc = Cxxxx()
abc.Cmeth(['fob'], 'oar')
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("xvar", text.IndexOf("x = 42"), BuiltinTypeId.List);
            entry.AssertIsInstance("xvar", text.IndexOf("pass"));
        }


        [TestMethod, Priority(0)]
        public async Task FunctionOverloads() {
            var text = @"
def f(a, b, c=0):
    pass

f(1, 1, 1)
f(3.14, 3.14, 3.14)
f('a', 'b', 'c')
f(1, 3.14, 'c')
f('a', 'b', 1)
";
            var entry = ProcessText(text);
            var f = entry.GetSignatures("f", 0).Select(sig => {
                return string.Format(
                    "{0}({1})",
                    sig.Name,
                    string.Join(
                        ", ",
                        sig.Parameters
                        .Select(
                            p => {
                                if (String.IsNullOrWhiteSpace(p.DefaultValue)) {
                                    return string.Format("{0} := ({1})", p.Name, p.Type);
                                } else {
                                    return string.Format("{0} = {1} := ({2})", p.Name, p.DefaultValue, p.Type);
                                }
                            }
                        )
                    )
                );
            }).ToList();
            foreach (var sig in f) {
                Console.WriteLine(sig);
            }
            AssertUtil.ContainsExactly(f, "f(a := (float, int, str), b := (float, int, str), c = 0 := (float, int, str))");
        }

        internal static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*( *=.+)?", RegexOptions.IgnoreCase);
        internal static string GetSafeParameterName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }


        protected virtual string ListInitParameterName {
            get { return "object"; }
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/799
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task OverrideCompletions() {
            var text = @"
class oar(list):
    pass
";
            var entry = ProcessTextV2(text);

            var init = entry.GetOverrideable(text.IndexOf("pass")).Single(r => r.Name == "append");
            AssertUtil.AreEqual(init.Parameters.Select(GetSafeParameterName), "self", "value");

            entry = ProcessTextV3(text);

            init = entry.GetOverrideable(text.IndexOf("pass")).Single(r => r.Name == "append");
            AssertUtil.AreEqual(init.Parameters.Select(GetSafeParameterName), "self", "value");

            // Ensure that nested classes are correctly resolved.
            text = @"
class oar(int):
    class fob(dict):
        pass
";
            entry = ProcessTextV2(text);
            var oarItems = entry.GetOverrideable(text.IndexOf("    pass")).Select(x => x.Name).ToSet();
            var fobItems = entry.GetOverrideable(text.IndexOf("pass")).Select(x => x.Name).ToSet();
            AssertUtil.DoesntContain(oarItems, "keys");
            AssertUtil.DoesntContain(oarItems, "items");
            AssertUtil.ContainsAtLeast(oarItems, "bit_length");

            AssertUtil.ContainsAtLeast(fobItems, "keys", "items");
            AssertUtil.DoesntContain(fobItems, "bit_length");
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task DictCtor() {
            var text = @"
d1 = dict({2:3})
x1 = d1[2]

d2 = dict(x = 2)
x2 = d2['x']

d3 = dict(**{2:3})
x3 = d3[2]
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x1", BuiltinTypeId.Int);
            entry.AssertIsInstance("x2", BuiltinTypeId.Int);
            entry.AssertIsInstance("x3", BuiltinTypeId.Int);
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task SpecializedOverride() {
            var text = @"
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
            var entry = ProcessText(text);
            entry.AssertIsInstance("x1", BuiltinTypeId.Int);
            entry.AssertIsInstance("x2", BuiltinTypeId.Int);
            entry.AssertIsInstance("x3", BuiltinTypeId.Int);
            entry.AssertIsInstance("x4", BuiltinTypeId.Str);
            entry.AssertIsInstance("x5", BuiltinTypeId.Str);
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task SpecializedOverride2() {
            var text = @"
class setdict(dict):
    def __setitem__(self, index):
        pass

a = setdict()
a[42] = 100
b = a[42]
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("b");
        }

        /// <summary>
        /// We shouldn't use instance members when invoking special methods
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task IterNoInstance() {
            var text = @"
class me(object):
    pass


a = me()
a.__getitem__ = lambda x: 42

for v in a: pass
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("v", text.IndexOf("pass"));

            text = @"
class me(object):
    pass


a = me()
a.__iter__ = lambda: (yield 42)

for v in a: pass
";
            entry = ProcessText(text);
            entry.AssertIsInstance("v", text.IndexOf("pass"));
        }

        [TestMethod, Priority(0)]
        public async Task SimpleMethodCall() {
            var text = @"
class x(object):
    def abc(self, fob):
        pass
        
a = x()
a.abc('abc')
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("fob", text.IndexOf("pass"), BuiltinTypeId.Str);
            entry.AssertHasAttr("self", text.IndexOf("pass"), "abc");
            entry.AssertHasAttr("self", text.IndexOf("pass"), entry.ObjectMembers);
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinRetval() {
            var text = @"
x = [2,3,4]
a = x.index(2)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.List);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinFuncRetval() {
            var text = @"
x = ord('a')
y = range(5)
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public void FunctionMembers() {
            var text = @"
def f(x): pass
f.abc = 32
";
            var entry2 = ProcessTextV2(text);
            var entry3 = ProcessTextV3(text);
            entry2.AssertHasAttr("f", "abc");
            entry3.AssertHasAttr("f", "abc");

            text = @"
def f(x): pass

";
            entry2 = ProcessTextV2(text);
            entry3 = ProcessTextV3(text);
            entry2.AssertHasAttr("f", entry2.FunctionMembers);
            entry2.AssertNotHasAttr("f", "x");
            entry2.AssertIsInstance("f.func_name", BuiltinTypeId.Str);
            entry3.AssertHasAttr("f", entry3.FunctionMembers);
            entry3.AssertNotHasAttr("f", "x");
            entry3.AssertIsInstance("f.__name__", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task RangeIteration() {
            var text = @"
for i in range(5):
    pass
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("i", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImport() {
            var text = @"
import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(includeDunder: false), "sys");
            entry.AssertHasAttr("sys", "winver");
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImportInFunc() {
            var text = @"
def f():
    import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(text.IndexOf("sys"), includeDunder: false), "sys", "f");
            entry.AssertHasAttr("sys", text.IndexOf("sys"), "winver");
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImportInClass() {
            var text = @"
class C:
    import sys
";
            var entry = ProcessText(text);
            AssertUtil.ContainsExactly(entry.GetNamesNoBuiltins(text.IndexOf("sys"), includeDunder: false), "sys", "C");
            entry.AssertHasAttr("sys", text.IndexOf("sys"), "winver");
        }

        [TestMethod, Priority(0)]
        public async Task NoImportClr() {
            var text = @"
x = 'abc'
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Str);
            entry.AssertHasAttrExact("x", entry.StrMembers);
        }

        [TestMethod, Priority(0)]
        public async Task MutualRecursion() {
            var text = @"
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
            var entry = ProcessText(text);
            entry.AssertHasAttrExact("other", text.IndexOf("other.g"), "g", "__doc__", "__class__");
            entry.AssertIsInstance("x", BuiltinTypeId.List, BuiltinTypeId.Str);
            entry.AssertHasAttrExact("x", entry.ListMembers.Union(entry.StrMembers).ToArray());
        }

        [TestMethod, Priority(0)]
        public async Task MutualGeneratorRecursion() {
            var text = @"
class C:
    def f(self, other, depth):
        if depth == 0:
            yield 'abc'
        yield next(other.g(self, depth - 1))

class D:
    def g(self, other, depth):
        if depth == 0:
            yield ['d', 'e', 'f']
        
        yield next(other.f(self, depth - 1))

x = next(D().g(C(), 42))

";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.List, BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task DistinctGenerators() {
            var text = @"
def f(x):
    return x

def g(x):
    yield f(x)

class S0(object): pass
it = g(S0())
val = next(it)

" + string.Join("\r\n", Enumerable.Range(1, 100).Select(i => string.Format("class S{0}(object): pass\r\nf(S{0}())", i)));
            Console.WriteLine(text);

            // Ensure the returned generators are distinct
            var entry = ProcessText(text);
            entry.AssertIsInstance("it", BuiltinTypeId.Generator);
            entry.AssertIsInstance("val", "S0");
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRefVars() {
            var text = @"
class x(object):
    def __init__(self, val):
        self.abc = [val]
    
x(42)
x('abc')
x([])
";
            var entry = ProcessText(text);
            var values = entry.GetValues("self.abc", text.IndexOf("self.abc"));
            Assert.AreEqual(1, values.Length);
            entry.AssertDescription("self.abc", text.IndexOf("self.abc"), "list");
        }

        [TestMethod, Priority(0)]
        public async Task ReturnFunc() {
            var text = @"
def g():
    return []

def f():
    return g
    
x = f()()
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnArg() {
            var text = @"
def g(a):
    return a

x = g(1)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnArg2() {
            var text = @"

def f(a):
    def g():
        return a
    return g

x = f(2)()
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign() {
            var text = @"
class C:
    def func(self):
        self.abc = 42

a = C()
a.func()
fob = a.abc
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("fob", BuiltinTypeId.Int);
            entry.AssertHasAttrExact("fob", entry.IntMembers);
            entry.AssertHasAttrExact("a", "abc", "func", "__doc__", "__class__");
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign2() {
            var text = @"
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
            var entry = ProcessText(text);
            // TODO: AssertUtil.ContainsExactly(entry.GetTypesFromName("fob", 0), ListType);
            Assert.Inconclusive("Test not yet implemented");
        }

        [TestMethod, Priority(0)]
        public async Task AnnotatedAssign() {
            var text = @"
x : int = 42

class C:
    y : int = 42

    def func(self):
        self.abc : int = 42

a = C()
a.func()
fob1 = a.abc
fob2 = a.y
fob3 = x
";
            var entry = ProcessText(text, PythonLanguageVersion.V36);
            entry.AssertIsInstance("fob1", BuiltinTypeId.Int);
            entry.AssertIsInstance("fob2", BuiltinTypeId.Int);
            entry.AssertIsInstance("fob3", BuiltinTypeId.Int);
            entry.AssertHasAttr("a", "abc", "func", "y", "__doc__", "__class__");

            text = @"
def f(val):
    print(val)

class C:
    def __init__(self, y):
        self.y = y

x:f(42) = 1
x:C(42) = 1
";
            entry = ProcessText(text, PythonLanguageVersion.V36);
            entry.AssertIsInstance("val", text.IndexOf("print"), BuiltinTypeId.Int);
            entry.AssertIsInstance("y", text.IndexOf("self."), BuiltinTypeId.Int);
        }


        [TestMethod, Priority(0)]
        public async Task UnfinishedDot() {
            // the partial dot should be ignored and we shouldn't see g as
            // a member of D
            var text = @"
class D(object):
    def func(self):
        self.
        
def g(a, b, c): pass
";
            var entry = ProcessText(text, allowParseErrors: true);
            entry.AssertHasAttr("self", text.IndexOf("self."), "func");
            entry.AssertHasAttr("self", text.IndexOf("self."), entry.ObjectMembers);
        }

        [TestMethod, Priority(0)]
        public async Task CrossModule() {
            var text1 = @"
import mod2
";
            var text2 = @"
x = 42
";

            await PermutedTestAsync("mod", new[] { text1, text2 }, state => {
                AssertUtil.ContainsExactly(
                    state.GetMemberNames(state.Modules["mod1"], "mod2", 0).Where(n => n.Length < 4 || !n.StartsWith("__") || !n.EndsWith("__")),
                    "x"
                );
            });
        }

        [TestMethod, Priority(0)]
        public async Task CrossModuleCall() {
            var text1 = @"
import mod2
y = mod2.f('abc')
";
            var text2 = @"
def f(x):
    return x
";

            await PermutedTestAsync("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod2"], "x", text2.IndexOf("return x"), BuiltinTypeId.Str);
                state.AssertIsInstance(state.Modules["mod1"], "y", BuiltinTypeId.Str);
            });
        }

        [TestMethod, Priority(0)]
        public async Task CrossModuleCallType() {
            var text1 = @"
import mod2
y = mod2.c('abc').x
";
            var text2 = @"
class c:
    def __init__(self, x):
        self.x = x
";

            await PermutedTestAsync("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod2"], "x", text2.IndexOf("= x"), BuiltinTypeId.Str);
                state.AssertIsInstance(state.Modules["mod1"], "y", BuiltinTypeId.Str);
            });
        }

        [TestMethod, Priority(0)]
        public async Task CrossModuleCallType2() {
            var text1 = @"
from mod2 import c
class x(object):
    def Fob(self):
        y = c('abc').x
";
            var text2 = @"
class c:
    def __init__(self, x):
        self.x = x
";

            await PermutedTestAsync("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod2"], "x", text2.IndexOf("= x"), BuiltinTypeId.Str);
                state.AssertIsInstance(state.Modules["mod1"], "y", text1.IndexOf("y ="), BuiltinTypeId.Str);
            });
        }

        [TestMethod, Priority(0)]
        public async Task CrossModuleFuncAndType() {
            var text1 = @"
class Something(object):
    def f(self): pass
    def g(self): pass


def SomeFunc():
    x = Something()
    return x
";
            var text2 = @"
from mod1 import SomeFunc

x = SomeFunc()
";

            var text3 = @"
from mod2 import x
a = x
";

            await PermutedTestAsync("mod", new[] { text1, text2, text3 }, state => {
                state.AssertHasAttr(state.Modules["mod3"], "a", 0, "f", "g");
                state.AssertHasAttr(state.Modules["mod3"], "a", 0, state.ObjectMembers);
            });
        }

        [TestMethod, Priority(0)]
        public async Task MembersAfterError() {
            var text = @"
class X(object):
    def f(self):
        return self.
        
    def g(self):
        pass
        
    def h(self):
        pass
";
            var entry = ProcessText(text, allowParseErrors: true);
            entry.AssertHasAttr("self", text.IndexOf("self."), "f", "g", "h");
            entry.AssertHasAttr("self", text.IndexOf("self."), entry.ObjectMembers);
        }


        [TestMethod, Priority(0)]
        public async Task Property() {
            var text = @"
class x(object):
    @property
    def SomeProp(self):
        return 42

a = x().SomeProp
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task StaticMethod() {
            var text = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

a = x().StaticMethod(4.0)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task InheritedStaticMethod() {
            var text = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

class y(x):
    pass

a = y().StaticMethod(4.0)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethod() {
            var text = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

a = x().ClassMethod()
b = x.ClassMethod()
";
            var entry = ProcessText(text);
            entry.AssertDescription("a", "x");
            entry.AssertDescription("b", "x");
            entry.AssertDescription("cls", text.IndexOf("return"), "x");

            var exprs = new[] { "x.ClassMethod", "x().ClassMethod" };
            foreach (var expr in exprs) {
                // cls is implied, so expect no parameters
                entry.AssertHasParameters(expr);
            }

            text = @"
class x(object):
    @classmethod
    def UncalledClassMethod(cls):
        return cls
";
            entry = ProcessText(text);
            entry.AssertDescription("cls", text.IndexOf("return"), "x");
        }

        [TestMethod, Priority(0)]
        public async Task InheritedClassMethod() {
            var text = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

class y(x):
    pass

a = y().ClassMethod()
b = y.ClassMethod()
";
            var entry = ProcessTextV2(text);
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("a"), "x", "y");
            AssertUtil.ContainsExactly(entry.GetShortDescriptions("b"), "x", "y");
            var desc = string.Join(Environment.NewLine, entry.GetCompletionDocumentation("", "cls", text.IndexOf("return")));
            AssertUtil.Contains(desc, "x", "y");

            var exprs = new[] { "y.ClassMethod", "y().ClassMethod" };
            foreach (var expr in exprs) {
                // cls is implied, so expect no parameters
                entry.AssertHasParameters(expr);
            }
        }

        [TestMethod, Priority(0)]
        public async Task UserDescriptor() {
            var text = @"
class mydesc(object):
    def __get__(self, inst, ctx):
        return 42

class C(object):
    x = mydesc()

fob = C.x
oar = C().x
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("fob", BuiltinTypeId.Int);
            entry.AssertIsInstance("oar", BuiltinTypeId.Int);
            entry.AssertIsInstance("C.x", BuiltinTypeId.Int);

            entry.AssertIsInstance("ctx", text.IndexOf("return"), BuiltinTypeId.Type);
            entry.AssertIsInstance("inst", text.IndexOf("return 42"), "None", "C");

            text = @"
class mydesc(object):
    def __get__(self, inst, ctx):
        return 42

class C(object):
    x = mydesc()
    def instfunc(self):
        pass

oar = C().x
";

            entry = ProcessText(text);
            entry.AssertIsInstance("inst", text.IndexOf("return 42"), "C");
            entry.AssertHasAttr("inst", text.IndexOf("return 42"), "instfunc");
        }

        [TestMethod, Priority(0)]
        public async Task AssignSelf() {
            var text = @"
class x(object):
    def __init__(self):
        self.x = 'abc'
    def f(self):
        pass
";
            var entry = ProcessText(text);
            entry.AssertHasAttr("self", text.IndexOf("pass"), "x");
            entry.AssertIsInstance("self.x", text.IndexOf("pass"), BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task AssignToMissingMember() {
            var text = @"
class test():
    x = 0;
    y = 1;
t = test()
t.x, t. =
";
            // http://pytools.codeplex.com/workitem/733

            // this just shouldn't crash, we should handle the malformed code, not much to inspect afterwards...
            var entry = ProcessText(text, allowParseErrors: true);
        }

        class EmptyAnalysisCookie : IAnalysisCookie {
            public static EmptyAnalysisCookie Instance = new EmptyAnalysisCookie();
            public string GetLine(int lineNo) {
                throw new NotImplementedException();
            }
        }

        static void AnalyzeLeak(Action func, int minutesBeforeMeasure = 1, int minutesBeforeAssert = 1) {
            long RUN_TIME = minutesBeforeMeasure * 60 * 1000;
            long LEAK_TIME = minutesBeforeAssert * 60 * 1000;

            var sw = new Stopwatch();
            sw.Start();
            for (var start = sw.ElapsedMilliseconds; start + RUN_TIME > sw.ElapsedMilliseconds;) {
                func();
            }

            var memory1 = GC.GetTotalMemory(true);

            for (var start = sw.ElapsedMilliseconds; start + LEAK_TIME > sw.ElapsedMilliseconds;) {
                func();
            }

            var memory2 = GC.GetTotalMemory(true);

            var delta = memory2 - memory1;
            Trace.TraceInformation("Usage after {0} minute(s): {1}", minutesBeforeMeasure, memory1);
            Trace.TraceInformation("Usage after {0} minute(s): {1}", minutesBeforeAssert, memory2);
            Trace.TraceInformation("Change: {0}", delta);

            Assert.AreEqual((double)memory1, (double)memory2, memory2 * 0.1, string.Format("Memory increased by {0}", delta));
        }

        //[TestMethod, Priority(2), Timeout(5 * 60 * 1000)]
        public void MemLeak() {
            var state = CreateAnalyzer();
            var oar = state.AddModule("oar", "");
            var baz = state.AddModule("baz", "");

            AnalyzeLeak(() => {
                state.UpdateModule(oar, @"
import sys
from baz import D

class C(object):
    def f(self, b):
        x = sys.version
        y = sys.exc_clear()
        a = []
        a.append(b)
        return a

a = C()
z = a.f(D())
min(a, D())

");

                state.UpdateModule(baz, @"
from oar import C

class D(object):
    def g(self, a):
        pass

a = D()
a.f(C())
z = C().f(42)

min(a, D())
");
            });
        }

        //[TestMethod, Priority(2), Timeout(15 * 60 * 1000)]
        public void MemLeak2() {
            bool anyTested = false;

            foreach (var ver in PythonPaths.Versions) {
                var azureDir = Path.Combine(ver.PrefixPath, "Lib", "site-packages", "azure");
                if (Directory.Exists(azureDir)) {
                    anyTested = true;
                    AnalyzeDirLeak(azureDir);
                }
            }

            if (!anyTested) {
                Assert.Inconclusive("Test requires Azure SDK to be installed");
            }
        }

        private void AnalyzeDirLeak(string dir) {
            List<string> files = new List<string>();
            CollectFiles(dir, files);

            List<FileStreamReader> sourceUnits = new List<FileStreamReader>();
            foreach (string file in files) {
                sourceUnits.Add(
                    new FileStreamReader(file)
                );
            }

            Stopwatch sw = new Stopwatch();

            sw.Start();
            long start0 = sw.ElapsedMilliseconds;
            using (var state = CreateAnalyzer()) {
                var projectState = state.Analyzer;
                var modules = new List<IPythonProjectEntry>();
                foreach (var sourceUnit in sourceUnits) {
                    modules.Add(projectState.AddModule(ModulePath.FromFullPath(sourceUnit.Path).ModuleName, sourceUnit.Path, null));
                }
                long start1 = sw.ElapsedMilliseconds;
                Trace.TraceInformation("AddSourceUnit: {0} ms", start1 - start0);

                var nodes = new List<Microsoft.PythonTools.Parsing.Ast.PythonAst>();
                for (int i = 0; i < modules.Count; i++) {
                    PythonAst ast = null;
                    try {
                        var sourceUnit = sourceUnits[i];

                        ast = Parser.CreateParser(sourceUnit, projectState.LanguageVersion).ParseFile();
                    } catch (Exception) {
                    }
                    nodes.Add(ast);
                }
                long start2 = sw.ElapsedMilliseconds;
                Trace.TraceInformation("Parse: {0} ms", start2 - start1);

                for (int i = 0; i < modules.Count; i++) {
                    var ast = nodes[i];

                    if (ast != null) {
                        using (var p = modules[i].BeginParse()) {
                            p.Tree = ast;
                            p.Complete();
                        }
                    }
                }

                long start3 = sw.ElapsedMilliseconds;
                for (int i = 0; i < modules.Count; i++) {
                    Trace.TraceInformation("Analyzing {1}: {0} ms", sw.ElapsedMilliseconds - start3, sourceUnits[i].Path);
                    var ast = nodes[i];
                    if (ast != null) {
                        modules[i].Analyze(CancellationToken.None, true);
                    }
                }
                if (modules.Count > 0) {
                    Trace.TraceInformation("Analyzing queue");
                    modules[0].AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
                }

                int index = -1;
                for (int i = 0; i < modules.Count; i++) {
                    if (((ProjectEntry)modules[i]).ModuleName == "azure.servicebus.servicebusservice") {
                        index = i;
                        break;
                    }
                }
                AnalyzeLeak(() => {
                    using (var reader = new FileStreamReader(modules[index].FilePath)) {
                        var ast = Parser.CreateParser(reader, projectState.LanguageVersion).ParseFile();

                        using (var p = modules[index].BeginParse()) {
                            p.Tree = ast;
                            p.Complete();
                        }
                    }

                    modules[index].Analyze(CancellationToken.None, true);
                    modules[index].AnalysisGroup.AnalyzeQueuedEntries(CancellationToken.None);
                });
            }
        }

        [TestMethod, Priority(0)]
        public void CancelAnalysis() {
            var ver = PythonPaths.Versions.LastOrDefault(v => v.IsCPython);
            if (ver == null) {
                Assert.Inconclusive("Test requires Python installation");
            }

            var cancelSource = new CancellationTokenSource();
            var task = Task.Run(() => new AnalysisTest().AnalyzeDir(Path.Combine(ver.PrefixPath, "Lib"), ver.Version, cancel: cancelSource.Token));

            // Allow 10 seconds for parsing to complete and analysis to start
            cancelSource.CancelAfter(TimeSpan.FromSeconds(10));

            // Allow 20 seconds after cancellation to abort
            if (!task.Wait(TimeSpan.FromSeconds(30))) {
                try {
                    task.Dispose();
                } catch (InvalidOperationException) {
                }
                Assert.Fail("Analysis did not abort within 20 seconds");
            }
        }

        [TestMethod, Priority(0)]
        public void MoveClass() {
            var fobSrc = "";

            var oarSrc = @"
class C(object):
    pass
";

            var bazSrc = @"
class C(object):
    pass
";

            using (var state = CreateAnalyzer()) {
                var fob = state.AddModule("fob", fobSrc);
                var oar = state.AddModule("oar", oarSrc);
                var baz = state.AddModule("baz", bazSrc);

                state.UpdateModule(fob, "from oar import C");
                state.WaitForAnalysis();

                state.WaitForAnalysis();

                state.AssertDescription(fob, "C", "C");
                state.AssertReferencesInclude(fob, "C", 0,
                    new VariableLocation(1, 17, VariableType.Reference, fob.FilePath),
                    new VariableLocation(2, 1, VariableType.Value, oar.FilePath)
                );

                // delete the class..
                state.UpdateModule(oar, "");
                state.WaitForAnalysis();

                state.AssertIsInstance(fob, "C");

                state.UpdateModule(fob, "from baz import C");
                state.WaitForAnalysis();

                state.AssertDescription(fob, "C", "C");
                state.AssertReferencesInclude(fob, "C", 0,
                    new VariableLocation(1, 17, VariableType.Reference, fob.FilePath),
                    new VariableLocation(2, 1, VariableType.Value, baz.FilePath)
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Package() {
            var src1 = "";

            var src2 = @"
from fob.y import abc
import fob.y as y
";

            var src3 = @"
abc = 42
";

            using (var state = CreateAnalyzer()) {
                var package = state.AddModule("fob", src1, "fob\\__init__.py");
                var x = state.AddModule("fob.x", src2);
                var y = state.AddModule("fob.y", src3);
                state.WaitForAnalysis();

                state.AssertDescription(x, "y", "Python module fob.y");
                state.AssertIsInstance(x, "abc", BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public void PackageRelativeImport() {
            using (var state = CreateAnalyzer()) {
                state.CreateProjectOnDisk = true;

                var package = state.AddModule("fob", "from .y import abc", "fob\\__init__.py");
                var x = state.AddModule("fob.x", "from .y import abc");
                var y = state.AddModule("fob.y", "abc = 42");

                state.WaitForAnalysis();

                state.AssertIsInstance(x, "abc", BuiltinTypeId.Int);
                state.AssertIsInstance(package, "abc", BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public void PackageRelativeImportPep328() {
            var imports = new Dictionary<string, string>() {
                { "from .moduleY import spam", "spam"},
                { "from .moduleY import spam as ham", "ham"},
                { "from . import moduleY", "moduleY.spam" },
                { "from ..subpackage1 import moduleY", "moduleY.spam"},
                { "from ..subpackage2.moduleZ import eggs", "eggs"},
                { "from ..moduleA import foo", "foo" },
                { "from ...package import bar", "bar"}
            };

            foreach (var imp in imports) {
                using (var state = CreateAnalyzer()) {
                    state.CreateProjectOnDisk = true;

                    var package = state.AddModule("package", "def bar():\n  pass\n", @"package\__init__.py");
                    var modA = state.AddModule("package.moduleA", "def foo():\n  pass\n", @"package\moduleA.py");

                    var sub1 = state.AddModule("package.subpackage1", string.Empty, @"package\subpackage1\__init__.py");
                    var modX = state.AddModule("package.subpackage1.moduleX", imp.Key, @"package\subpackage1\moduleX.py");
                    var modY = state.AddModule("package.subpackage1.moduleY", "def spam():\n  pass\n", @"package\subpackage1\moduleY.py");

                    var sub2 = state.AddModule("package.subpackage2", string.Empty, @"package\subpackage2\__init__.py");
                    var modZ = state.AddModule("package.subpackage2.moduleZ", "def eggs():\n  pass\n", @"package\subpackage2\moduleZ.py");

                    state.WaitForAnalysis();
                    state.AssertIsInstance(modX, imp.Value, BuiltinTypeId.Function);
                }
            }
        }

        [TestMethod, Priority(0)]
        public void PackageRelativeImportAliasedMember() {
            // similar to unittest package which has unittest.main which contains a function called "main".
            // Make sure we see the function, not the module.
            using (var state = CreateAnalyzer()) {
                state.CreateProjectOnDisk = true;

                var package = state.AddModule("fob", "from .y import y", "fob\\__init__.py");
                var y = state.AddModule("fob.y", "def y(): pass");

                state.WaitForAnalysis();

                state.AssertIsInstance(package, "y", BuiltinTypeId.Module, BuiltinTypeId.Function);
            }
        }


        [TestMethod, Priority(0)]
        public void Defaults() {
            var text = @"
def f(x = 42):
    return x
    
a = f()
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void Decorator() {
            var text1 = @"
import mod2

inst = mod2.MyClass()

@inst.mydec
def f():
    return 42
    

";

            var text2 = @"
import mod1

class MyClass(object):
    def mydec(self, x):
        return x
";

            PermutedTest("mod", new[] { text1, text2 }, state => {
                state.AssertIsInstance(state.Modules["mod1"], "f", BuiltinTypeId.Function);
                state.AssertIsInstance(state.Modules["mod2"], "mod1.f", BuiltinTypeId.Function);
                state.AssertIsInstance(state.Modules["mod2"], "MyClass().mydec(mod1.f)", BuiltinTypeId.Function);
            });
        }


        [TestMethod, Priority(0)]
        public void DecoratorFlow() {
            var text1 = @"
import mod2

inst = mod2.MyClass()

@inst.filter(fob=42)
def f():
    return 42
    
";

            var text2 = @"
import mod1

class MyClass(object):
    def filter(self, name=None, filter_func=None, **flags):
        # @register.filter()
        def dec(func):
            return self.filter_function(func, **flags)
        return dec
    def filter_function(self, func, **flags):
        name = getattr(func, ""_decorated_function"", func).__name__
        return self.filter(name, func, **flags)
";

            PermutedTest("mod", new[] { text1, text2 }, state => {
                // Ensure we ended up with a function
                state.AssertIsInstance(state.Modules["mod1"], "f", 0, BuiltinTypeId.Function);

                // Ensure we passed a function in to the decorator (def dec(func))
                //state.AssertIsInstance(state.Modules["mod2"], "func", text2.IndexOf("return self.filter_function("), BuiltinTypeId.Function);

                // Ensure we saw the function passed *through* the decorator
                state.AssertIsInstance(state.Modules["mod2"], "func", text2.IndexOf("return self.filter("), BuiltinTypeId.Function);

                // Ensure we saw the function passed *back into* the original decorator constructor
                state.AssertIsInstance(
                    state.Modules["mod2"], "filter_func", text2.IndexOf("# @register.filter()"),
                    BuiltinTypeId.Function,
                    BuiltinTypeId.NoneType
                );
            });
        }

        [TestMethod, Priority(0)]
        public void DecoratorTypes() {
            var text = @"
def nop(fn):
    def wrap():
        return fn()
    wp = wrap
    return wp

@nop
def a_tuple():
    return (1, 2, 3)

@nop
def a_list():
    return [1, 2, 3]

@nop
def a_float():
    return 0.1

@nop
def a_string():
    return 'abc'

x = a_tuple()
y = a_list()
z = a_float()
w = a_string()
";
            var entry = ProcessTextV2(text);
            entry.AssertIsInstance("x", BuiltinTypeId.Tuple);
            entry.AssertIsInstance("y", BuiltinTypeId.List);
            entry.AssertIsInstance("z", BuiltinTypeId.Float);
            entry.AssertIsInstance("w", BuiltinTypeId.Str);

            text = @"
def as_list(fn):
    def wrap(v):
        if v == 0:
            return list(fn())
        elif v == 1:
            return set(fn(*args, **kwargs))
        else:
            return str(fn())
    return wrap

@as_list
def items():
    return (1, 2, 3)

items2 = as_list(lambda: (1, 2, 3))

x = items(0)
";
            entry = ProcessTextV2(text);
            entry.AssertIsInstance("items", entry.GetTypeIds("items2").ToArray());
            entry.AssertIsInstance("x", BuiltinTypeId.List, BuiltinTypeId.Set, BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public void DecoratorReturnTypes_NoDecorator() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"# without decorator
def returnsGiven(parm):
    return parm

retGivenInt = returnsGiven(1)
retGivenString = returnsGiven('str')
retGivenBool = returnsGiven(True)
";

            var entry = ProcessText(text);

            entry.AssertIsInstance("retGivenInt", BuiltinTypeId.Int);
            entry.AssertIsInstance("retGivenString", BuiltinTypeId.Str);
            entry.AssertIsInstance("retGivenBool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public void DecoratorReturnTypes_DecoratorNoParams() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"# with decorator without wrap
def decoratorFunctionTakesArg1(f):
    def wrapped_f(arg):
        return f(arg)
    return wrapped_f

@decoratorFunctionTakesArg1
def returnsGivenWithDecorator1(parm):
    return parm

retGivenInt = returnsGivenWithDecorator1(1)
retGivenString = returnsGivenWithDecorator1('str')
retGivenBool = returnsGivenWithDecorator1(True)
";

            var entry = ProcessText(text);

            entry.AssertIsInstance("retGivenInt", BuiltinTypeId.Int);
            entry.AssertIsInstance("retGivenString", BuiltinTypeId.Str);
            entry.AssertIsInstance("retGivenBool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public void DecoratorReturnTypes_DecoratorWithParams() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"
# with decorator with wrap
def decoratorFunctionTakesArg2():
    def wrap(f):
        def wrapped_f(arg):
            return f(arg)
        return wrapped_f
    return wrap

@decoratorFunctionTakesArg2()
def returnsGivenWithDecorator2(parm):
    return parm

retGivenInt = returnsGivenWithDecorator2(1)
retGivenString = returnsGivenWithDecorator2('str')
retGivenBool = returnsGivenWithDecorator2(True)";

            var entry = ProcessText(text);

            entry.AssertIsInstance("retGivenInt", BuiltinTypeId.Int);
            entry.AssertIsInstance("retGivenString", BuiltinTypeId.Str);
            entry.AssertIsInstance("retGivenBool", BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public void DecoratorOverflow() {
            var text1 = @"
import mod2

@mod2.decorator_b
def decorator_a(fn):
    return fn
    

";

            var text2 = @"
import mod1

@mod1.decorator_a
def decorator_b(fn):
    return fn
";

            PermutedTest("mod", new[] { text1, text2 }, state => {
                // Neither decorator is callable, but at least analysis completed
                state.AssertIsInstance(state.Modules["mod1"], "decorator_a", BuiltinTypeId.Function);
                state.AssertIsInstance(state.Modules["mod2"], "decorator_b", BuiltinTypeId.Function);
            });
        }

        [TestMethod, Priority(0)]
        public void ProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";

            var entry = CreateAnalyzer();
            entry.Analyzer.Limits.ProcessCustomDecorators = true;
            entry.AddModule("fob", text);
            entry.WaitForAnalysis();

            entry.AssertIsInstance("my_fn", BuiltinTypeId.List);
            entry.AssertIsInstance("fn", text.IndexOf("return"), BuiltinTypeId.Function);
        }

        [TestMethod, Priority(0)]
        public void NoProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";

            var entry = CreateAnalyzer();
            entry.Analyzer.Limits.ProcessCustomDecorators = false;
            entry.AddModule("fob", text);
            entry.WaitForAnalysis();

            entry.AssertIsInstance("my_fn", BuiltinTypeId.Function);
            entry.AssertIsInstance("fn", text.IndexOf("return"), BuiltinTypeId.Function);
        }

        [TestMethod, Priority(0)]
        public void DecoratorReferences() {
            var text = @"
def d1(f):
    return f
class d2:
    def __call__(self, f): return f

@d1
def func_d1(): pass
@d2()
def func_d2(): pass

@d1
class cls_d1(object): pass
@d2()
class cls_d2(object): pass
";
            var entry = ProcessText(text);
            entry.AssertReferences("d1",
                new VariableLocation(2, 1, VariableType.Value),
                new VariableLocation(2, 5, VariableType.Definition),
                new VariableLocation(7, 2, VariableType.Reference),
                new VariableLocation(12, 2, VariableType.Reference)
            );
            entry.AssertReferences("d2",
                new VariableLocation(4, 1, VariableType.Value),
                new VariableLocation(4, 7, VariableType.Definition),
                new VariableLocation(9, 2, VariableType.Reference),
                new VariableLocation(14, 2, VariableType.Reference)
            );
            AssertUtil.ContainsExactly(entry.GetValues("f", 18).Select(v => v.MemberType), PythonMemberType.Function, PythonMemberType.Class);
            AssertUtil.ContainsExactly(entry.GetValues("f", 66).Select(v => v.MemberType), PythonMemberType.Function, PythonMemberType.Class);
            AssertUtil.ContainsExactly(entry.GetValues("func_d1").Select(v => v.MemberType), PythonMemberType.Function);
            AssertUtil.ContainsExactly(entry.GetValues("func_d2").Select(v => v.MemberType), PythonMemberType.Function);
            AssertUtil.ContainsExactly(entry.GetValues("cls_d1").Select(v => v.MemberType), PythonMemberType.Class);
            AssertUtil.ContainsExactly(entry.GetValues("cls_d2").Select(v => v.MemberType), PythonMemberType.Class);
        }

        [TestMethod, Priority(0)]
        public void DecoratorClass() {
            var text = @"
def dec1(C):
    def sub_method(self): pass
    C.sub_method = sub_method
    return C

@dec1
class MyBaseClass1(object):
    def base_method(self): pass

def dec2(C):
    class MySubClass(C):
        def sub_method(self): pass
    return MySubClass

@dec2
class MyBaseClass2(object):
    def base_method(self): pass

mc1 = MyBaseClass1()
mc2 = MyBaseClass2()
";
            var entry = ProcessText(text);
            AssertUtil.ContainsAtLeast(entry.GetMemberNames("mc1", 0, GetMemberOptions.None), "base_method", "sub_method");
            entry.AssertIsInstance("mc2", "MySubClass");
            AssertUtil.ContainsAtLeast(entry.GetMemberNames("mc2", 0, GetMemberOptions.None), "sub_method");
        }

        [TestMethod, Priority(0)]
        public void ClassInit() {
            var text = @"
class X:
    def __init__(self, value):
        self.value = value

a = X(2)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a.value", 0, BuiltinTypeId.Int);
            entry.AssertIsInstance("value", text.IndexOf("self."), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void InstanceCall() {
            var text = @"
class X:
    def __call__(self, value):
        return value

x = X()

a = x(2)
";
            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.Int);
        }

        /// <summary>
        /// Verifies that regardless of how we get to imports/function return values that
        /// we properly understand the imported value.
        /// </summary>
        [TestMethod, Priority(0)]
        public void ImportScopesOrder() {
            var text1 = @"
import _io
import mod2
import mmap as mm

import sys
def f():
    return sys

def g():
    return _io

def h():
    return mod2.sys

def i():
    import zlib
    return zlib

def j():
    return mm

def k():
    return mod2.impp

import operator as op

import re

";

            var text2 = @"
import sys
import imp as impp
";
            PermutedTest("mod", new[] { text1, text2 }, state => {
                state.DefaultModule = "mod1";
                state.AssertDescription("g", "mod1.g() -> _io");
                state.AssertDescription("f", "mod1.f() -> sys");
                state.AssertDescription("h", "mod1.h() -> sys");
                state.AssertDescription("i", "mod1.i() -> zlib");
                state.AssertDescription("j", "mod1.j() -> mmap");
                state.AssertDescription("k", "mod1.k() -> imp");
            });
        }

        [TestMethod, Priority(0)]
        public void ClassNew() {
            var text = @"
class X:
    def __new__(cls, value):
        res = object.__new__(cls)
        res.value = value
        return res

a = X(2)
";
            var entry = ProcessText(text);
            entry.AssertDescription("cls", text.IndexOf("= value"), "X");
            entry.AssertIsInstance("value", text.IndexOf("res.value = "), BuiltinTypeId.Int);
            entry.AssertIsInstance("res", text.IndexOf("res.value = "), "X");
            entry.AssertIsInstance("a", text.IndexOf("a = "), "X");
            entry.AssertIsInstance("a.value", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void Global() {
            var text = @"
x = None
y = None
def f():
    def g():
        global x, y
        x = 123
        y = 123
    return x, y

a, b = f()
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("a", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("x", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("y", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void Nonlocal() {
            var text = @"
def f():
    x = None
    y = None
    def g():
        nonlocal x, y
        x = 123
        y = 234
    return x, y

a, b = f()
";

            var entry = ProcessTextV3(text);
            entry.AssertIsInstance("x", text.IndexOf("nonlocal"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("y", text.IndexOf("nonlocal"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("x", text.IndexOf("return"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("y", text.IndexOf("return"), BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("a", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            entry.AssertIsInstance("b", BuiltinTypeId.NoneType, BuiltinTypeId.Int);

            entry.AssertReferences("x", text.IndexOf("x ="),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(6, 18, VariableType.Reference),
                new VariableLocation(7, 9, VariableType.Definition),
                new VariableLocation(9, 12, VariableType.Reference)
            );

            entry.AssertReferences("y", text.IndexOf("x ="),
                new VariableLocation(4, 5, VariableType.Definition),
                new VariableLocation(6, 21, VariableType.Reference),
                new VariableLocation(8, 9, VariableType.Definition),
                new VariableLocation(9, 15, VariableType.Reference)
            );


            text = @"
def f(x):
    def g():
        nonlocal x
        x = 123
    return x

a = f(None)
";

            entry = ProcessTextV3(text);
            entry.AssertIsInstance("a", BuiltinTypeId.NoneType, BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void IsInstance() {
            var text = @"
x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100
    pass
else:
    pass
    assert isinstance(x, str)
    y = 200
    pass
    




if isinstance(x, tuple):
    fob = 300
    pass
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("x", text.IndexOf("z ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("x", text.IndexOf("z =") + 1, BuiltinTypeId.Int);
            entry.AssertIsInstance("x", text.IndexOf("pass"), BuiltinTypeId.NoneType, BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("x", text.IndexOf("y ="), BuiltinTypeId.Str);
            entry.AssertIsInstance("x", text.IndexOf("y =") + 1, BuiltinTypeId.Str);
            entry.AssertIsInstance("x", text.IndexOf("else:") + 7, BuiltinTypeId.NoneType, BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("x", text.IndexOf("fob ="), BuiltinTypeId.Tuple);
            entry.AssertIsInstance("x", text.LastIndexOf("pass"), BuiltinTypeId.Tuple);

            entry.AssertReferences("x",
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("z ="),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("z =") + 1,
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("z =") - 2,
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("y ="),
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("y =") + 1,
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            entry.AssertReferences("x", text.IndexOf("y =") - 2,
                new VariableLocation(2, 1, VariableType.Definition),
                new VariableLocation(7, 23, VariableType.Reference),
                new VariableLocation(12, 23, VariableType.Reference),
                new VariableLocation(20, 15, VariableType.Reference)
            );

            text = @"
def f(a):
    def g():
        nonlocal a
        print(a)
        assert isinstance(a, int)
        pass

f('abc')
";

            entry = ProcessTextV3(text);
            entry.AssertIsInstance("a");
            entry.AssertIsInstance("a", text.IndexOf("def g()"), BuiltinTypeId.Int, BuiltinTypeId.Unicode);
            entry.AssertIsInstance("a", text.IndexOf("pass"), BuiltinTypeId.Int);
            entry.AssertIsInstance("a", text.IndexOf("print(a)"), BuiltinTypeId.Int, BuiltinTypeId.Unicode);

            text = @"x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100
    
    pass

print(z)";

            entry = ProcessText(text);
            entry.AssertIsInstance("z", BuiltinTypeId.Int);
            entry.AssertIsInstance("z", text.IndexOf("z ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("z", text.Length - 1, BuiltinTypeId.Int);

            entry.AssertReferences("z",
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );

            entry.AssertReferences("z", text.IndexOf("z ="),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(11, 7, VariableType.Reference)
            );

            // http://pytools.codeplex.com/workitem/636

            // this just shouldn't crash, we should handle the malformed code, not much to inspect afterwards...

            entry = ProcessText("if isinstance(x, list):\r\n", allowParseErrors: true);
            entry = ProcessText("if isinstance(x, list):", allowParseErrors: true);
        }

        [TestMethod, Priority(0)]
        public void NestedIsInstance() {
            var code = @"
def f():
    x = None
    y = None

    assert isinstance(x, int)
    z = x

    assert isinstance(y, int)
    w = y

    pass";

            var entry = ProcessText(code);
            entry.AssertIsInstance("z", code.IndexOf("pass"), BuiltinTypeId.Int);
            entry.AssertIsInstance("w", code.IndexOf("pass"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void NestedIsInstance1908() {
            // https://pytools.codeplex.com/workitem/1908
            var code = @"
def f(x):
    y = object()    
    assert isinstance(x, int)
    if isinstance(y, float):
        print('hi')

    pass
";

            var entry = ProcessText(code);
            entry.AssertIsInstance("y", code.IndexOf("pass"), BuiltinTypeId.Object, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void IsInstanceUserDefinedType() {
            var text = @"
class C(object):
    def f(self):
        pass

def f(a):
    assert isinstance(a, C)
    print(a)
    pass
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("a", text.IndexOf("print(a)"), "C");
        }

        [TestMethod, Priority(0)]
        public void IsInstanceNested() {
            var text = @"
class R: pass

def fn(a, b, c):
    result = R()
    assert isinstance(a, str)
    result.a = a

    assert isinstance(b, type)
    if isinstance(b, tuple):
        pass
    result.b = b

    assert isinstance(c, str)
    result.c = c
    return result

r1 = fn('fob', (int, str), 'oar')
r2 = fn(123, None, 4.5)
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("r1.a", BuiltinTypeId.Str);
            entry.AssertIsInstance("r1.b", BuiltinTypeId.Type, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("r1.c", BuiltinTypeId.Str);

            entry.AssertIsInstance("r2.a", BuiltinTypeId.Str);
            entry.AssertIsInstance("r2.b", BuiltinTypeId.Type, BuiltinTypeId.Tuple);
            entry.AssertIsInstance("r2.c", BuiltinTypeId.Str);
        }

        private static IEnumerable<string> DumpScopesToStrings(InterpreterScope scope) {
            yield return scope.Name;
            foreach (var child in scope.Children) {
                foreach (var s in DumpScopesToStrings(child)) {
                    yield return "  " + s;
                }
            }
        }

        [TestMethod, Priority(0)]
        public void IsInstanceAndLambdaScopes() {
            // https://github.com/Microsoft/PTVS/issues/2801
            var text = @"if isinstance(p, dict):
    v = [i for i in (lambda x: x)()]";

            var entry = ProcessTextV3(text);
            var scope = entry.Modules[entry.DefaultModule].Analysis.Scope;
            var dump = string.Join(Environment.NewLine, DumpScopesToStrings(scope));

            Console.WriteLine($"Actual:{Environment.NewLine}{dump}");

            Assert.AreEqual(entry.DefaultModule + @"
  <statements>
  <isinstance scope>
    <comprehension scope>
      <lambda>
        <statements>
  <statements>", dump);
        }

        [TestMethod, Priority(0)]
        public void IsInstanceReferences() {
            var text = @"def fob():
    oar = get_b()
    assert isinstance(oar, float)

    if oar.complex:
        raise IndexError

    return oar";

            var entry = ProcessText(text);

            for (int i = text.IndexOf("oar", 0); i >= 0; i = text.IndexOf("oar", i + 1)) {
                entry.AssertReferences("oar", i,
                    new VariableLocation(2, 5, VariableType.Definition),
                    new VariableLocation(3, 23, VariableType.Reference),
                    new VariableLocation(5, 8, VariableType.Reference),
                    new VariableLocation(8, 12, VariableType.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FunctoolsDecoratorReferences() {
            var text = @"from functools import wraps

def d(f):
    @wraps(f)
    def wrapped(*a, **kw):
        return f(*a, **kw)
    return wrapped

@d
def g(p):
    return p

n1 = g(1)";

            var entry = ProcessText(text);

            entry.AssertReferences("d",
                new VariableLocation(3, 1, VariableType.Value),
                new VariableLocation(3, 5, VariableType.Definition),
                new VariableLocation(9, 2, VariableType.Reference)
            );

            entry.AssertReferences("g",
                new VariableLocation(4, 5, VariableType.Value),
                new VariableLocation(10, 5, VariableType.Definition),
                new VariableLocation(13, 6, VariableType.Reference)
            );

            // Decorators that don't use @wraps will expose the wrapper function
            // as a value.
            text = @"def d(f):
    def wrapped(*a, **kw):
        return f(*a, **kw)
    return wrapped

@d
def g(p):
    return p

n1 = g(1)";

            entry = ProcessText(text);

            entry.AssertReferences("d",
                new VariableLocation(1, 1, VariableType.Value),
                new VariableLocation(1, 5, VariableType.Definition),
                new VariableLocation(6, 2, VariableType.Reference)
            );

            entry.AssertReferences("g",
                new VariableLocation(2, 5, VariableType.Value),
                new VariableLocation(7, 5, VariableType.Definition),
                new VariableLocation(10, 6, VariableType.Reference)
            );
        }

        [TestMethod, Priority(0)]
        public void QuickInfo() {
            var text = @"
import sys
a = 41.0
b = 42L
c = 'abc'
x = (2, 3, 4)
y = [2, 3, 4]
z = 43

class fob(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = fob()

def f():
    print 'hello'
    return 'abc'

def g():
    return c.Length

def h():
    return f
    return g

class return_func_class:
    def return_func(self):
        '''some help'''
        return self.return_func


def docstr_func():
    '''useful documentation'''
    return 42

def with_params(a, b, c):
    pass

def with_params_default(a, b, c = 100):
    pass

def with_params_default_2(a, b, c = []):
    pass

def with_params_default_3(a, b, c = ()):
    pass

def with_params_default_4(a, b, c = {}):
    pass

def with_params_default_2a(a, b, c = [None]):
    pass

def with_params_default_3a(a, b, c = (None, )):
    pass

def with_params_default_4a(a, b, c = {42: 100}):
    pass

def with_params_default_starargs(*args, **kwargs):
    pass
";
            var entry = ProcessTextV2(text);

            entry.AssertIsInstance("fob()", "fob");
            entry.AssertDescription("int()", "int");
            entry.AssertDescription("a", "float");
            entry.AssertDescription("a", "float");
            entry.AssertDescription("b", "long");
            entry.AssertDescription("c", "str");
            entry.AssertIsInstance("x", BuiltinTypeId.Tuple);
            entry.AssertIsInstance("y", BuiltinTypeId.List);
            entry.AssertDescription("z", "int");
            entry.AssertDescriptionContains("min", "min(");
            entry.AssertDescriptionContains("list.append", "list.append(");
            entry.AssertIsInstance("\"abc\".Length");
            entry.AssertIsInstance("c.Length");
            entry.AssertIsInstance("d", "fob");
            entry.AssertDescription("sys", "sys");
            entry.AssertDescription("f", "test-module.f() -> str");
            entry.AssertDescription("fob.f", "test-module.fob.f(self: fob)\r\ndeclared in fob");
            entry.AssertDescription("fob().g", "method g of test-module.fob objects");
            entry.AssertDescription("fob", "class test-module.fob(object)");
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.StringSplitOptions.RemoveEmptyEntries", 1), "field of type StringSplitOptions");
            entry.AssertDescription("g", "test-module.g()");    // return info could be better
            //AssertUtil.ContainsExactly(entry.GetVariableDescriptionsByIndex("System.AppDomain.DomainUnload", 1), "event of type System.EventHandler");
            entry.AssertDescription("None", "None");
            entry.AssertDescription("f.func_name", "property of type str");
            entry.AssertDescription("h", "test-module.h() -> test-module.f() -> str, test-module.g()");
            entry.AssertDescription("docstr_func", "test-module.docstr_func() -> int");
            entry.AssertDocumentation("docstr_func", "useful documentation");

            entry.AssertDescription("with_params", "test-module.with_params(a, b, c)");
            entry.AssertDescription("with_params_default", "test-module.with_params_default(a, b, c: int=100)");
            entry.AssertDescription("with_params_default_2", "test-module.with_params_default_2(a, b, c: list=[])");
            entry.AssertDescription("with_params_default_3", "test-module.with_params_default_3(a, b, c: tuple=())");
            entry.AssertDescription("with_params_default_4", "test-module.with_params_default_4(a, b, c: dict={})");
            entry.AssertDescription("with_params_default_2a", "test-module.with_params_default_2a(a, b, c: list=[...])");
            entry.AssertDescription("with_params_default_3a", "test-module.with_params_default_3a(a, b, c: tuple=(...))");
            entry.AssertDescription("with_params_default_4a", "test-module.with_params_default_4a(a, b, c: dict={...})");
            entry.AssertDescription("with_params_default_starargs", "test-module.with_params_default_starargs(*args, **kwargs)");

            // method which returns itself, we shouldn't stack overflow producing the help...
            entry.AssertDescription("return_func_class().return_func", "method return_func of test-module.return_func_class objects...");
            entry.AssertDocumentation("return_func_class().return_func", "some help");
        }

        [TestMethod, Priority(0)]
        public void CompletionDocumentation() {
            var text = @"
import sys
a = 41.0
b = 42L
c = 'abc'
x = (2, 3, 4)
y = [2, 3, 4]
z = 43

class fob(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = fob()

def f():
    print 'hello'
    return 'abc'

def g():
    return c.Length
";
            var entry = ProcessText(text);

            AssertUtil.Contains(entry.GetCompletionDocumentation("", "d", 1).First(), "fob");
            AssertUtil.Contains(entry.GetCompletionDocumentation("", "int", 1).First(), "integer");
            AssertUtil.Contains(entry.GetCompletionDocumentation("", "min", 1).First(), "min(");
        }

        [TestMethod, Priority(0)]
        public void MemberType() {
            var text = @"
import sys
a = 41.0
b = 42L
c = 'abc'
x = (2, 3, 4)
y = [2, 3, 4]
z = 43

class fob(object):
    @property
    def f(self): pass

    def g(self): pass
    
d = fob()

def f():
    print 'hello'
    return 'abc'

def g():
    return c.Length
";
            var entry = ProcessText(text);


            entry.AssertAttrIsType("f", "func_name", PythonMemberType.Property);
            entry.AssertAttrIsType("f", "func_name", PythonMemberType.Property);
            entry.AssertAttrIsType("list", "append", PythonMemberType.Method);
            entry.AssertAttrIsType("y", "append", PythonMemberType.Method);
            entry.AssertAttrIsType("", "int", PythonMemberType.Class);
            entry.AssertAttrIsType("", "min", PythonMemberType.Function);
            entry.AssertAttrIsType("", "sys", PythonMemberType.Module);
        }

        [TestMethod, Priority(0)]
        public void RecurisveDataStructures() {
            var text = @"
d = {}
d[0] = d
";
            var entry = ProcessTextV2(text);

            entry.AssertDescription("d", "dict({int : dict})");
        }

        /// <summary>
        /// Variable is refered to in the base class, defined in the derived class, we should know the type information.
        /// </summary>
        [TestMethod, Priority(0)]
        public void BaseReferencedDerivedDefined() {
            var text = @"
class Base(object):
    def f(self):
        x = self.map

class Derived(Base):
    def __init__(self):
        self.map = {}

pass
";

            var entry = ProcessText(text);
            entry.AssertAttrIsType("Derived()", "map", PythonMemberType.Field);
        }


        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod, Priority(0)]
        public void NoTypesButIsMember() {
            var text = @"
def f(x, y):
    C(x, y)

class C(object):
    def __init__(self, x, y):
        self.x = x
        self.y = y

f(1)
";

            var entry = ProcessText(text);
            entry.AssertHasAttr("C()", "x", "y");
        }

        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod, Priority(0)]
        public void SequenceFromSequence() {
            var text = @"
x = []
x.append(1)

t = (1, )

class MyIndexer(object):
    def __getitem__(self, index):
        return 1

ly = list(x)
lz = list(MyIndexer())

ty = tuple(x)
tz = tuple(MyIndexer())

lyt = list(t)
tyt = tuple(t)
";

            var entry = ProcessText(text);
            entry.AssertIsInstance("x[0]", BuiltinTypeId.Int);

            foreach (string value in new[] { "ly", "lz", "ty", "tz", "lyt", "tyt" }) {
                entry.AssertIsInstance(value + "[0]", BuiltinTypeId.Int);
            }
        }

#if FALSE
        [TestMethod, Priority(0)]
        public void SaveStdLib() {
            // only run this once...
            if (GetType() == typeof(AnalysisTest)) {
                var stdLib = AnalyzeStdLib();

                string tmpFolder = TestData.GetTempPath("6666d700-a6d8-4e11-8b73-3ba99a61e27b");

                new SaveAnalysis().Save(stdLib, tmpFolder);

                File.Copy(Path.Combine(PythonInterpreterFactory.GetBaselineDatabasePath(), "__builtin__.idb"), Path.Combine(tmpFolder, "__builtin__.idb"), true);

                var newPs = new PythonAnalyzer(new CPythonInterpreter(new TypeDatabase(tmpFolder)), PythonLanguageVersion.V27);
            }
        }
#endif


        [TestMethod, Priority(0)]
        public void SubclassFindAllRefs() {
            string text = @"
class Base(object):
    def __init__(self):
        self.fob()
    
    def fob(self): 
        pass
    
    
class Derived(Base):
    def fob(self): 
        'x'
";

            var entry = ProcessText(text);

            var refs = new[] {
                new VariableLocation(4, 14, VariableType.Reference),
                new VariableLocation(6, 5, VariableType.Value),
                new VariableLocation(6, 9, VariableType.Definition),
                new VariableLocation(11, 5, VariableType.Value),
                new VariableLocation(11, 9, VariableType.Definition),
            };

            entry.AssertReferences("self.fob", text.IndexOf("'x'"), refs);
            entry.AssertReferences("self.fob", text.IndexOf("pass"), refs);
            entry.AssertReferences("self.fob", text.IndexOf("self.fob"), refs);
        }

        /// <summary>
        /// Verifies that constructing lists / tuples from more lists/tuples doesn't cause an infinite analysis as we keep creating more lists/tuples.
        /// </summary>
        [TestMethod, Priority(0)]
        public void ListRecursion() {
            string text = @"
def f(x):
    print abc
    return f(list(x))

abc = f(())
";

            var entry = ProcessText(text);

            //var vars = entry.GetVariables("fob", GetLineNumber(text, "'x'"));

        }

        [TestMethod, Priority(0)]
        public void TypeAtEndOfMethod() {
            string text = @"
class Fob(object):
    def oar(self, a):
        pass


    def fob(self): 
        pass

x = Fob()
x.oar(100)
";

            var entry = ProcessText(text);
            var mod = entry.Modules[entry.DefaultModule].Analysis;

            AssertUtil.ContainsAtLeast(mod.GetAllAvailableMembers(new SourceLocation(6, 9)).Select(mr => mr.Name), "a");
        }

        [TestMethod, Priority(0)]
        public void TypeAtEndOfIncompleteMethod() {
            string text = @"
class Fob(object):
    def oar(self, a):





x = Fob()
x.oar(100)
";

            var entry = ProcessText(text, allowParseErrors: true);
            var mod = entry.Modules[entry.DefaultModule].Analysis;

            AssertUtil.ContainsAtLeast(mod.GetAllAvailableMembers(new SourceLocation(6, 9)).Select(mr => mr.Name), "a");
        }

        [TestMethod, Priority(0)]
        public void TypeIntersectionUserDefinedTypes() {
            string text = @"
class C1(object):
    def fob(self): pass

class C2(object):
    def oar(self): pass

c = C1()
c.fob()
c = C2()

";

            var entry = ProcessText(text);
            AssertUtil.DoesntContain(entry.GetMemberNames("c", 0, GetMemberOptions.IntersectMultipleResults), new[] { "fob", "oar" });
        }

        [TestMethod, Priority(0)]
        public void UpdateMethodMultiFiles() {
            string text1 = @"
def f(abc):
    pass
";

            string text2 = @"
import mod1
mod1.f(42)
";

            var state = CreateAnalyzer();

            // add both files to the project
            var entry1 = state.AddModule("mod1", text1);
            var entry2 = state.AddModule("mod2", text2);

            state.WaitForAnalysis();
            state.AssertIsInstance(entry1, "abc", text1.IndexOf("pass"), BuiltinTypeId.Int);

            // re-analyze project1, we should still know about the type info provided by module2
            state.UpdateModule(entry1, null);
            state.WaitForAnalysis();

            state.AssertIsInstance(entry1, "abc", text1.IndexOf("pass"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void MetaClassesV2() {

            string text = @"class C(type):
    def f(self):
        print('C.f')

    def x(self, var):
        pass


class D(object):
    __metaclass__ = C
    @classmethod
    def g(cls):
        print cls.g


    def inst_method(self):
        pass
    ";

            var entry = ProcessTextV2(text);
            int i = text.IndexOf("print cls.g");
            entry.AssertHasParameters("cls.f", i);
            entry.AssertHasParameters("cls.g", i);
            entry.AssertHasParameters("cls.x", i, "var");
            entry.AssertHasParameters("cls.inst_method", i, "self");
        }

        [TestMethod, Priority(0)]
        public void MetaClassesV3() {
            var text = @"class C(type):
    def f(self):
        print('C.f')

    def x(self, var):
        pass


class D(object, metaclass = C):
    @classmethod
    def g(cls):
        print(cls.g)


    def inst_method(self):
        pass
    ";

            var entry = ProcessTextV3(text);
            int i = text.IndexOf("print(cls.g)");
            entry.AssertHasParameters("cls.f", i);
            entry.AssertHasParameters("cls.g", i);
            entry.AssertHasParameters("cls.x", i, "var");
            entry.AssertHasParameters("cls.inst_method", i, "self");
        }

        /// <summary>
        /// Tests assigning odd things to the metaclass variable.
        /// </summary>
        [TestMethod, Priority(0)]
        public void InvalidMetaClassValues() {
            var assigns = new[] { "[1,2,3]", "(1,2)", "1", "abc", "1.0", "lambda x: 42", "C.f", "C().f", "f", "{2:3}" };

            foreach (var assign in assigns) {
                string text = @"
class C(object): 
    def f(self): pass

def f():  pass

class D(object):
    __metaclass__ = " + assign + @"
    @classmethod
    def g(cls):
        print cls.g


    def inst_method(self):
        pass
    ";

                ProcessTextV2(text);
            }

            foreach (var assign in assigns) {
                string text = @"
class C(object): 
    def f(self): pass

def f():  pass

class D(metaclass = " + assign + @"):
    @classmethod
    def g(cls):
        print cls.g


    def inst_method(self):
        pass
    ";

                ProcessTextV3(text, allowParseErrors: true);
            }
        }

        [TestMethod, Priority(0)]
        public void FromImport() {
            ProcessText("from #   blah", allowParseErrors: true);
        }

        [TestMethod, Priority(0)]
        public void SelfNestedMethod() {
            // http://pytools.codeplex.com/workitem/648
            var code = @"class MyClass:
    def func1(self):
        def func2(a, b):
            return a

        return func2('abc', 123)

x = MyClass().func1()
";

            var entry = ProcessText(code);

            entry.AssertIsInstance("x", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public void Super() {
            var code = @"
class Base1(object):
    def base_func(self, x): pass
    def base1_func(self): pass
class Base2(object):
    def base_func(self, x, y, z): pass
    def base2_func(self): pass
class Derived1(Base1, Base2):
    def derived1_func(self):
        print('derived1_func')
class Derived2(Base2, Base1):
    def derived2_func(self):
        print('derived2_func')
class Derived3(object):
    def derived3_func(self):
        cls = Derived1
        cls = Derived2
        print('derived3_func')
";
            var entry = ProcessText(code);

            // super(Derived1)
            {
                // Member from derived class should not be present
                entry.AssertNotHasAttr("super(Derived1)", code.IndexOf("print('derived1_func')"), "derived1_func");

                // Members from both base classes with distinct names should be present, and should have all parameters including self
                entry.AssertHasParameters("super(Derived1).base1_func", code.IndexOf("print('derived1_func')"), "self");
                entry.AssertHasParameters("super(Derived1).base2_func", code.IndexOf("print('derived1_func')"), "self");

                // Only one member with clashing names should be present, and it should be from Base1
                entry.AssertHasParameters("super(Derived1).base_func", code.IndexOf("print('derived1_func')"), "self", "x");
            }

            // super(Derived2)
            {
                // Only one member with clashing names should be present, and it should be from Base2
                entry.AssertHasParameters("super(Derived2).base_func", code.IndexOf("print('derived2_func')"), "self", "x", "y", "z");
            }

            // super(Derived1, self), or Py3k magic super() to the same effect
            int i = code.IndexOf("print('derived1_func')");
            entry.AssertNotHasAttr("super(Derived1, self)", i, "derived1_func");
            entry.AssertHasParameters("super(Derived1, self).base1_func", i);
            entry.AssertHasParameters("super(Derived1, self).base2_func", i);
            entry.AssertHasParameters("super(Derived1, self).base_func", i, "x");

            if (entry.Analyzer.LanguageVersion.Is3x()) {
                entry.AssertNotHasAttr("super()", i, "derived1_func");
                entry.AssertHasParameters("super().base1_func", i);
                entry.AssertHasParameters("super().base2_func", i);
                entry.AssertHasParameters("super().base_func", i, "x");
            }

            // super(Derived2, self), or Py3k magic super() to the same effect
            i = code.IndexOf("print('derived2_func')");
            entry.AssertHasParameters("super(Derived2, self).base_func", i, "x", "y", "z");
            if (entry.Analyzer.LanguageVersion.Is3x()) {
                entry.AssertHasParameters("super().base_func", i, "x", "y", "z");
            }

            // super(Derived1 union Derived1)
            {
                // Members with clashing names from both potential bases should be unioned
                var sigs = entry.GetSignatures("super(cls).base_func", code.IndexOf("print('derived3_func')"));
                Assert.AreEqual(2, sigs.Length);
                Assert.IsTrue(sigs.Any(overload => overload.Parameters.Length == 2)); // (self, x)
                Assert.IsTrue(sigs.Any(overload => overload.Parameters.Length == 4)); // (self, x, y, z)
            }
        }

        [TestMethod, Priority(0)]
        public void ParameterAnnotation() {
            var text = @"
s = None
def f(s: s = 123):
    return s
";
            var entry = ProcessTextV3(text);

            entry.AssertIsInstance("s", text.IndexOf("s:"), BuiltinTypeId.Int, BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("s ="), BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("return"), BuiltinTypeId.Int, BuiltinTypeId.NoneType);
        }

        [TestMethod, Priority(0)]
        public void ParameterAnnotationLambda() {
            var text = @"
s = None
def f(s: lambda s: s > 0 = 123):
    return s
";
            var entry = ProcessTextV3(text);

            entry.AssertIsInstance("s", text.IndexOf("s:"), BuiltinTypeId.Int);
            entry.AssertIsInstance("s", text.IndexOf("s >"), BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("return"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void ReturnAnnotation() {
            var text = @"
s = None
def f(s = 123) -> s:
    return s
";
            var entry = ProcessTextV3(text);

            entry.AssertIsInstance("s", text.IndexOf("(s =") + 1, BuiltinTypeId.Int);
            entry.AssertIsInstance("s", text.IndexOf("s:"), BuiltinTypeId.NoneType);
            entry.AssertIsInstance("s", text.IndexOf("return"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void FunctoolsPartial() {
            var text = @"
from _functools import partial

def fob(a, b, c, d):
    return a, b, c, d

sanity = fob(123, 3.14, 'abc', [])

fob_1 = partial(fob, 123, 3.14, 'abc', [])
result_1 = fob_1()

fob_2 = partial(fob, d = [], c = 'abc', b = 3.14, a = 123)
result_2 = fob_2()

fob_3 = partial(fob, 123, 3.14)
result_3 = fob_3('abc', [])

fob_4 = partial(fob, c = 'abc', d = [])
result_4 = fob_4(123, 3.14)

func_from_fob_1 = fob_1.func
args_from_fob_1 = fob_1.args
keywords_from_fob_2 = fob_2.keywords
";
            var entry = ProcessText(text);

            foreach (var name in new[] {
                "sanity",
                "result_1",
                "result_2",
                "result_3",
                "result_4",
                "args_from_fob_1"
            }) {
                entry.AssertDescription(name, "tuple[int, float, str, list]");
                var result = entry.GetValue<AnalysisValue>(name);
                Console.WriteLine("{0} = {1}", name, result);
                AssertTupleContains(result, BuiltinTypeId.Int, BuiltinTypeId.Float, entry.BuiltinTypeId_Str, BuiltinTypeId.List);
            }

            var fob = entry.GetValue<FunctionInfo>("fob");
            var fob2 = entry.GetValue<FunctionInfo>("func_from_fob_1");
            Assert.AreSame(fob, fob2);

            entry.GetValue<DictionaryInfo>("keywords_from_fob_2");
        }

        [TestMethod, Priority(0)]
        public void FunctoolsWraps() {
            var text = @"
from functools import wraps, update_wrapper

def decorator1(fn):
    @wraps(fn)
    def wrapper(*args, **kwargs):
        fn(*args, **kwargs)
        return 'decorated'
    return wrapper

@decorator1
def test1():
    '''doc'''
    return 'undecorated'

def test2():
    pass

def test2a():
    pass

test2.test_attr = 123
update_wrapper(test2a, test2, ('test_attr',))

test1_result = test1()
";

            var state = CreateAnalyzer();
            var textEntry = state.AddModule("fob", text);
            state.WaitForAnalysis();

            state.AssertConstantEquals("test1.__name__", "test1");
            state.AssertConstantEquals("test1.__doc__", "doc");
            var fi = state.GetValue<FunctionInfo>("test1");
            Assert.AreEqual("doc", fi.Documentation);
            state.GetValue<FunctionInfo>("test1.__wrapped__");
            Assert.AreEqual(2, state.GetValue<FunctionInfo>("test1").Overloads.Count());
            state.AssertConstantEquals("test1_result", "decorated");

            // __name__ should not have been changed by update_wrapper
            state.AssertConstantEquals("test2.__name__", "test2");
            state.AssertConstantEquals("test2a.__name__", "test2a");

            // test_attr should have been copied by update_wrapper
            state.AssertIsInstance("test2.test_attr", BuiltinTypeId.Int);
            state.AssertIsInstance("test2a.test_attr", BuiltinTypeId.Int);
        }

        private static void AssertTupleContains(AnalysisValue tuple, params BuiltinTypeId[] id) {
            var indexTypes = (tuple as SequenceInfo)?.IndexTypes?.Select(v => v.TypesNoCopy).ToArray() ??
                (tuple as ProtocolInfo)?.GetProtocols<TupleProtocol>()?.FirstOrDefault()?._values;
            Assert.IsNotNull(indexTypes);

            var expected = string.Join(", ", id);
            var actual = string.Join(", ", indexTypes.Select(t => {
                if (t.Count == 1) {
                    return t.Single().TypeId.ToString();
                } else {
                    return "{" + string.Join(", ", t.Select(t2 => t2.TypeId).OrderBy(t2 => t2)) + "}";
                }
            }));
            if (indexTypes
                .Zip(id, (t1, id2) => t1.Count == 1 && t1.Single().TypeId == id2)
                .Any(b => !b)) {
                Assert.Fail(string.Format("Expected <{0}>. Actual <{1}>.", expected, actual));
            }
        }


        [TestMethod, Priority(0)]
        public void ValidatePotentialModuleNames() {
            // Validating against the structure given in
            // http://www.python.org/dev/peps/pep-0328/

            var entry = new MockPythonProjectEntry {
                ModuleName = "package.subpackage1.moduleX",
                FilePath = "C:\\package\\subpackage1\\moduleX.py"
            };

            // Without absolute_import, we should see these two possibilities
            // for a regular import.
            AssertUtil.ArrayEquals(
                ModuleResolver.ResolvePotentialModuleNames(entry, "moduleY", false).ToArray(),
                new[] { "package.subpackage1.moduleY", "moduleY" }
            );

            // With absolute_import, we should see the two possibilities for a
            // regular import, but in the opposite order.
            AssertUtil.ArrayEquals(
                ModuleResolver.ResolvePotentialModuleNames(entry, "moduleY", true).ToArray(),
                new[] { "moduleY", "package.subpackage1.moduleY" }
            );

            // Regardless of absolute import, we should see these results for
            // relative imports.
            foreach (var absoluteImport in new[] { true, false }) {
                Console.WriteLine("Testing with absoluteImport = {0}", absoluteImport);

                AssertUtil.ContainsExactly(
                    ModuleResolver.ResolvePotentialModuleNames(entry, ".moduleY", absoluteImport),
                    "package.subpackage1.moduleY"
                );
                AssertUtil.ContainsExactly(
                    ModuleResolver.ResolvePotentialModuleNames(entry, ".", absoluteImport),
                    "package.subpackage1"
                );
                AssertUtil.ContainsExactly(
                    ModuleResolver.ResolvePotentialModuleNames(entry, "..subpackage1", absoluteImport),
                    "package.subpackage1"
                );
                AssertUtil.ContainsExactly(
                    ModuleResolver.ResolvePotentialModuleNames(entry, "..subpackage2.moduleZ", absoluteImport),
                    "package.subpackage2.moduleZ"
                );
                AssertUtil.ContainsExactly(
                    ModuleResolver.ResolvePotentialModuleNames(entry, "..moduleA", absoluteImport),
                    "package.moduleA"
                );

                // Despite what PEP 328 says, this relative import never succeeds.
                AssertUtil.ContainsExactly(
                    ModuleResolver.ResolvePotentialModuleNames(entry, "...package", absoluteImport),
                    "package"
                );
            }
        }

        [TestMethod, Priority(0)]
        public void MultilineFunctionDescription() {
            var code = @"class A:
    def fn(self):
        return lambda: 123
";
            var entry = ProcessText(code);

            Assert.AreEqual(
                "test-module.A.fn(self: A) -> lambda: 123 -> int\ndeclared in A",
                entry.GetDescriptions("A.fn", 0).Single().Replace("\r\n", "\n")
            );
        }

        [TestMethod, Priority(0)]
        public void SysModulesSetSpecialization() {
            var code = @"import sys
modules = sys.modules

modules['name_in_modules'] = None
";
            code += string.Join(
                Environment.NewLine,
                Enumerable.Range(0, 100).Select(i => string.Format("sys.modules['name{0}'] = None", i))
            );

            var entry = ProcessTextV2(code);

            var sys = entry.GetValue<SysModuleInfo>("sys");

            var modules = entry.GetValue<SysModuleInfo.SysModulesDictionaryInfo>("modules");
            Assert.IsInstanceOfType(modules, typeof(SysModuleInfo.SysModulesDictionaryInfo));

            AssertUtil.ContainsExactly(
                sys.Modules.Keys,
                Enumerable.Range(0, 100).Select(i => string.Format("name{0}", i))
                    .Concat(new[] { "name_in_modules" })
            );
        }

        [TestMethod, Priority(0)]
        public void SysModulesGetSpecialization() {
            var code = @"import sys
modules = sys.modules

modules['value_in_modules'] = 'abc'
modules['value_in_modules'] = 123
value_in_modules = modules['value_in_modules']
builtins = modules['__builtin__']
builtins2 = modules.get('__builtin__')
builtins3 = modules.pop('__builtin__')
";

            var entry = ProcessTextV2(code);

            entry.AssertIsInstance("value_in_modules", BuiltinTypeId.Int);

            Assert.AreEqual("__builtin__", entry.GetValue<AnalysisValue>("builtins").Name);
            Assert.AreEqual("__builtin__", entry.GetValue<AnalysisValue>("builtins2").Name);
            Assert.AreEqual("__builtin__", entry.GetValue<AnalysisValue>("builtins3").Name);
        }

        [TestMethod, Priority(0)]
        public void ClassInstanceAttributes() {
            var code = @"
class A:
    abc = 123

p1 = A.abc
p2 = A().abc
a = A()
a.abc = 3.1415
p4 = A().abc
p3 = a.abc
";
            var entry = ProcessText(code);

            entry.AssertIsInstance("p1", BuiltinTypeId.Int);
            entry.AssertIsInstance("p3", BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("p4", BuiltinTypeId.Int, BuiltinTypeId.Float);
            entry.AssertIsInstance("p2", BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void RecursiveGetDescriptor() {
            // see https://pytools.codeplex.com/workitem/2955
            var entry = ProcessText(@"
class WithGet:
    __get__ = WithGet()

class A:
    wg = WithGet()

x = A().wg");

            Assert.IsNotNull(entry);
        }

        [TestMethod, Priority(0)]
        public void Coroutine() {
            var code = @"
async def g():
    return 123

async def f():
    x = await g()
    g2 = g()
    y = await g2
";
            var entry = ProcessText(code, PythonLanguageVersion.V35);

            entry.AssertIsInstance("x", code.IndexOf("x ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("y", code.IndexOf("x ="), BuiltinTypeId.Int);
            entry.AssertIsInstance("g2", code.IndexOf("x ="), BuiltinTypeId.Generator);
        }

        [TestMethod, Priority(0)]
        public void AsyncWithStatement() {
            var text = @"
class X(object):
    def x_method(self): pass
    async def __aenter__(self): return self
    async def __aexit__(self, exc_type, exc_value, traceback): return False

class Y(object):
    def y_method(self): pass
    async def __aenter__(self): return 123
    async def __aexit__(self, exc_type, exc_value, traceback): return False

async def f():
    async with X() as x:
        pass #x

    async with Y() as y:
        pass #y
";
            var entry = ProcessText(text, PythonLanguageVersion.V35);
            entry.AssertHasAttr("x", text.IndexOf("pass #x"), "x_method");
            entry.AssertIsInstance("y", text.IndexOf("pass #y"), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void AsyncForIterator() {
            var code = @"
class X:
    async def __aiter__(self): return self
    async def __anext__(self): return 123

class Y:
    async def __aiter__(self): return X()

async def f():
    async for i in Y():
        pass
";
            var entry = ProcessText(code, PythonLanguageVersion.V35);

            entry.AssertIsInstance("i", code.IndexOf("pass"), BuiltinTypeId.Int);
        }


        [TestMethod, Priority(0)]
        public void RecursiveDecorators() {
            // See https://github.com/Microsoft/PTVS/issues/542
            // Should not crash/OOM
            var code = @"
def f():
    def d(fn):
        @f()
        def g(): pass

    return d
";

            ProcessText(code);
        }

        [TestMethod, Priority(0)]
        public void NullNamedArgument() {
            CallDelegate callable = (node, unit, args, keywordArgNames) => {
                bool anyNull = false;
                Console.WriteLine("fn({0})", string.Join(", ", keywordArgNames.Select(n => {
                    if (n == null) {
                        anyNull = true;
                        return "(null)";
                    } else {
                        return n.Name + "=(value)";
                    }
                })));
                Assert.IsFalse(anyNull, "Some arguments were null");
                return AnalysisSet.Empty;
            };

            using (var state = CreateAnalyzer(allowParseErrors: true)) {
                state.Analyzer.SpecializeFunction("NullNamedArgument", "fn", callable);

                var entry1 = state.AddModule("NullNamedArgument", "def fn(**kwargs): pass");
                var entry2 = state.AddModule("test", "import NullNamedArgument; NullNamedArgument.fn(a=0, ]]])");
                state.WaitForAnalysis();
            }
        }

        [TestMethod, Priority(0)]
        public void ModuleNameWalker() {
            foreach (var item in new[] {
                new { Code="import abc", Index=7, Expected="abc", Base="" },
                new { Code="import abc", Index=8, Expected="abc", Base="" },
                new { Code="import abc", Index=9, Expected="abc", Base="" },
                new { Code="import abc", Index=10, Expected="abc", Base="" },
                new { Code="import deg, abc as A", Index=12, Expected="abc", Base="" },
                new { Code="from abc import A", Index=6, Expected="abc", Base="" },
                new { Code="from .deg import A", Index=9, Expected="deg", Base="abc" },
                new { Code="from .hij import A", Index=9, Expected="abc.hij", Base="abc.deg" },
                new { Code="from ..hij import A", Index=10, Expected="hij", Base="abc.deg" },
                new { Code="from ..hij import A", Index=10, Expected="abc.hij", Base="abc.deg.HIJ" },
            }) {
                var entry = ProcessTextV3(item.Code);
                var walker = new ImportedModuleNameWalker(item.Base, string.Empty, item.Index, null);
                entry.Modules[entry.DefaultModule].Tree.Walk(walker);

                Assert.AreEqual(item.Expected, walker.ImportedModules.FirstOrDefault()?.Name);
            }
        }

        [TestMethod, Priority(0)]
        public void CrossModuleFunctionCallMemLeak() {
            var modA = @"from B import h
def f(x): return h(x)

f(1)";
            var modB = @"def g(x): pass
def h(x): return g(x)";

            var analyzer = CreateAnalyzer();
            var entryA = analyzer.AddModule("A", modA);
            var entryB = analyzer.AddModule("B", modB);
            analyzer.WaitForAnalysis(CancellationTokens.After5s);
            for (int i = 100; i > 0; --i) {
                entryA.Analyze(CancellationToken.None, true);
                analyzer.WaitForAnalysis(CancellationTokens.After5s);
            }
            var g = analyzer.GetValue<FunctionInfo>(entryB, "g");
            Assert.AreEqual(1, g.References.Count());
        }

        [TestMethod, Priority(0)]
        public void DefaultModuleAttributes() {
            var entry3 = ProcessTextV3("x = 1");
            AssertUtil.ContainsExactly(entry3.GetNamesNoBuiltins(), "__builtins__", "__file__", "__name__", "__package__", "__cached__", "__spec__", "x");
            var package = entry3.AddModule("package", "", Path.Combine(TestData.GetTempPath("package"), "__init__.py"));
            AssertUtil.ContainsExactly(entry3.GetNamesNoBuiltins(package), "__path__", "__builtins__", "__file__", "__name__", "__package__", "__cached__", "__spec__");

            entry3.AssertIsInstance("__file__", BuiltinTypeId.Unicode);
            entry3.AssertIsInstance("__name__", BuiltinTypeId.Unicode);
            entry3.AssertIsInstance("__package__", BuiltinTypeId.Unicode);
            entry3.AssertIsInstance(package, "__path__", BuiltinTypeId.List);

            var entry2 = ProcessTextV2("x = 1");
            AssertUtil.ContainsExactly(entry2.GetNamesNoBuiltins(), "__builtins__", "__file__", "__name__", "__package__", "x");

            entry2.AssertIsInstance("__file__", BuiltinTypeId.Bytes);
            entry2.AssertIsInstance("__name__", BuiltinTypeId.Bytes);
            entry2.AssertIsInstance("__package__", BuiltinTypeId.Bytes);
        }

        [TestMethod, Priority(0)]
        public void CrossModuleBaseClasses() {
            var analyzer = CreateAnalyzer();
            var entryA = analyzer.AddModule("A", @"class ClsA(object): pass");
            var entryB = analyzer.AddModule("B", @"from A import ClsA
class ClsB(ClsA): pass

x = ClsB.x");
            analyzer.WaitForAnalysis();
            analyzer.AssertIsInstance(entryB, "x");

            analyzer.UpdateModule(entryA, @"class ClsA(object): x = 123");
            entryA.Analyze(CancellationToken.None, true);
            analyzer.WaitForAnalysis();
            analyzer.AssertIsInstance(entryB, "x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void UndefinedVariableDiagnostic() {
            PythonAnalysis entry;
            string code;


            code = @"a = b + c
class D(b): pass
d()
D()
(e for e in e if e)
{f for f in f if f}
[g for g in g if g]

def func(b, c):
    b, c, d     # b, c are defined here
b, c, d         # but they are undefined here
";
            entry = ProcessTextV3(code);
            entry.AssertDiagnostics(
                "used-before-assignment:unknown variable 'b':(1, 5) - (1, 6)",
                "used-before-assignment:unknown variable 'c':(1, 9) - (1, 10)",
                "used-before-assignment:unknown variable 'b':(2, 9) - (2, 10)",
                "used-before-assignment:unknown variable 'd':(3, 1) - (3, 2)",
                "used-before-assignment:unknown variable 'e':(5, 13) - (5, 14)",
                "used-before-assignment:unknown variable 'f':(6, 13) - (6, 14)",
                "used-before-assignment:unknown variable 'g':(7, 13) - (7, 14)",
                "used-before-assignment:unknown variable 'd':(10, 11) - (10, 12)",
                "used-before-assignment:unknown variable 'b':(11, 1) - (11, 2)",
                "used-before-assignment:unknown variable 'c':(11, 4) - (11, 5)",
                "used-before-assignment:unknown variable 'd':(11, 7) - (11, 8)"
            );

            // Ensure all of these cases correctly generate no warning
            code = @"
for x in []:
    (_ for _ in x)
    [_ for _ in x]
    {_ for _ in x}
    {_ : _ for _ in x}

import sys
from sys import not_a_real_name_but_no_warning_anyway

def f(v = sys.version, u = not_a_real_name_but_no_warning_anyway):
    pass

with f() as v2:
    pass

";
            entry = ProcessTextV3(code);
            entry.AssertDiagnostics();
        }

        [TestMethod, Priority(0)]
        public void UncallableObjectDiagnostic() {
            var code = @"class MyClass:
    pass

class MyCallableClass:
    def __call__(self): return 123

mc = MyClass()
mcc = MyCallableClass()

x = mc()
y = mcc()
";
            var entry = ProcessTextV3(code);
            entry.AssertIsInstance("x");
            entry.AssertIsInstance("y", BuiltinTypeId.Int);
            entry.AssertDiagnostics(
                "not-callable:'MyClass' may not be callable:(10, 5) - (10, 7)"
            );
        }
*/
        [TestMethod, Priority(0)]
        public async Task OsPathMembers() {
            var code = @"import os.path as P
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("P").WithValue<BuiltinModule>()
                    .Which.Should().HaveMembers("abspath", "dirname");
            }
        }

        [TestMethod, Priority(0)]
        public async Task UnassignedClassMembers() {
            var code = @"
from typing import NamedTuple

class Employee(NamedTuple):
    name: str
    id: int = 3

e = Employee('Guido')
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("e").WithValue<IInstanceInfo>()
                    .Which.Should().HaveOnlyMembers("name", "id", "__doc__", "__class__");
            }
        }

        [TestMethod, Priority(0)]
        public async Task CrossModuleUnassignedImport() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable)) {
                // Hack to avoid creation of the real files
                // Project entries are explicitly added to the server before DidOpenTextDocument is called
                var path1 = TestData.GetTestSpecificPath(@"p\__init__.py");
                var path2 = TestData.GetTestSpecificPath(@"p\m.py");
                var uri1 = new Uri(path1);
                var uri2 = new Uri(path2);
                server.ProjectFiles.GetOrAddEntry(uri1, server.Analyzer.AddModule("p", path1, uri1));
                server.ProjectFiles.GetOrAddEntry(uri2, server.Analyzer.AddModule("p.m", path2, uri2));
                // End of hack

                await server.SendDidOpenTextDocument(uri1, "from . import m; m.X; m.Z; W = 1");
                await server.SendDidOpenTextDocument(uri2, "from . import Y, W; Z = 1");

                await server.GetAnalysisAsync(uri1);
                await server.GetAnalysisAsync(uri2);

                var completions = await server.SendCompletion(uri1, 0, 19);
                completions.Should().HaveLabels("Z", "W").And.NotContainLabels("X", "Y");
            }
        }

        #endregion

        #region Helpers
        private async Task<Server> CreateServerAsync(InterpreterConfiguration configuration = null) {
            configuration = configuration ?? PythonVersions.LatestAvailable2X ?? PythonVersions.LatestAvailable3X;
            configuration.AssertInstalled();

            var server = await new Server().InitializeAsync(configuration);
            server.Analyzer.EnableDiagnostics = true;
            server.Analyzer.Limits = GetLimits();

            return server;
        }

        protected virtual AnalysisLimits GetLimits() => AnalysisLimits.GetDefaultLimits(); 


        /// <summary>
        /// Returns all the permutations of the set [0 ... n-1]
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private static IEnumerable<List<int>> Permutations(int n) {
            if (n <= 0) {
                yield return new List<int>();
            } else {
                foreach (var prev in Permutations(n - 1)) {
                    for (int i = n - 1; i >= 0; i--) {
                        var result = new List<int>(prev);
                        result.Insert(i, n - 1);
                        yield return result;
                    }
                }
            }
        }

        /// <summary>
        /// For a given set of module definitions, build analysis info for each unique permutation
        /// of the ordering of the defintions and run the test against each analysis.
        /// </summary>
        /// <param name="prefix">Prefix for the module names. The first source text will become prefix + "1", etc.</param>
        /// <param name="code">The source code for each of the modules</param>
        /// <param name="test">The test to run against the analysis</param>
        private async Task PermutedTestAsync(string prefix, string[] code, Action<IModuleAnalysis[]> test) {
            foreach (var p in Permutations(code.Length)) {
                using (var server = await CreateServerAsync()) {
                    var entries = new ProjectEntry[code.Length];
                    for (var i = 0; i < code.Length; i++) {
                        var name = "{0}{1}".FormatInvariant(prefix, p[i] + 1);
                        var content = code[p[i]];
                        var filename = name.Replace('.', '\\') + ".py";

                        entries[p[i]] = server.AddModuleWithContent(name, filename, content);
                    }

                    var analysises = entries
                        .Select(e => e.GetAnalysisAsync(cancellationToken: new CancellationTokenSource(5000).Token))
                        .ToArray();

                    await Task.WhenAll(analysises);

                    test(analysises.Select(a => a.Result).ToArray());
                    Trace.WriteLine($"--- End Permutation [{string.Join(',',p)}] ---");
                }
            }
        }


        private static string[] GetUnion(params object[] objs) {
            var result = new HashSet<string>();
            foreach (var obj in objs) {
                if (obj is string) {
                    result.Add((string)obj);
                } else if (obj is IEnumerable<string>) {
                    result.UnionWith((IEnumerable<string>)obj);
                } else {
                    throw new NotImplementedException("Non-string member");
                }
            }
            return result.ToArray();
        }

        #endregion
    }
    
    [TestClass]
    public class StdLibAnalysisTest : AnalysisTest {
        protected override AnalysisLimits GetLimits() => AnalysisLimits.GetStandardLibraryLimits();
    }
}
