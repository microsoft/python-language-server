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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.UnitTests.Core.MSTest;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class AnalysisTest : ServerBasedTest {
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
                var fob = await server.AddModuleWithContentAsync("fob", Path.Combine("fob", "__init__.py"), "from oar import *");
                var oar = await server.AddModuleWithContentAsync("fob.oar", Path.Combine("fob", "oar", "__init__.py"), "from .baz import *");
                var baz = await server.AddModuleWithContentAsync("fob.oar.baz", Path.Combine("fob", "oar", "baz.py"), $"import fob.oar.quox as quox{Environment.NewLine}func = quox.func");
                var quox = await server.AddModuleWithContentAsync("fob.oar.quox", Path.Combine("fob", "oar", "quox.py"), "def func(): return 42");

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
        public async Task RecursiveListComprehension() {
            var code = @"
def f(x):
    x = []
    x = [i for i in x]
    x = (i for i in x)
    f(x)
";


            // If we complete processing then we have succeeded
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
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
                    .Which.Should().HaveMember<AstPythonStringLiteral>("platform");

                analysis.Should().HavePythonModuleVariable("a")
                    .Which.Should().HaveMember<AstPythonConstant>("ArrayType");

                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"import sys as s");

                analysis.Should().HavePythonModuleVariable("s")
                    .Which.Should().HaveMember<AstPythonStringLiteral>("platform");
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
                var uri = TestData.GetTestSpecificUri("test_module.py");
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
                await server.SendDidChangeTextDocumentAsync(uri, code);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("i").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("s").OfType(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]

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
from os import *
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
import sys,
            ");
                analysis.Should().HavePythonModuleVariable("sys")
                    .Which.Should().HaveMembers("path");
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
                var uri1 = TestData.GetTestSpecificUri("mod1.py");
                var uri2 = TestData.GetTestSpecificUri("mod2.py");
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uri1, text1);
                    await server.SendDidOpenTextDocument(uri2, text2);
                }

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
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

                await server.SendDidChangeTextDocumentAsync(uri2, text2);
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
            var uri = TestData.GetTestSpecificUri("test_module.py");
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

                await server.SendDidChangeTextDocumentAsync(uri, code);
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

                await server.SendDidChangeTextDocumentAsync(uri, code);
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

                await server.SendDidChangeTextDocumentAsync(uri, code);
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

                await server.SendDidChangeTextDocumentAsync(uri, code);
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

                await server.SendDidChangeTextDocumentAsync(uri, code);
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

        [PermutationalTestMethod(2), Priority(0)]
        public async Task ImportStarMro(int[] permutation) {
            var contents = new[] {
                @"
class Test_test1(object):
    def test_A(self):
        pass
",
                @"from module1 import *

class Test_test2(Test_test1):
    def test_newtest(self):pass"
            };

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysis = await server.GetAnalysisAsync(uris[1]);
                analysis.Should().HaveClassInfo("Test_test2")
                        .WithMethodResolutionOrder("Test_test2", "Test_test1", "type object");
            }
        }

        [TestMethod, Priority(0)]
        public async Task Iterator_Python27() {
            var uri = TestData.GetTestSpecificUri("test_module.py");
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

                await server.SendDidChangeTextDocumentAsync(uri, @"
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

                await server.SendDidChangeTextDocumentAsync(uri, @"
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

                await server.SendDidChangeTextDocumentAsync(uri, @"
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
            var uri = TestData.GetTestSpecificUri("test_module.py");
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

                await server.SendDidChangeTextDocumentAsync(uri, @"
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


        [DataRow(PythonLanguageVersion.V27)]
        [DataRow(PythonLanguageVersion.V35)]
        [DataRow(PythonLanguageVersion.V36)]
        [DataRow(PythonLanguageVersion.V37)]
        [DataRow(PythonLanguageVersion.V38)]
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

            using (var server = await CreateServerAsync(PythonVersions.Required_Python36X)) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
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
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
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

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
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
                new { Method = "matmul", Operator = "@", Version = PythonVersions.Required_Python36X },
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
                    .And.HaveVariable("y1").WithDescription("tuple[int, int, int]")
                    .And.HaveVariable("oar").WithDescription("list[int, int, int]")
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
                    .And.HaveVariable("y1").WithDescription("tuple[int, int, int]")
                    .And.HaveVariable("oar").WithDescription("list[int, int, int]")
                    .And.HaveVariable("oar2").WithDescription("list");
            }
        }

        [TestMethod, Priority(0)]
        public async Task InterableTypesDescription_Long() {
            var text = @"
x1 = (1,'2',3,4.,5,6,7,8)
y1 = 100 * x1

fob = [1,2,'3',4,5.,6,7,8]
oar = 100 * fob
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should()
                    .HaveVariable("y1").WithDescription("tuple[int, str, int, float, int, int, ...]")
                    .And.HaveVariable("oar").WithDescription("list[int, int, str, int, float, int, ...]");
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
                    .WithDescription("tuple[int, int]");

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
                analysis.Should().HaveVariable("iter").OfType(BuiltinTypeId.Tuple).WithDescription("tuple[int, int]")
                    .And.HaveVariable("itm").OfType(BuiltinTypeId.Tuple).WithDescription("tuple[int, int]");
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

                analysis.Should()
                    .HaveVariable("x").WithDescription("set[int]")
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
                    .Which.Should().HaveDescription("bound method get");
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
                analysis.Should().HaveVariable("b").WithDescription("method f of module.C objects");

                await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"
class C(object):
    def f(self):
        'doc string'

a = C()
b = a.f
            ");
                analysis.Should().HaveVariable("b").WithDescription("method f of module.C objects");
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

                signatures.Should().OnlyHaveSignature($"{expectedName}(a, b, c)");
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

                signatures.Should().OnlyHaveSignature($"{expectedName}(a, b, c, *d)");
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

                signatures.Should().OnlyHaveSignature($"{expectedName}(a, b, c, **d)");
            }
        }


        [TestMethod, Priority(0)]
        public async Task Builtins() {
            var code = @"
booltypetrue = True
booltypefalse = False
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("booltypetrue").OfType(BuiltinTypeId.Bool)
                    .And.HaveVariable("booltypefalse").OfType(BuiltinTypeId.Bool);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryFunctionTable() {
            var code = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x['fob'](42, [])
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("a").OfType(BuiltinTypeId.Int)
                    .And.HaveParameter("b").OfType(BuiltinTypeId.List);

                analysis.Should().HaveFunction("g")
                    .Which.Should().HaveParameter("a")
                    .And.HaveParameter("b");
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryFunctionTableGet2() {
            var code = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
x.get('fob')(42, [])
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("a").OfType(BuiltinTypeId.Int)
                    .And.HaveParameter("b").OfType(BuiltinTypeId.List);

                analysis.Should().HaveFunction("g")
                    .Which.Should().HaveParameter("a")
                    .And.HaveParameter("b");
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryAssign() {
            var code = @"
x = {'abc': 42}
y = x['fob']
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryFunctionTableGet() {
            var code = @"
def f(a, b):
    print(a, b)
    
def g(a, b):
    x, y = a, b

x = {'fob': f, 'oar' : g}
y = x.get('fob', None)
if y is not None:
    y(42, [])
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("a").OfType(BuiltinTypeId.Int)
                    .And.HaveParameter("b").OfType(BuiltinTypeId.List);

                analysis.Should().HaveFunction("g")
                    .Which.Should().HaveParameter("a")
                    .And.HaveParameter("b");
            }
        }

        [TestMethod, Priority(0)]
        public async Task FuncCallInIf() {
            var code = @"
def Method(a, b, c):
    print a, b, c
    
if not Method(42, 'abc', []):
    pass
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("Method")
                    .Which.Should().HaveParameter("a").OfType(BuiltinTypeId.Int)
                    .And.HaveParameter("b").OfType(BuiltinTypeId.Str)
                    .And.HaveParameter("c").OfType(BuiltinTypeId.List);
            }
        }

        [TestMethod, Priority(0)]
        public async Task WithStatement() {
            var code = @"
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
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMember<IBoundMethodInfo>("x_method");
            }
        }

        [TestMethod, Priority(0)]
        public async Task OverrideFunction() {
            var code = @"
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
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveClass("oar").WithFunction("Call")
                    .Which.Should().HaveParameter("xvar");
                analysis.Should().HaveClass("baz").WithFunction("Call")
                    .Which.Should().HaveParameter("xvar").OfType(BuiltinTypeId.List);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypingListOfTuples() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var code = @"
from typing import List

def ls() -> List[tuple]:
    pass

x = ls()[0]
";

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis
                    .Should().HaveVariable("ls").Which
                    .Should().HaveShortDescriptions("module.ls() -> list[tuple]");

                analysis
                    .Should().HaveVariable("x").Which
                    .Should().HaveType(BuiltinTypeId.Tuple);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CollectionsNamedTuple() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var code = @"
from collections import namedtuple
nt = namedtuple('Point', ['x', 'y'])
pt = nt(1, 2)
";

                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                server.Analyzer.Limits = new AnalysisLimits { UseTypeStubPackages = true, UseTypeStubPackagesExclusively = false };
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis
                    .Should().HaveVariable("pt").Which
                    .Should().HaveType(BuiltinTypeId.Tuple);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctionOverloads() {
            var code = @"
def f(a, b, c=0):
    pass

f(1, 1, 1)
f(3.14, 3.14, 3.14)
f('a', 'b', 'c')
f(1, 3.14, 'c')
f('a', 'b', 1)
";
            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                var signatures = await server.SendSignatureHelp(uri, 6, 2);

                signatures.Should().OnlyHaveSignature("f(a, b, c: int=0)");
            }
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task DictCtor() {
            var code = @"
d1 = dict({2:3})
x1 = d1[2]

d2 = dict(x = 2)
x2 = d2['x']

d3 = dict(**{2:3})
x3 = d3[2]
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x1").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x2").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x3").OfType(BuiltinTypeId.Int);
            }
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task SpecializedOverride() {
            var code = @"
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
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x1").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x2").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x3").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x4").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("x5").OfType(BuiltinTypeId.Str);
            }
        }

        /// <summary>
        /// https://github.com/Microsoft/PTVS/issues/995
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task SpecializedOverride2() {
            var code = @"
class setdict(dict):
    def __setitem__(self, index):
        pass

a = setdict()
a[42] = 100
b = a[42]
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("b").WithNoTypes();
            }
        }

        /// <summary>
        /// We shouldn't use instance members when invoking special methods
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task GetItemNoInstance() {
            var code = @"
class me(object):
    pass


a = me()
a.__getitem__ = lambda x: 42

for v in a: pass
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("v").WithNoTypes();
            }
        }

        /// <summary>
        /// We shouldn't use instance members when invoking special methods
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task IterNoInstance() {
            var code = @"
class me(object):
    pass


a = me()
a.__iter__ = lambda: (yield 42)

for v in a: pass
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("v").WithNoTypes();
            }
        }

        [TestMethod, Priority(0)]
        public async Task SimpleMethodCall() {
            var code = @"
class x(object):
    def abc(self, fob):
        pass
        
a = x()
a.abc('abc')
";
            using (var server = await CreateServerAsync()) {
                var objectMemberNames = server.GetBuiltinTypeMemberNames(BuiltinTypeId.Object);
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveClass("x").WithFunction("abc")
                    .Which.Should().HaveParameter("fob").OfType(BuiltinTypeId.Str)
                    .And.HaveParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers(objectMemberNames).And.HaveMembers("abc");
            }
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinRetval() {
            var code = @"
x = [2,3,4]
a = x.index(2)
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("a").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinFuncRetval() {
            var code = @"
x = ord('a')
y = range(5)
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.List);
            }
        }

        [TestMethod, Priority(0)]
        public async Task RangeIteration() {
            var code = @"
for i in range(5):
    pass
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("i").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctionMembers2X() {
            var code = @"
def f(x): pass
f.abc = 32
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunctionInfo("f")
                    .Which.Should().HaveMembers("abc");

                code = @"
def f(x): pass

";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunctionInfo("f")
                    .Which.Should().NotHaveMembers("x")
                    .And.HaveMemberOfType("func_name", BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctionMembers3X() {
            var code = @"
def f(x): pass
f.abc = 32
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunctionInfo("f")
                    .Which.Should().HaveMembers("abc");

                code = @"
def f(x): pass

";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunctionInfo("f")
                    .Which.Should().NotHaveMembers("x")
                    .And.HaveMemberOfType("__name__", BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImport() {
            var code = @"
import sys
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HavePythonModuleVariable("sys")
                    .Which.Should().HaveMembers("platform");
            }
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImportInFunc() {
            var code = @"
def f():
    import sys
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunction("f")
                    .Which.Should().HavePythonModuleVariable("sys")
                    .Which.Should().HaveMembers("platform");
            }
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImportInClass() {
            var code = @"
class C:
    import sys
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveClass("C")
                    .Which.Should().HavePythonModuleVariable("sys")
                    .Which.Should().HaveMembers("platform");
            }
        }

        [TestMethod, Priority(0)]
        public async Task NoImportClr() {
            var code = @"
x = 'abc'
";
            using (var server = await CreateServerAsync()) {
                var stringMemberNames = server.GetBuiltinTypeMemberNames(BuiltinTypeId.Str);
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str).WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveOnlyMembers(stringMemberNames);
            }
        }

        [TestMethod, Priority(0)]
        public async Task MutualRecursion() {
            var code = @"
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
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Str)
                    .And.HaveClass("C").WithFunction("f")
                    .Which.Should().HaveParameter("other").WithValue<IInstanceInfo>()
                    .Which.Should().HaveOnlyMembers("g", "__doc__", "__class__");
            }
        }

        [TestMethod, Priority(0)]
        public async Task MutualGeneratorRecursion() {
            var code = @"
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
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DistinctGenerators() {
            var code = @"
def f(x):
    return x

def g(x):
    yield f(x)

class S0(object): pass
it = g(S0())
val = next(it)

" + string.Join(Environment.NewLine, Enumerable.Range(1, 100).Select(i => string.Format("class S{0}(object): pass{1}f(S{0}())", i, Environment.NewLine)));
            Console.WriteLine(code);

            // Ensure the returned generators are distinct
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("it").OfType(BuiltinTypeId.Generator)
                    .And.HaveVariable("val").OfType("S0");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRefVars() {
            var code = @"
class x(object):
    def __init__(self, val):
        self.abc = [val]
    
x(42)
x('abc')
x([])
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveClass("x").WithFunction("__init__")
                    .Which.Should().HaveParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMember<ListInfo>("abc");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReturnFunc() {
            var code = @"
def g():
    return []

def f():
    return g
    
x = f()()
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReturnArg() {
            var code = @"
def g(a):
    return a

x = g(1)
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReturnArg2() {
            var code = @"

def f(a):
    def g():
        return a
    return g

x = f(2)()
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign() {
            var code = @"
class C:
    def func(self):
        self.abc = 42

a = C()
a.func()
fob = a.abc
";
            using (var server = await CreateServerAsync()) {
                var intMemberNames = server.GetBuiltinTypeMemberNames(BuiltinTypeId.Int);
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.Int).WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveOnlyMembers(intMemberNames);
                analysis.Should().HaveVariable("a").WithValue<IInstanceInfo>()
                    .Which.Should().HaveOnlyMembers("abc", "func", "__doc__", "__class__");

            }
        }

        [TestMethod, Priority(0)]
        public async Task MemberAssign2() {
            var code = @"
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
            using (var server = await CreateServerAsync()) {
                var listMemberNames = server.GetBuiltinTypeMemberNames(BuiltinTypeId.List);
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.List).WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveOnlyMembers(listMemberNames);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AnnotatedAssign() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var code = @"
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
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("fob1").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("fob2").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("fob3").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("a").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers("abc", "func", "y", "__doc__", "__class__");

                code = @"
def f(val):
    print(val)

class C:
    def __init__(self, y):
        self.y = y

x:f(42) = 1
x:C(42) = 1
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f").WithParameter("val").OfType(BuiltinTypeId.Int)
                    .And.HaveClass("C").WithFunction("__init__").WithParameter("y").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task UnfinishedDot() {
            // the partial dot should be ignored and we shouldn't see g as
            // a member of D
            var code = @"
class D(object):
    def func(self):
        self.
        
def g(a, b, c): pass
";
            using (var server = await CreateServerAsync()) {
                var objectMemberNames = server.GetBuiltinTypeMemberNames(BuiltinTypeId.Object);
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveClass("D").WithFunction("func").WithParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers(objectMemberNames)
                    .And.HaveMembers("func");
            }
        }

        [PermutationalTestMethod(2), Priority(0)]
        public async Task CrossModule(int[] permutation) {
            var contents = new[] { "import module2", "x = 42" };

            using (var server = await CreateServerAsync()) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }
                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

                var analysis = await server.GetAnalysisAsync(uris[0]);
                analysis.Should().HaveVariable("module2").WithValue<IModuleInfo>()
                    .Which.Should().HaveMembers("x");
            }
        }

        [PermutationalTestMethod(2), Priority(0)]
        public async Task CrossModuleCall(int[] permutation) {
            var contents = new[] { @"
import module2
y = module2.f('abc')
",
                @"
def f(x):
    return x
" };

            using (var server = await CreateServerAsync()) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                var analysis1 = await server.GetAnalysisAsync(uris[0]);
                var analysis2 = await server.GetAnalysisAsync(uris[1]);

                analysis2.Should().HaveFunction("f").WithParameter("x").OfType(BuiltinTypeId.Str);
                analysis1.Should().HaveVariable("y").OfType(BuiltinTypeId.Str);
            }
        }

        [PermutationalTestMethod(2), Priority(0)]
        public async Task CrossModuleCallType(int[] permutation) {
            var contents = new[] { @"
import module2
y = module2.c('abc').x
",
                @"
class c:
    def __init__(self, x):
        self.x = x
" };

            using (var server = await CreateServerAsync()) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                var analysis1 = await server.GetAnalysisAsync(uris[0]);
                var analysis2 = await server.GetAnalysisAsync(uris[1]);

                analysis2.Should().HaveClass("c").WithFunction("__init__").WithParameter("x").OfType(BuiltinTypeId.Str);
                analysis1.Should().HaveVariable("y").OfType(BuiltinTypeId.Str);
            }
        }

        [PermutationalTestMethod(2), Priority(0)]
        public async Task CrossModuleCallType2(int[] permutation) {
            var contents = new[] {@"
from module2 import c
class x(object):
    def Fob(self):
        y = c('abc').x
",
                @"
class c:
    def __init__(self, x):
        self.x = x
"
            };

            using (var server = await CreateServerAsync()) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                var analysis1 = await server.GetAnalysisAsync(uris[0]);
                var analysis2 = await server.GetAnalysisAsync(uris[1]);

                analysis2.Should().HaveClass("c").WithFunction("__init__").WithParameter("x").OfType(BuiltinTypeId.Str);
                analysis1.Should().HaveClass("x").WithFunction("Fob").WithVariable("y").OfType(BuiltinTypeId.Str);
            }
        }

        [PermutationalTestMethod(3), Priority(0)]
        public async Task CrossModuleFuncAndType(int[] permutation) {
            using (var server = await CreateServerAsync()) {
                var contents = new[] {
                    @"
class Something(object):
    def f(self): pass
    def g(self): pass


def SomeFunc():
    x = Something()
    return x
",
                    @"
from module1 import SomeFunc

x = SomeFunc()
",
                    @"
from module2 import x
a = x
"              };

                var uris = TestData.GetNextModuleUris(3);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                    await server.SendDidOpenTextDocument(uris[permutation[2]], contents[permutation[2]]);
                }

                await server.GetAnalysisAsync(uris[0]);
                await server.GetAnalysisAsync(uris[1]);
                var analysis = await server.GetAnalysisAsync(uris[2]);

                var objectMemberNames = analysis.ProjectState.Types[BuiltinTypeId.Object].GetMemberNames(analysis.InterpreterContext);
                analysis.Should().HaveVariable("a").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers(objectMemberNames)
                    .And.HaveMembers("f", "g");
            }
        }

        [TestMethod, Priority(0)]
        public async Task MembersAfterError() {
            var code = @"
class X(object):
    def f(self):
        return self.
        
    def g(self):
        pass
        
    def h(self):
        pass
";
            using (var server = await CreateServerAsync()) {
                var objectMemberNames = server.GetBuiltinTypeMemberNames(BuiltinTypeId.Object);
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveClass("X").WithFunction("f").WithParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers(objectMemberNames)
                    .And.HaveMembers("f", "g", "h");
            }
        }

        [TestMethod, Priority(0)]
        public async Task Property() {
            var code = @"
class x(object):
    @property
    def SomeProp(self):
        return 42

a = x().SomeProp
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task StaticMethod() {
            var code = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

a = x().StaticMethod(4.0)
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task InheritedStaticMethod() {
            var code = @"
class x(object):
    @staticmethod
    def StaticMethod(value):
        return value

class y(x):
    pass

a = y().StaticMethod(4.0)
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethod() {
            var code = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

a = x().ClassMethod()
b = x.ClassMethod()
";
            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                var analysis = await server.GetAnalysisAsync(uri);
                var signature1 = await server.SendSignatureHelp(uri, 6, 20);
                var signature2 = await server.SendSignatureHelp(uri, 7, 18);

                analysis.Should().HaveVariable("a").WithDescription("x")
                    .And.HaveVariable("b").WithDescription("x")
                    .And.HaveClass("x").WithFunction("ClassMethod").WithParameter("cls").WithDescription("x");
                signature1.Should().HaveSingleSignature()
                    .Which.Should().HaveNoParameters();
                signature2.Should().HaveSingleSignature()
                    .Which.Should().HaveNoParameters();
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethod2() {
            var code = @"
class x(object):
    @classmethod
    def UncalledClassMethod(cls):
        return cls
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveClass("x").WithFunction("UncalledClassMethod").WithParameter("cls").WithDescription("x");
            }
        }

        [TestMethod, Priority(0)]
        public async Task InheritedClassMethod() {
            var code = @"
class x(object):
    @classmethod
    def ClassMethod(cls):
        return cls

class y(x):
    pass

a = y().ClassMethod()
b = y.ClassMethod()
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                var analysis = await server.GetAnalysisAsync(uri);
                var signature1 = await server.SendSignatureHelp(uri, 9, 20);
                var signature2 = await server.SendSignatureHelp(uri, 10, 18);

                analysis.Should().HaveVariable("a").WithShortDescriptions("x", "y")
                    .And.HaveVariable("b").WithShortDescriptions("x", "y")
                    .And.HaveClass("x").WithFunction("ClassMethod").WithParameter("cls").WithShortDescriptions("x", "y");
                signature1.Should().HaveSingleSignature()
                    .Which.Should().HaveNoParameters();
                signature2.Should().HaveSingleSignature()
                    .Which.Should().HaveNoParameters();
            }
        }

        [TestMethod, Priority(0)]
        public async Task UserDescriptor() {
            var code = @"
class mydesc(object):
    def __get__(self, inst, ctx):
        return 42

class C(object):
    x = mydesc()

fob = C.x
oar = C().x
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("fob").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("oar").OfType(BuiltinTypeId.Int)
                    .And.HaveClass("mydesc").WithFunction("__get__")
                    .Which.Should().HaveParameter("ctx").OfType(BuiltinTypeId.Type)
                    .And.HaveParameter("inst").OfTypes("None", "C");
            }
        }

        [TestMethod, Priority(0)]
        public async Task UserDescriptor2() {
            var content = @"
class mydesc(object):
    def __get__(self, inst, ctx):
        return 42

class C(object):
    x = mydesc()
    def instfunc(self):
        pass

oar = C().x
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(content);

                analysis.Should().HaveClass("mydesc").WithFunction("__get__").WithParameter("inst").OfType("C").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers("instfunc");
            }
        }

        [TestMethod, Priority(0)]
        public async Task AssignSelf() {
            var content = @"
class x(object):
    def __init__(self):
        self.x = 'abc'
    def f(self):
        pass
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(content);

                analysis.Should().HaveClass("x").WithFunction("f").WithParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfType("x", BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AssignToMissingMember() {
            var content = @"
class test():
    x = 0;
    y = 1;
t = test()
t.x, t. =
";
            // http://pytools.codeplex.com/workitem/733

            // this just shouldn't crash, we should handle the malformed code, not much to inspect afterwards...
            using (var server = await CreateServerAsync()) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(content);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CancelAnalysis() {
            var configuration = PythonVersions.LatestAvailable;
            if (configuration == null) {
                Assert.Inconclusive("Test requires Python installation");
            }

            var files = Directory.GetFiles(configuration.LibraryPath, "*.py", SearchOption.AllDirectories);
            Trace.TraceInformation($"Files count: {files}");
            var contentTasks = files.Take(50).Select(f => File.ReadAllTextAsync(f)).ToArray();

            await Task.WhenAll(contentTasks);

            Server server = null;
            var serverDisposeTask = Task.CompletedTask;
            var analysisCompleteTask = Task.CompletedTask;
            try {
                server = await CreateServerAsync(configuration, new Uri(configuration.LibraryPath));
                for (var i = 0; i < contentTasks.Length && server.AnalysisQueue.Count < 50; i++) {
                    await server.SendDidOpenTextDocument(new Uri(files[i]), contentTasks[i].Result);
                }

                server.AnalysisQueue.Count.Should().NotBe(0);
            } finally {
                if (server != null) {
                    analysisCompleteTask = EventTaskSources.AnalysisQueue.AnalysisComplete.Create(server.AnalysisQueue, new CancellationTokenSource(10000).Token);
                    serverDisposeTask = Task.WhenAny(Task.Run(() => server.Dispose()), Task.Delay(1000));
                }
            }

            await serverDisposeTask;
            server.Should().NotBeNull();
            server.AnalysisQueue.Count.Should().Be(0);

            await analysisCompleteTask;
        }

        [TestMethod, Priority(0)]
        public async Task Package() {
            var srcX = @"
from fob.y import abc
import fob.y as y
";

            var srcY = @"
abc = 42
";

            using (var server = await CreateServerAsync(rootUri: TestData.GetTestSpecificRootUri())) {
                var initX = await TestData.CreateTestSpecificFileAsync(Path.Combine("fob", "__init__.py"), string.Empty);
                var modX = TestData.GetTestSpecificUri(Path.Combine("fob", "x.py"));
                var modY = await TestData.CreateTestSpecificFileAsync(Path.Combine("fob", "y.py"), srcY);
                
                using (server.AnalysisQueue.Pause()) {
                    await server.LoadFileAsync(initX);
                    await server.SendDidOpenTextDocument(modX, srcX);
                    await server.LoadFileAsync(modY);
                }

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysis = await server.GetAnalysisAsync(modX);

                analysis.Should()
                    .HaveVariable("y").WithDescription("Python module fob.y")
                    .And.HaveVariable("abc").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task PackageRelativeImport() {
            var src1 = "from .y import abc";
            var src2 = "from .y import abc";
            var src3 = "abc = 42";

            using (var server = await CreateServerAsync(rootUri: TestData.GetTestSpecificRootUri())) {
                var uriSrc1 = await TestData.CreateTestSpecificFileAsync(Path.Combine("fob", "__init__.py"), src1);
                var uriSrc2 = await TestData.CreateTestSpecificFileAsync(Path.Combine("fob", "x.py"), src2);
                var uriSrc3 = await TestData.CreateTestSpecificFileAsync(Path.Combine("fob", "y.py"), src3);

                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uriSrc1, src1);
                    await server.SendDidOpenTextDocument(uriSrc2, src2);
                    await server.SendDidOpenTextDocument(uriSrc3, src3);
                }

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysisPackage = await server.GetAnalysisAsync(uriSrc1);
                var analysisX = await server.GetAnalysisAsync(uriSrc2);

                analysisPackage.Should().HaveVariable("abc").OfType(BuiltinTypeId.Int);
                analysisX.Should().HaveVariable("abc").OfType(BuiltinTypeId.Int);
            }
        }

        [DataRow("from .moduleY import spam", "spam", null)]
        [DataRow("from .moduleY import spam as ham", "ham", null)]
        [DataRow("from . import moduleY", "moduleY", "spam")]
        [DataRow("from ..subpackage1 import moduleY", "moduleY", "spam")]
        [DataRow("from ..subpackage2.moduleZ import eggs", "eggs", null)]
        [DataRow("from ..moduleA import foo", "foo", null)]
        [DataRow("from ...package import bar", "bar", null)]
        [DataTestMethod, Priority(0)]
        public async Task PackageRelativeImportPep328(string subpackageModuleXContent, string variable, string member) {
            var initContent = "def bar():\n  pass\n";
            var moduleAContent = "def foo():\n  pass\n";
            var subpackageModuleYContent = "def spam():\n  pass\n";
            var subpackage2ModuleZContent = "def eggs():\n  pass\n";

            using (var server = await CreateServerAsync(rootUri: TestData.GetTestSpecificRootUri())) {
                var initUri = await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "__init__.py"), initContent);
                var moduleAUri = await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "moduleA.py"), moduleAContent);

                var subpackageInitUri = await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "subpackage1", "__init__.py"), string.Empty);
                var subpackageModuleXUri = await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "subpackage1", "moduleX.py"), subpackageModuleXContent);
                var subpackageModuleYUri = await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "subpackage1", "moduleY.py"), subpackageModuleYContent);

                var subpackage2InitUri = await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "subpackage2", "__init__.py"), string.Empty);
                var subpackage2ModuleZUri = await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "subpackage2", "moduleZ.py"), subpackage2ModuleZContent);

                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(initUri, initContent);
                    await server.SendDidOpenTextDocument(moduleAUri, moduleAContent);
                    await server.SendDidOpenTextDocument(subpackageInitUri, string.Empty);
                    await server.SendDidOpenTextDocument(subpackageModuleXUri, subpackageModuleXContent);
                    await server.SendDidOpenTextDocument(subpackageModuleYUri, subpackageModuleYContent);
                    await server.SendDidOpenTextDocument(subpackage2InitUri, string.Empty);
                    await server.SendDidOpenTextDocument(subpackage2ModuleZUri, subpackage2ModuleZContent);
                }

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysisX = await server.GetAnalysisAsync(subpackageModuleXUri);

                if (member != null) {
                    analysisX.Should().HaveVariable(variable).WithValue<IModule>()
                        .WithMemberOfType(member, PythonMemberType.Function);
                } else {
                    analysisX.Should().HaveVariable(variable).OfType(BuiltinTypeId.Function);
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task PackageRelativeImportAliasedMember() {
            // similar to unittest package which has unittest.main which contains a function called "main".
            // Make sure we see the function, not the module.
            var src1 = "from .y import y";
            var src2 = "def y(): pass";

            using (var server = await CreateServerAsync(rootUri: TestData.GetTestSpecificRootUri())) {
                var uriSrc1 = await TestData.CreateTestSpecificFileAsync(Path.Combine("fob", "__init__.py"), src1);
                var uriSrc2 = await TestData.CreateTestSpecificFileAsync(Path.Combine("fob", "y.py"), src2);

                await server.SendDidOpenTextDocument(uriSrc1, src1);
                await server.SendDidOpenTextDocument(uriSrc2, src2);

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysisPackage = await server.GetAnalysisAsync(uriSrc1);

                analysisPackage.Should().HaveVariable("y").OfTypes(BuiltinTypeId.Module, BuiltinTypeId.Function);
            }
        }

        [TestMethod, Priority(0)]
        public async Task GenericListBase() {
            var code = @"
from typing import List

def func(a: List[str]):
    pass
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis
                    .Should().HaveFunction("func").Which
                    .Should().HaveVariable("a")
                    .OfTypes(BuiltinTypeId.List, BuiltinTypeId.Unknown); // list and 'function argument'
            }
        }

        [TestMethod, Priority(0)]
        public async Task GenericDictBase() {
            var code = @"
from typing import Dict

def func(a: Dict[int, str]):
    pass
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis
                    .Should().HaveFunction("func").Which
                    .Should().HaveVariable("a")
                    .OfTypes(BuiltinTypeId.Dict, BuiltinTypeId.Unknown); // dict and 'function argument'
            }
        }

        [TestMethod, Priority(0)]
        public async Task NewType() {
            var code = @"
from typing import NewType

Foo = NewType('Foo', dict)
foo: Foo = Foo({ })
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis
                    .Should().HaveVariable("Foo")
                    .OfType(BuiltinTypeId.Type).WithDescription("Foo")
                    .And.HaveVariable("foo")
                    .OfType(BuiltinTypeId.Type).WithDescription("Foo");
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeVar() {
            var code = @"
from typing import Sequence, TypeVar

T = TypeVar('T') # Declare type variable

def first(l: Sequence[T]) -> T: # Generic function
    return l[0]

arr = [1, 2, 3]
x = first(arr)  # should be int
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis
                    .Should().HaveVariable("x")
                    .OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Type); // int and T
            }
        }

        [TestMethod, Priority(0)]
        public async Task Defaults() {
            var text = @"
def f(x = 42):
    return x

a = f()
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int);
            }
        }

        [PermutationalTestMethod(2), Priority(0)]
        public async Task Decorator(int[] permutation) {
            var contents = new[] { @"
import module2

inst = module2.MyClass()

@inst.mydec
def f():
    return 42


",
                @"
import module1

class MyClass(object):
    def mydec(self, x):
        return x

g = MyClass().mydec(module1.f)
" };


            using (var server = await CreateServerAsync()) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysis1 = await server.GetAnalysisAsync(uris[0]);
                var analysis2 = await server.GetAnalysisAsync(uris[1]);

                analysis1.Should().HaveFunction("f");
                analysis2.Should().HaveVariable("g").OfType(BuiltinTypeId.Function)
                    .And.HaveVariable("module1").WithValue<IModuleInfo>()
                    .Which.Should().HaveMemberOfType("f", BuiltinTypeId.Function);
            }
        }

        [PermutationalTestMethod(2), Priority(0)]
        public async Task DecoratorFlow(int[] permutation) {
            var contents = new[] { @"
import module2

inst = module2.MyClass()

@inst.filter(fob=42)
def f():
    return 42

",
                @"
import module1

class MyClass(object):
    def filter(self, name=None, filter_func=None, **flags):
        # @register.filter()
        def dec(func):
            return self.filter_function(func, **flags)
        return dec
    def filter_function(self, func, **flags):
        name = getattr(func, ""_decorated_function"", func).__name__
        res = self.filter(name, func, **flags)
        return res
" };

            using (var server = await CreateServerAsync()) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                var analysis2 = await server.GetAnalysisAsync(uris[1]);
                var analysis1 = await server.GetAnalysisAsync(uris[0]);

                // Ensure we ended up with a function
                analysis1.Should().HaveVariable("f").OfTypes(BuiltinTypeId.Function, BuiltinTypeId.Function);

                // Ensure we passed a function in to the decorator (def dec(func))
                analysis2.Should().HaveClass("MyClass")
                    .WithFunction("filter")
                    .WithVariable("dec")
                    .OfType(BuiltinTypeId.Function);

                // Ensure we saw the function passed *through* the decorator
                analysis2.Should().HaveClass("MyClass")
                    .WithFunction("filter_function")
                    .WithVariable("res")
                    .OfType(BuiltinTypeId.Function);

                // Ensure we saw the function passed *back into* the original decorator constructor
                analysis2.Should().HaveClass("MyClass")
                    .WithFunction("filter")
                    .WithParameter("filter_func")
                    .OfTypes(BuiltinTypeId.Function, BuiltinTypeId.NoneType);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorTypes() {
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
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Tuple)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("z").OfType(BuiltinTypeId.Float)
                    .And.HaveVariable("w").OfType(BuiltinTypeId.Str);

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
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("items").OfType(BuiltinTypeId.Function)
                    .And.HaveVariable("items2").OfType(BuiltinTypeId.Function)
                    .And.HaveVariable("x").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Set, BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorReturnTypes_NoDecorator() {
            // https://pytools.codeplex.com/workitem/1694
            var text = @"# without decorator
def returnsGiven(parm):
    return parm

retGivenInt = returnsGiven(1)
retGivenString = returnsGiven('str')
retGivenBool = returnsGiven(True)
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("retGivenInt").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("retGivenString").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("retGivenBool").OfType(BuiltinTypeId.Bool);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorReturnTypes_DecoratorNoParams() {
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

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("retGivenInt").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("retGivenString").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("retGivenBool").OfType(BuiltinTypeId.Bool);
            }
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorReturnTypes_DecoratorWithParams() {
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

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("retGivenInt").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("retGivenString").OfType(BuiltinTypeId.Str)
                    .And.HaveVariable("retGivenBool").OfType(BuiltinTypeId.Bool);
            }
        }

        [PermutationalTestMethod(2), Priority(0)]
        public async Task DecoratorOverflow(int[] permutation) {
            var contents = new[] { @"
import mod2

@mod2.decorator_b
def decorator_a(fn):
    return fn


",
            @"
import mod1

@mod1.decorator_a
def decorator_b(fn):
    return fn
"};

            using (var server = await CreateServerAsync()) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                var analysis1 = await server.GetAnalysisAsync(uris[0]);
                var analysis2 = await server.GetAnalysisAsync(uris[1]);

                analysis1.Should().HaveVariable("decorator_a").OfType(BuiltinTypeId.Function);
                analysis2.Should().HaveVariable("decorator_b").OfType(BuiltinTypeId.Function);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";

            using (var server = await CreateServerAsync()) {
                server.Analyzer.Limits.ProcessCustomDecorators = true;

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("my_fn").OfType(BuiltinTypeId.List)
                    .And.HaveFunction("d").WithParameter("fn").OfType(BuiltinTypeId.Function);
            }
        }

        [TestMethod, Priority(0)]
        public async Task NoProcessDecorators() {
            var text = @"
def d(fn):
    return []

@d
def my_fn():
    return None
";


            using (var server = await CreateServerAsync()) {
                server.Analyzer.Limits.ProcessCustomDecorators = false;

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("my_fn").OfType(BuiltinTypeId.Function)
                    .And.HaveFunction("d").WithParameter("fn").OfType(BuiltinTypeId.Function);
            }
        }


        [TestMethod, Priority(0)]
        public async Task DecoratorClass() {
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
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("mc1").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers("base_method", "sub_method");
                analysis.Should().HaveVariable("mc2").OfType("MySubClass").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers("sub_method");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassInit() {
            var text = @"
class X:
    def __init__(self, value):
        self.value = value

a = X(2)
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("a").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfType("value", BuiltinTypeId.Int);
                analysis.Should().HaveClass("X").WithFunction("__init__").WithParameter("self").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfType("value", BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task InstanceCall() {
            var text = @"
class X:
    def __call__(self, value):
        return value

x = X()

a = x(2)
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int);
            }
        }

        /// <summary>
        /// Verifies that regardless of how we get to imports/function return values that
        /// we properly understand the imported value.
        /// </summary>
        [PermutationalTestMethod(2), Priority(0)]
        public async Task ImportScopesOrder(int[] permutation) {
            var contents = new[] { @"
import _io
import module2
import mmap as mm

import sys
def f():
    return sys

def g():
    return _io

def h():
    return module2.sys

def i():
    import zlib
    return zlib

def j():
    return mm

def k():
    return module2.impp

import operator as op

import re

",
                @"
import sys
import imp as impp
" };
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uris = TestData.GetNextModuleUris(2);
                using (server.AnalysisQueue.Pause()) {
                    await server.SendDidOpenTextDocument(uris[permutation[0]], contents[permutation[0]]);
                    await server.SendDidOpenTextDocument(uris[permutation[1]], contents[permutation[1]]);
                }

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysis = await server.GetAnalysisAsync(uris[0]);

                analysis.Should().HaveVariable("g").WithDescription("module1.g() -> _io")
                    .And.HaveVariable("f").WithDescription("module1.f() -> sys")
                    .And.HaveVariable("h").WithDescription("module1.h() -> sys")
                    .And.HaveVariable("i").WithDescription("module1.i() -> zlib")
                    .And.HaveVariable("j").WithDescription("module1.j() -> mmap")
                    .And.HaveVariable("k").WithDescription("module1.k() -> imp");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassNew() {
            var text = @"
class X:
    def __new__(cls, value):
        res = object.__new__(cls)
        res.value = value
        return res

a = X(2)
";
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);

                analysis.Should().HaveClass("X").WithFunction("__new__")
                    .Which.Should().HaveParameter("cls").WithDescription("X")
                    .And.HaveParameter("value").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("res").OfType("X");

                analysis.Should().HaveVariable("a").OfType("X").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfType("value", BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Global() {
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

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("a").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)
                    .And.HaveVariable("x").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IsInstance() {
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

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var hoverInTrue = await server.SendHover(uri, 6, 22);
                var hoverInFalse = await server.SendHover(uri, 11, 22);
                var hover = await server.SendHover(uri, 19, 14);
                var referencesX1 = await server.SendFindReferences(uri, 1, 0);
                var referencesX2 = await server.SendFindReferences(uri, 6, 22);
                var referencesX3 = await server.SendFindReferences(uri, 11, 22);
                var referencesX4 = await server.SendFindReferences(uri, 19, 14);
                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Tuple);
                hoverInTrue.Should().HaveTypeName("int");
                hoverInFalse.Should().HaveTypeName("str");
                hover.Should().HaveTypeName("tuple");

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri, (1, 0, 1, 1), ReferenceKind.Definition),
                    (uri, (6, 22, 6, 23), ReferenceKind.Reference),
                    (uri, (11, 22, 11, 23), ReferenceKind.Reference),
                    (uri, (19, 14, 19, 15), ReferenceKind.Reference)
                };

                referencesX1.Should().OnlyHaveReferences(expectedReferences);
                referencesX2.Should().OnlyHaveReferences(expectedReferences);
                referencesX3.Should().OnlyHaveReferences(expectedReferences);
                referencesX4.Should().OnlyHaveReferences(expectedReferences);

                text = @"x = None


if True:
    pass
    assert isinstance(x, int)
    z = 100

    pass

print(z)";

                await server.SendDidChangeTextDocumentAsync(uri, text);
                var referencesZ1 = await server.SendFindReferences(uri, 6, 4);
                var referencesZ2 = await server.SendFindReferences(uri, 10, 6);
                analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)
                    .And.HaveVariable("z").OfType(BuiltinTypeId.Int);

                expectedReferences = new (Uri, (int, int, int, int) range, ReferenceKind?)[] {
                    (uri, (6, 4, 6, 5), ReferenceKind.Definition),
                    (uri, (10, 6, 10, 7), ReferenceKind.Reference)
                };

                referencesZ1.Should().OnlyHaveReferences(expectedReferences);
                referencesZ2.Should().OnlyHaveReferences(expectedReferences);

                // http://pytools.codeplex.com/workitem/636
                // this just shouldn't crash, we should handle the malformed code, not much to inspect afterwards...
                await server.ChangeDefaultDocumentAndGetAnalysisAsync($"if isinstance(x, list):{Environment.NewLine}");
                await server.ChangeDefaultDocumentAndGetAnalysisAsync("if isinstance(x, list):");
            }
        }

        [TestMethod, Priority(0)]
        public async Task IsInstance3X() {
            var text = @"
def f(a):
    def g():
        nonlocal a
        print(a)
        assert isinstance(a, int)
                            
        pass

f('abc')
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var hover = await server.SendHover(uri, 5, 26);
                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("a").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Unicode)
                    .And.HaveFunction("g").WithVariable("a").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Unicode);

                hover.Should().HaveTypeName("int");
            }
        }

        [TestMethod, Priority(0)]
        public async Task NestedIsInstance() {
            var code = @"
def f():
    x = None
    y = None

    assert isinstance(x, int)
    z = x

    assert isinstance(y, int)
    w = y

    pass";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveVariable("z").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("w").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task NestedIsInstance1908() {
            // https://pytools.codeplex.com/workitem/1908
            var code = @"
def f(x):
    y = object()    
    assert isinstance(x, int)
    if isinstance(y, float):
        print('hi')

    pass
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveFunction("f").WithVariable("y").OfTypes(BuiltinTypeId.Object, BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IsInstanceUserDefinedType() {
            var text = @"
class C(object):
    def f(self):
        pass

def f(a):
    assert isinstance(a, C)
    print(a)
    pass
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveFunction("f").WithParameter("a").OfType("C");
            }
        }

        [TestMethod, Priority(0)]
        public async Task IsInstanceNested() {
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

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);

                analysis.Should().HaveVariable("r1").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfType("a", BuiltinTypeId.Str)
                    .And.HaveMemberOfTypes("b", BuiltinTypeId.Type, BuiltinTypeId.Tuple)
                    .And.HaveMemberOfType("c", BuiltinTypeId.Str);

                analysis.Should().HaveVariable("r2").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMemberOfType("a", BuiltinTypeId.Str)
                    .And.HaveMemberOfTypes("b", BuiltinTypeId.Type, BuiltinTypeId.Tuple)
                    .And.HaveMemberOfType("c", BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IsInstanceAndLambdaScopes() {
            // https://github.com/Microsoft/PTVS/issues/2801
            var text = @"if isinstance(p, dict):
    v = [i for i in (lambda x: x)()]";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Scope.Should().HaveChildScopeAt<StatementScope>(0)
                    .And.HaveChildScopeAt<StatementScope>(2)
                    .And.HaveChildScopeAt<IsInstanceScope>(1)
                    .Which.Should().OnlyHaveChildScope<ComprehensionScope>()
                    .Which.Should().OnlyHaveChildScope<FunctionScope>()
                    .Which.Should().OnlyHaveChildScope<StatementScope>();
            }
        }

        [TestMethod, Priority(0)]
        public async Task QuickInfo() {
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

m = min
list_append = list.append
abc_length = ""abc"".Length
c_length = c.Length
fn = f.func_name
fob_g = fob().g
none = None
rf = return_func_class().return_func
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);

                analysis.Should().HaveClass("fob").WithVariable("f").WithDescription($"module.fob.f(self: fob){Environment.NewLine}declared in fob")
                    .And.HaveVariable("a").OfType(BuiltinTypeId.Float).WithDescription("float")
                    .And.HaveVariable("b").OfType(BuiltinTypeId.Long).WithDescription("long")
                    .And.HaveVariable("c").OfType(BuiltinTypeId.Str).WithDescription("str")
                    .And.HaveVariable("x").OfType(BuiltinTypeId.Tuple)
                    .And.HaveVariable("y").OfType(BuiltinTypeId.List)
                    .And.HaveVariable("z").OfType(BuiltinTypeId.Int).WithDescription("int")
                    .And.HaveVariable("m").WithDescription("min(a, b, c, key = func)")
                    .And.HaveVariable("list_append").WithDescription("list.append(self, value)")
                    .And.HaveVariable("abc_length").WithNoTypes()
                    .And.HaveVariable("c_length").WithNoTypes()
                    .And.HaveVariable("d").OfType("fob")
                    .And.HaveVariable("sys").WithDescription("sys")
                    .And.HaveVariable("f").WithDescription("module.f() -> str")
                    .And.HaveVariable("fob_g").WithDescription("method g of module.fob objects")
                    .And.HaveVariable("fob").WithDescription("class module.fob(object)")
                    .And.HaveVariable("g").WithDescription("module.g()")
                    .And.HaveVariable("none").WithDescription("None")
                    .And.HaveVariable("fn").WithDescription("property of type str")
                    .And.HaveVariable("h").WithDescription("module.h() -> module.f() -> str, module.g()")
                    .And.HaveVariable("docstr_func").WithDescription("module.docstr_func() -> int")
                                                    .WithDocumentation("useful documentation")

                    .And.HaveVariable("with_params").WithDescription("module.with_params(a, b, c)")
                    .And.HaveVariable("with_params_default").WithDescription("module.with_params_default(a, b, c: int=100)")
                    .And.HaveVariable("with_params_default_2").WithDescription("module.with_params_default_2(a, b, c: list=[])")
                    .And.HaveVariable("with_params_default_3").WithDescription("module.with_params_default_3(a, b, c: tuple=())")
                    .And.HaveVariable("with_params_default_4").WithDescription("module.with_params_default_4(a, b, c: dict={})")
                    .And.HaveVariable("with_params_default_2a").WithDescription("module.with_params_default_2a(a, b, c: list[None]=[...])")
                    .And.HaveVariable("with_params_default_3a").WithDescription("module.with_params_default_3a(a, b, c: tuple[None]=(...))")
                    .And.HaveVariable("with_params_default_4a").WithDescription("module.with_params_default_4a(a, b, c: dict={...})")
                    .And.HaveVariable("with_params_default_starargs").WithDescription("module.with_params_default_starargs(*args, **kwargs)")

                    //// method which returns itself, we shouldn't stack overflow producing the help...
                    .And.HaveVariable("rf").WithDescription("method return_func of module.return_func_class objects...")
                                           .WithDocumentation("some help");
            }
        }

        [TestMethod, Priority(0)]
        public async Task MemberType() {
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
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);

                analysis.Should().HaveBuiltinMember<IBuiltinClassInfo>("list").WithMemberOfType("append", PythonMemberType.Method)
                    .And.HaveBuiltinMember<SpecializedCallable>("min").OfPythonMemberType(PythonMemberType.Function)
                    .And.HaveBuiltinMember<IBuiltinClassInfo>("int").OfPythonMemberType(PythonMemberType.Class)
                    .And.HaveFunctionInfo("f").WithMemberOfType("func_name", PythonMemberType.Property)
                    .And.HaveVariable("y").WithValue<ListInfo>().WithMemberOfType("append", PythonMemberType.Method)
                    .And.HaveVariable("sys").WithValue<SysModuleInfo>().OfPythonMemberType(PythonMemberType.Module);
            }
        }

        [TestMethod, Priority(0)]
        public async Task RecursiveDataStructures() {
            var text = @"
d = {}
d[0] = d
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("d").WithDescription("dict({int : dict})");
            }
        }

        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task NoTypesButIsMember() {
            var text = @"
def f(x, y):
    C(x, y)

class C(object):
    def __init__(self, x, y):
        self.x = x
        self.y = y

f(1)
c = C()
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("c").WithValue<IInstanceInfo>()
                    .Which.Should().HaveMembers("x", "y");
            }
        }

        /// <summary>
        /// Test case where we have a member but we don't have any type information for the member.  It should
        /// still show up as a member.
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task SequenceFromSequence() {
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

x0 = x[0]
ly0 = ly[0]
lz0 = lz[0]
ty0 = ty[0]
tz0 = tz[0]
lyt0 = lyt[0]
tyt0 = tyt[0]
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("x0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("ly0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("lz0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("ty0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("tz0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("lyt0").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("tyt0").OfType(BuiltinTypeId.Int);
            }
        }

        /// <summary>
        /// Verifies that constructing lists / tuples from more lists/tuples doesn't cause an infinite analysis as we keep creating more lists/tuples.
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task ListRecursion() {
            var text = @"
def f(x):
    print abc
    return f(list(x))

abc = f(())
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("abc");
            }
        }

        [TestMethod, Priority(0)]
        public async Task UpdateMethodMultiFiles() {
            var text1 = @"
def f(abc):
    pass
";

            var text2 = @"
import module1
module1.f(42)
";

            using (var server = await CreateServerAsync()) {
                var uri1 = await server.OpenNextDocumentAndGetUriAsync(text1);
                var uri2 = await server.OpenNextDocumentAndGetUriAsync(text2);

                var analysis1 = await server.GetAnalysisAsync(uri1);
                var analysis2 = await server.GetAnalysisAsync(uri2);
                analysis1.Should().HaveFunction("f").WithParameter("abc").OfType(BuiltinTypeId.Int);

                // re-analyze project1, we should still know about the type info provided by module2
                await server.SendDidChangeTextDocumentAsync(uri1, Environment.NewLine + text1);
                analysis1 = await server.GetAnalysisAsync(uri1);
                analysis1.Should().HaveFunction("f").WithParameter("abc").OfType(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task MetaClassesV2() {

            string text = @"class C(type):
    def f(self):
        print('C.f')

    def x(self, var):
        pass


class D(object):
    __metaclass__ = C
    @classmethod
    def g(cls):
        cls.f()
        cls.g()
        cls.x()
        cls.inst_method()


    def inst_method(self):
        pass
    ";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var signaturesF = await server.SendSignatureHelp(uri, 12, 14);
                var signaturesG = await server.SendSignatureHelp(uri, 13, 14);
                var signaturesX = await server.SendSignatureHelp(uri, 14, 14);
                var signaturesInstMethod = await server.SendSignatureHelp(uri, 15, 24);

                signaturesF.Should().OnlyHaveSignature("f()").Which.Should().HaveNoParameters();
                signaturesG.Should().OnlyHaveSignature("g()").Which.Should().HaveNoParameters();
                signaturesX.Should().OnlyHaveSignature("x(var)").Which.Should().OnlyHaveParameterLabels("var");
                signaturesInstMethod.Should().OnlyHaveSignature("inst_method(self: D)").Which.Should().OnlyHaveParameterLabels("self");
            }
        }

        [TestMethod, Priority(0)]
        public async Task MetaClassesV3() {
            var text = @"class C(type):
    def f(self):
        print('C.f')

    def x(self, var):
        pass


class D(object, metaclass = C):
    @classmethod
    def g(cls):
        cls.f()
        cls.g()
        cls.x()
        cls.inst_method()


    def inst_method(self):
        pass
    ";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var signaturesF = await server.SendSignatureHelp(uri, 11, 14);
                var signaturesG = await server.SendSignatureHelp(uri, 12, 14);
                var signaturesX = await server.SendSignatureHelp(uri, 13, 14);
                var signaturesInstMethod = await server.SendSignatureHelp(uri, 14, 24);

                signaturesF.Should().OnlyHaveSignature("f()").Which.Should().HaveNoParameters();
                signaturesG.Should().OnlyHaveSignature("g()").Which.Should().HaveNoParameters();
                signaturesX.Should().OnlyHaveSignature("x(var)").Which.Should().OnlyHaveParameterLabels("var");
                signaturesInstMethod.Should().OnlyHaveSignature("inst_method(self: D)").Which.Should().OnlyHaveParameterLabels("self");
            }
        }

        /// <summary>
        /// Tests assigning odd things to the metaclass variable.
        /// </summary>
        [DataRow("[1,2,3]")]
        [DataRow("(1,2)")]
        [DataRow("1")]
        [DataRow("abc")]
        [DataRow("1.0")]
        [DataRow("lambda x: 42")]
        [DataRow("C.f")]
        [DataRow("C().f")]
        [DataRow("f")]
        [DataRow("{2:3}")]
        [DataTestMethod, Priority(0)]
        public async Task InvalidMetaClassValuesV2(string assign) {
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

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
            }
        }

        [DataRow("[1,2,3]")]
        [DataRow("(1,2)")]
        [DataRow("1")]
        [DataRow("abc")]
        [DataRow("1.0")]
        [DataRow("lambda x: 42")]
        [DataRow("C.f")]
        [DataRow("C().f")]
        [DataRow("f")]
        [DataRow("{2:3}")]
        [DataTestMethod, Priority(0)]
        public async Task InvalidMetaClassValuesV3(string assign) {
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

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FromImport() {
            using (var server = await CreateServerAsync()) {
                await server.OpenDefaultDocumentAndGetAnalysisAsync("from #   blah");
            }
        }

        [TestMethod, Priority(0)]
        public async Task SelfNestedMethod() {
            // http://pytools.codeplex.com/workitem/648
            var code = @"class MyClass:
    def func1(self):
        def func2(a, b):
            return a

        return func2('abc', 123)

x = MyClass().func1()
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
            }
        }



        [TestMethod, Priority(0)]
        public async Task ParameterAnnotation() {
            var text = @"
s = None
def f(s: s = 123):
    return s
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("s").OfType(BuiltinTypeId.NoneType)
                    .And.HaveFunction("f")
                    .Which.Should().HaveParameter("s").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.NoneType)
                    // Function returns int, s (None) and type of the passed argument (Unknown/ParameterInfo)
                    .And.HaveReturnValue().OfTypes(BuiltinTypeId.Int, BuiltinTypeId.NoneType, BuiltinTypeId.Unknown);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ParameterAnnotationLambda() {
            var text = @"
s = None
def f(s: lambda s: s > 0 = 123):
    return s
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("s").OfType(BuiltinTypeId.NoneType)
                    .And.HaveFunction("f")
                    .Which.Should().HaveParameter("s").OfTypes(BuiltinTypeId.Int)
                    // Function returns int and type of the passed argument (Unknown/ParameterInfo)
                    .And.HaveReturnValue().OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Unknown);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReturnAnnotation() {
            var text = @"
s = None
def f(s = 123) -> s:
    return s
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("s").OfType(BuiltinTypeId.NoneType)
                    .And.HaveFunction("f")
                    .Which.Should().HaveParameter("s").OfTypes(BuiltinTypeId.Int)
                    // Function returns int, s (None) and also type of the passed argument (Unknown/ParameterInfo)
                    .And.HaveReturnValue().OfTypes(BuiltinTypeId.Int, BuiltinTypeId.NoneType, BuiltinTypeId.Unknown);
            }
        }
/*
        [TestMethod, Priority(0)]
        public async Task Super() {
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
        public async Task FunctoolsPartial() {
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
        public async Task FunctoolsWraps() {
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
        public async Task MultilineFunctionDescription() {
            var code = @"class A:
    def fn(self):
        return lambda: 123
";
            var entry = ProcessText(code);

            Assert.AreEqual(
                "test_module.A.fn(self: A) -> lambda: 123 -> int\ndeclared in A",
                entry.GetDescriptions("A.fn", 0).Single().Replace("\r\n", "\n")
            );
        }

        [TestMethod, Priority(0)]
        public async Task SysModulesSetSpecialization() {
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
        public async Task SysModulesGetSpecialization() {
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
        public async Task ClassInstanceAttributes() {
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
        public async Task RecursiveGetDescriptor() {
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
        public async Task Coroutine() {
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
        public async Task AsyncWithStatement() {
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
        public async Task AsyncForIterator() {
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
        public async Task RecursiveDecorators() {
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
                var path1 = TestData.GetTestSpecificPath(Path.Combine("p", "__init__.py"));
                var path2 = TestData.GetTestSpecificPath(Path.Combine("p", "m.py"));
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
    }

    [TestClass]
    public class StdLibAnalysisTest : AnalysisTest {
        protected override AnalysisLimits GetLimits() => AnalysisLimits.GetStandardLibraryLimits();
    }
}
