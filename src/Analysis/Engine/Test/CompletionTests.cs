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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Shell;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Documentation;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class CompletionTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() 
            => TestEnvironmentImpl.TestCleanup();

        [ServerTestMethod, Priority(0)]
        public async Task InWithStatementDerivedClass(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("with open(x) as fs:\n  fs. ");
            await server.GetAnalysisAsync(uri);
            var completions = await server.SendCompletion(uri, 1, 5);

            completions.Should().HaveLabels("read", "write");
            await server.UnloadFileAsync(uri);
        }

        [ServerTestMethod(LatestAvailable2X = true), Priority(0)]
        public async Task PrivateMembers(Server server) {
            var uri = TestData.GetTestSpecificUri("test_module.py");

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

            await server.SendDidChangeTextDocumentAsync(uri, code);
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

            await server.SendDidChangeTextDocumentAsync(uri, code);
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

            await server.SendDidChangeTextDocumentAsync(uri, code);
            await server.GetAnalysisAsync(uri);

            completions = await server.SendCompletion(uri, 5, 16);
            completions.Should().HaveLabels("__FOB", "f");

            completions = await server.SendCompletion(uri, 8, 8);
            completions.Should().HaveLabels("_C__FOB", "f");
        }

        [ServerTestMethod, Priority(0)]
        public async Task RecursiveClass(Server server) {
            var code = @"
cls = object

class cls(cls):
    abc = 42

a = cls().abc
b = cls.abc
";

            var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
            var completion = await server.SendCompletion(TestData.GetDefaultModuleUri(), 8, 0);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int);

            completion.Should().HaveLabels("cls", "object");
        }

        [ServerTestMethod, Priority(0)]
        public async Task ForwardRef(Server server) {
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

            var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
            var completionInD = await server.SendCompletion(TestData.GetDefaultModuleUri(), 3, 4);
            var completionInOar = await server.SendCompletion(TestData.GetDefaultModuleUri(), 5, 8);
            var completionForAbc = await server.SendCompletion(TestData.GetDefaultModuleUri(), 5, 12);

            completionInD.Should().HaveLabels("C", "D", "oar")
                .And.NotContainLabels("a", "abc", "self", "x", "fob", "baz");

            completionInOar.Should().HaveLabels("C", "D", "a", "oar", "abc", "self", "x")
                .And.NotContainLabels("fob", "baz");

            completionForAbc.Should().HaveLabels("baz", "fob");

            analysis.Should().HaveClass("D").WithFunction("oar")
                .Which.Should().HaveParameter("x").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Str, BuiltinTypeId.Tuple);
        }

        [ServerTestMethod, Priority(0)]
        public async Task SimpleGlobals(Server server) {
            var code = @"
class x(object):
    def abc(self):
        pass
        
a = x()
x.abc()
";
            var objectMemberNames = server.GetBuiltinTypeMemberNames(BuiltinTypeId.Object);

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            var completion = await server.SendCompletion(uri, 6, 0);
            var completionX = await server.SendCompletion(uri, 6, 2);

            completion.Should().HaveLabels("a", "x").And.NotContainLabels("abc", "self");
            completionX.Should().HaveLabels(objectMemberNames).And.HaveLabels("abc");
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/799
        /// </summary>
        [ServerTestMethod(LatestAvailable2X = true), Priority(0)]
        public async Task OverrideCompletions2X(Server server) {
            var code = @"
class oar(list):
    def 
    pass
";
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            var completions = await server.SendCompletion(uri, 2, 8);

            completions.Should().HaveItem("append")
                .Which.Should().HaveInsertText($"append(self, value):{Environment.NewLine}    return super(oar, self).append(value)")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [DataRow(PythonLanguageVersion.V36, "value")]
        [DataRow(PythonLanguageVersion.V37, "object")]
        [ServerTestMethod(VersionArgumentIndex = 1), Priority(0)]
        public async Task OverrideCompletions3X(Server server, PythonLanguageVersion version, string parameterName) {
            var code = @"
class oar(list):
    def 
    pass
";
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            var completions = await server.SendCompletion(uri, 2, 8);

            completions.Should().HaveItem("append")
                .Which.Should().HaveInsertText($"append(self, {parameterName}):{Environment.NewLine}    return super().append({parameterName})")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [ServerTestMethod(LatestAvailable2X = true), Priority(0)]
        public async Task OverrideCompletionsNested(Server server) {
            // Ensure that nested classes are correctly resolved.
            var code = @"
class oar(int):
    class fob(dict):
        def 
        pass
    def 
    pass
";


            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            var completionsOar = await server.SendCompletion(uri, 5, 8);
            var completionsFob = await server.SendCompletion(uri, 3, 12);

            completionsOar.Should().NotContainLabels("keys", "items")
                .And.HaveItem("bit_length");
            completionsFob.Should().NotContainLabels("bit_length")
                .And.HaveLabels("keys", "items");
        }

        [ServerTestMethod, Priority(0)]
        public async Task Documentation(Server server) {
            var text = @"
import sys
z = 43

class fob(object):
    @property
    def f(self): pass

    def g(self): pass

d = fob()
";
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            var completionD = await server.SendCompletion(uri, 15, 1);
            completionD.Should().HaveItem("d")
                .Which.Should().HaveDocumentation("fob");
            completionD.Should().HaveItem("z")
                .Which.Should().HaveDocumentation("int");
        }

        [ServerTestMethod, Priority(0)]
        public async Task TypeAtEndOfMethod(Server server) {
            var text = @"
class Fob(object):
    def oar(self, a):
        pass


    def fob(self): 
        pass

x = Fob()
x.oar(100)
";

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
            var completion = await server.SendCompletion(uri, 5, 8);
            completion.Should().HaveItem("a")
                .Which.Should().HaveDocumentation("int");
        }

        [ServerTestMethod, Priority(0)]
        public async Task TypeAtEndOfIncompleteMethod(Server server) {
            var text = @"
class Fob(object):
    def oar(self, a):





x = Fob()
x.oar(100)
";

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
            var completion = await server.SendCompletion(uri, 5, 8);
            completion.Should().HaveItem("a")
                .Which.Should().HaveDocumentation("int");
        }

        [ServerTestMethod, Priority(0)]
        public async Task TypeIntersectionUserDefinedTypes(Server server) {
            var text = @"
class C1(object):
    def fob(self): pass

class C2(object):
    def oar(self): pass

c = C1()
c.fob()
c = C2()
c.
";


            var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
            var completion = await server.SendCompletion(uri, 10, 2);
            completion.Should().NotContainLabels("fob", "oar");
        }

        [DataRow(@"
def foo():
    pass

", 3, 0, "foo", "foo($0)")]
        [DataRow(@"
def foo():
    pass
fo
", 3, 2, "foo", "foo($0)")]
        [DataRow(@"
def foo(value):
    pass

", 3, 0, "foo", "foo($0)")]
        [DataRow(@"
def foo(value):
    pass

", 3, 0, "min", "min($0)")]
        [DataRow(@"
class Class1(object):
    def foo(self):
        pass
Class1().
", 4, 9, "foo", "foo($0)")]
        [DataRow(@"
class Class1(object):
    def foo(self, value):
        pass
Class1().
", 4, 9, "foo", "foo($0)")]
        [DataRow(@"
class Class1(list):
    def foo(self):
        pass
Class1().
", 4, 9, "append", "append($0)")]
        [ServerTestMethod, Priority(0)]
        public async Task AddBracketsEnabled(Server server, string code, int row, int character, string expectedLabel, string expectedInsertText) {
            await server.SendDidChangeConfiguration(new ServerSettings.PythonCompletionOptions { addBrackets = true });

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            var completion = await server.SendCompletion(uri, row, character);

            completion.Should().HaveItem(expectedLabel)
                .Which.Should().HaveInsertText(expectedInsertText)
                .And.HaveInsertTextFormat(InsertTextFormat.Snippet);
        }

        [DataRow(PythonLanguageMajorVersion.LatestV2, "foo(self):{0}    return super(B, self).foo()")]
        [DataRow(PythonLanguageMajorVersion.LatestV3, "foo(self):{0}    return super().foo()")]
        [ServerTestMethod(VersionArgumentIndex = 1), Priority(0)]
        public async Task AddBracketsEnabled_MethodOverride(Server server, PythonLanguageVersion version, string expectedInsertText) {
            expectedInsertText = expectedInsertText.FormatInvariant(Environment.NewLine);
            var code = @"
class A(object):
    def foo(self):
        pass

class B(A):
    def f";

            await server.SendDidChangeConfiguration(new ServerSettings.PythonCompletionOptions { addBrackets = true });
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            var completion = await server.SendCompletion(uri, 6, 9);

            completion.Should().HaveItem("foo")
                .Which.Should().HaveInsertText(expectedInsertText)
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [ServerTestMethod(LatestAvailable3X = true), Priority(0)]
        public async Task TopLevelCompletions(Server server) {
            var uri = new Uri(TestData.GetPath(Path.Combine("TestData", "AstAnalysis", "TopLevelCompletions.py")));
            await server.LoadFileAsync(uri);

            (await server.SendCompletion(uri, 0, 0)).Should().HaveLabels("x", "y", "z", "int", "float", "class", "def", "while", "in")
                .And.NotContainLabels("return", "sys", "yield");

            // Completions in function body
            (await server.SendCompletion(uri, 5, 5)).Should().HaveLabels("x", "y", "z", "int", "float", "class", "def", "while", "in", "return", "yield")
                .And.NotContainLabels("sys");
        }

        [ServerTestMethod(TestSpecificRootUri = true), Priority(0)]
        public async Task Completion_PackageRelativeImport(Server server) {
            var appPath = "app.py";
            var module1Path = "module1.py";
            var packageInitPath = Path.Combine("package", "__init__.py");
            var packageModule1Path = Path.Combine("package", "module1.py");
            var subpackageInitPath = Path.Combine("package", "sub_package", "__init__.py");
            var subpackageTestingPath = Path.Combine("package", "sub_package", "testing.py");

            var appSrc = "import package.sub_package.testing";
            var module1Src = "def wrong(): print('WRONG'); pass";
            var packageInitSrc = string.Empty;
            var packageModule1Src = "def right(): print('RIGHT'); pass";
            var subpackageInitSrc = string.Empty;
            var subpackageTestingSrc = "from ..module1 import ";

            await TestData.CreateTestSpecificFileAsync(appPath, appSrc);
            await TestData.CreateTestSpecificFileAsync(module1Path, module1Src);
            await TestData.CreateTestSpecificFileAsync(packageInitPath, packageInitSrc);
            await TestData.CreateTestSpecificFileAsync(packageModule1Path, packageModule1Src);
            await TestData.CreateTestSpecificFileAsync(subpackageInitPath, subpackageInitSrc);
            var uri = await TestData.CreateTestSpecificFileAsync(subpackageTestingPath, subpackageTestingSrc);

            using (server.AnalysisQueue.Pause()) {
                await server.OpenDocumentAndGetUriAsync(appPath, appSrc);
                await server.OpenDocumentAndGetUriAsync(module1Path, module1Src);
                await server.OpenDocumentAndGetUriAsync(packageInitPath, packageInitSrc);
                await server.OpenDocumentAndGetUriAsync(packageModule1Path, packageModule1Src);
                await server.OpenDocumentAndGetUriAsync(subpackageInitPath, subpackageInitSrc);
                await server.OpenDocumentAndGetUriAsync(subpackageTestingPath, subpackageTestingSrc);
            }

            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
            var completion = await server.SendCompletion(uri, 0, 22);

            completion.Should().HaveItem("right")
                .Which.Should().HaveInsertText("right")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [DataRow(PythonLanguageMajorVersion.LatestV2)]
        [DataRow(PythonLanguageMajorVersion.LatestV3)]
        [ServerTestMethod(VersionArgumentIndex = 1), Priority(0)]
        public async Task InForStatement(Server server, PythonLanguageVersion version) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("for  ");
            (await server.SendCompletion(uri, 0, 3)).Should().HaveLabels("for");
            (await server.SendCompletion(uri, 0, 4)).Should().HaveNoCompletion();

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("for  x ");
            (await server.SendCompletion(uri, 0, 3)).Should().HaveLabels("for");
            (await server.SendCompletion(uri, 0, 4)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 5)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 6)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 7)).Should().HaveLabels("in").And.NotContainLabels("for", "abs");

            // TODO: Fix parser to parse "for x i" as ForStatement and not ForStatement+ExpressionStatement
            //u = await s.OpenDefaultDocumentAndGetUriAsync("for x i");
            //await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 8), applicableSpan: new SourceSpan(1, 7, 1, 8));
            //await s.UnloadFileAsync(u);

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("for x in ");
            (await server.SendCompletion(uri, 0, 6)).Should().HaveLabels("in").And.NotContainLabels("for", "abs");
            (await server.SendCompletion(uri, 0, 8)).Should().HaveLabels("in").And.NotContainLabels("for", "abs");
            (await server.SendCompletion(uri, 0, 9)).Should().HaveLabels("abs", "x");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"def f():
    for ");
            (await server.SendCompletion(uri, 1, 8)).Should().HaveNoCompletion();

            await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"def f():
    for x in ");
            (await server.SendCompletion(uri, 1, 10)).Should().HaveLabels("in").And.NotContainLabels("for", "abs");
            (await server.SendCompletion(uri, 1, 12)).Should().HaveLabels("in").And.NotContainLabels("for", "abs");
            (await server.SendCompletion(uri, 1, 13)).Should().HaveLabels("abs", "x");

            if (version.Is3x()) {
                await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"async def f():
    async for x in ");
                (await server.SendCompletion(uri, 1, 4)).Should().HaveLabels("async", "for");
                (await server.SendCompletion(uri, 1, 9)).Should().HaveLabels("async", "for");
                (await server.SendCompletion(uri, 1, 13)).Should().HaveLabels("async", "for");
                (await server.SendCompletion(uri, 1, 14)).Should().HaveNoCompletion();
            }
        }

        [DataRow(PythonLanguageMajorVersion.LatestV2)]
        [DataRow(PythonLanguageMajorVersion.LatestV3)]
        [ServerTestMethod(VersionArgumentIndex = 1), Priority(0)]
        public async Task InFunctionDefinition(Server server, PythonLanguageVersion version) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("def f(a, b:int, c=2, d:float=None): pass");
            (await server.SendCompletion(uri, 0, 4)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 6)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 7)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 9)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 13)).Should().HaveLabels("int");
            (await server.SendCompletion(uri, 0, 16)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 18)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 28)).Should().HaveLabels("float");
            (await server.SendCompletion(uri, 0, 33)).Should().HaveLabels("NotImplemented");
            (await server.SendCompletion(uri, 0, 34)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 35)).Should().HaveLabels("any");
        }

        [ServerTestMethod(LatestAvailable2X = true), Priority(0)]
        public async Task InFunctionDefinition_2X(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("@dec" + Environment.NewLine + "def  f(): pass");
            (await server.SendCompletion(uri, 0, 0)).Should().HaveLabels("any");
            (await server.SendCompletion(uri, 0, 1)).Should().HaveLabels("abs").And.NotContainLabels("def");
            (await server.SendCompletion(uri, 1, 0)).Should().HaveLabels("def");
            (await server.SendCompletion(uri, 1, 3)).Should().HaveLabels("def");
            (await server.SendCompletion(uri, 1, 4)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 1, 5)).Should().HaveNoCompletion();
        }

        [ServerTestMethod(LatestAvailable3X = true), Priority(0)]
        public async Task InFunctionDefinition_3X(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("@dec" + Environment.NewLine + "async   def  f(): pass");
            (await server.SendCompletion(uri, 0, 0)).Should().HaveLabels("any");
            (await server.SendCompletion(uri, 0, 1)).Should().HaveLabels("abs").And.NotContainLabels("def");
            (await server.SendCompletion(uri, 1, 0)).Should().HaveLabels("def");
            (await server.SendCompletion(uri, 1, 11)).Should().HaveLabels("def");
            (await server.SendCompletion(uri, 1, 12)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 1, 13)).Should().HaveNoCompletion();
        }

        [DataRow(PythonLanguageMajorVersion.LatestV2)]
        [DataRow(PythonLanguageMajorVersion.LatestV3)]
        [ServerTestMethod(VersionArgumentIndex = 1), Priority(0)]
        public async Task InClassDefinition(Server server, PythonLanguageVersion version) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("class C(object, parameter=MC): pass");

            (await server.SendCompletion(uri, 0, 7)).Should().HaveNoCompletion();
            if (version.Is2x()) {
                (await server.SendCompletion(uri, 0, 8)).Should().HaveLabels("object").And.NotContainLabels("metaclass=");
            } else {
                (await server.SendCompletion(uri, 0, 8)).Should().HaveLabels("metaclass=", "object");
            }

            (await server.SendCompletion(uri, 0, 14)).Should().HaveLabels("any");
            (await server.SendCompletion(uri, 0, 16)).Should().HaveLabels("any");
            (await server.SendCompletion(uri, 0, 28)).Should().HaveLabels("object");
            (await server.SendCompletion(uri, 0, 29)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 30)).Should().HaveLabels("any");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("class D(o");
            (await server.SendCompletion(uri, 0, 7)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 8)).Should().HaveLabels("any");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("class E(metaclass=MC,o): pass");
            (await server.SendCompletion(uri, 0, 21)).Should().HaveLabels("object").And.NotContainLabels("metaclass=");
        }

        [ServerTestMethod, Priority(0)]
        public async Task InWithStatement(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("with x as y, z as w: pass");
            (await server.SendCompletion(uri, 0, 5)).Should().HaveAnyCompletions();
            (await server.SendCompletion(uri, 0, 7)).Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");
            (await server.SendCompletion(uri, 0, 10)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 13)).Should().HaveAnyCompletions();
            (await server.SendCompletion(uri, 0, 16)).Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");
            (await server.SendCompletion(uri, 0, 19)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 20)).Should().HaveAnyCompletions();
            await server.UnloadFileAsync(uri);

            uri = await server.OpenDefaultDocumentAndGetUriAsync("with ");
            (await server.SendCompletion(uri, 0, 5)).Should().HaveAnyCompletions();
            await server.UnloadFileAsync(uri);

            uri = await server.OpenDefaultDocumentAndGetUriAsync("with x ");
            (await server.SendCompletion(uri, 0, 5)).Should().HaveAnyCompletions();
            (await server.SendCompletion(uri, 0, 7)).Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");
            await server.UnloadFileAsync(uri);

            uri = await server.OpenDefaultDocumentAndGetUriAsync("with x as ");
            (await server.SendCompletion(uri, 0, 5)).Should().HaveAnyCompletions();
            (await server.SendCompletion(uri, 0, 7)).Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");
            (await server.SendCompletion(uri, 0, 10)).Should().HaveNoCompletion();
            await server.UnloadFileAsync(uri);
        }

        [ServerTestMethod(LatestAvailable3X = true), Priority(0)]
        public async Task InImport(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(@"import unittest.case as C, unittest
from unittest.case import TestCase as TC, TestCase");

            (await server.SendCompletion(uri, 0, 6)).Should().HaveLabels("from", "import", "abs", "dir").And.NotContainLabels("abc");
            (await server.SendCompletion(uri, 0, 7)).Should().HaveLabels("abc", "unittest").And.NotContainLabels("abs", "dir");
            (await server.SendCompletion(uri, 0, 16)).Should().HaveLabels("case").And.NotContainLabels("abc", "unittest", "abs", "dir");
            (await server.SendCompletion(uri, 0, 22)).Should().HaveLabels("as").And.NotContainLabels("abc", "unittest", "abs", "dir");
            (await server.SendCompletion(uri, 0, 24)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 0, 27)).Should().HaveLabels("abc", "unittest").And.NotContainLabels("abs", "dir");

            (await server.SendCompletion(uri, 1, 4)).Should().HaveLabels("from", "import", "abs", "dir").And.NotContainLabels("abc");
            (await server.SendCompletion(uri, 1, 5)).Should().HaveLabels("abc", "unittest").And.NotContainLabels("abs", "dir");
            (await server.SendCompletion(uri, 1, 14)).Should().HaveLabels("case").And.NotContainLabels("abc", "unittest", "abs", "dir");
            (await server.SendCompletion(uri, 1, 19)).Should().HaveLabels("import").And.NotContainLabels("abc", "unittest", "abs", "dir");
            (await server.SendCompletion(uri, 1, 21)).Should().HaveLabels("import")
                .And.NotContainLabels("abc", "unittest", "abs", "dir")
                .And.Subject._applicableSpan.Should().Be(1, 19, 1, 25);

            (await server.SendCompletion(uri, 1, 26)).Should().HaveLabels("TestCase").And.NotContainLabels("abs", "dir", "case");
            (await server.SendCompletion(uri, 1, 35)).Should().HaveLabels("as").And.NotContainLabels("abc", "unittest", "abs", "dir");
            (await server.SendCompletion(uri, 1, 38)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 1, 43)).Should().HaveLabels("TestCase").And.NotContainLabels("abs", "dir", "case");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"from unittest.case imp

pass");
            (await server.SendCompletion(uri, 0, 21)).Should().HaveLabels("import")
                .And.NotContainLabels("abc", "unittest", "abs", "dir")
                .And.Subject._applicableSpan.Should().Be(0, 19, 0, 22);

            await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"import unittest.case a

pass");
            (await server.SendCompletion(uri, 0, 22)).Should().HaveLabels("as")
                .And.NotContainLabels("abc", "unittest", "abs", "dir")
                .And.Subject._applicableSpan.Should().Be(0, 21, 0, 22);

            await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"from unittest.case import TestCase a

pass");
            (await server.SendCompletion(uri, 0, 36)).Should().HaveLabels("as")
                .And.NotContainLabels("abc", "unittest", "abs", "dir")
                .And.Subject._applicableSpan.Should().Be(0, 35, 0, 36);
        }

        [ServerTestMethod, Priority(0)]
        public async Task ForOverride(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(@"class A(object):
    def i(): pass
    def 
pass");

            (await server.SendCompletion(uri, 1, 8)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 2, 7)).Should().HaveInsertTexts("def").And.NotContainInsertTexts("__init__");
            (await server.SendCompletion(uri, 2, 8)).Should().HaveLabels("__init__").And.NotContainLabels("def");
        }

        [DataRow(PythonLanguageMajorVersion.LatestV2, "B, self")]
        [DataRow(PythonLanguageMajorVersion.LatestV3, "")]
        [ServerTestMethod(VersionArgumentIndex = 1), Priority(0)]
        public async Task ForOverrideArgs(Server server, PythonLanguageVersion version, string superArgs) {
            var code = @"
class A(object):
    def foo(self, a, b=None, *args, **kwargs):
        pass

class B(A):
    def f";

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);

            (await server.SendCompletion(uri, 2, 8)).Should().HaveNoCompletion();
            (await server.SendCompletion(uri, 6, 9)).Should()
                    .HaveInsertTexts($"foo(self, a, b=None, *args, **kwargs):{Environment.NewLine}    return super({superArgs}).foo(a, b=b, *args, **kwargs)")
                    .And.NotContainInsertTexts($"foo(self, a, b = None, *args, **kwargs):{Environment.NewLine}    return super({superArgs}).foo(a, b = b, *args, **kwargs)");
        }

        [ServerTestMethod(LatestAvailable3X = true), Priority(0)]
        public async Task InDecorator(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(@"@dec
def f(): pass

x = a @ b");
            (await server.SendCompletion(uri, 0, 1)).Should().HaveLabels("f", "x", "property", "abs").And.NotContainLabels("def");
            (await server.SendCompletion(uri, 3, 7)).Should().HaveLabels("f", "x", "property", "abs").And.NotContainLabels("def");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("@");
            (await server.SendCompletion(uri, 0, 1)).Should().HaveAnyCompletions();

            await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"import unittest

@unittest.
");
            (await server.SendCompletion(uri, 2, 10)).Should().HaveLabels("TestCase", "skip", "skipUnless").And.NotContainLabels("abs", "def");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync(@"import unittest

@unittest.
def f(): pass");
            (await server.SendCompletion(uri, 2, 10)).Should().HaveLabels("TestCase", "skip", "skipUnless").And.NotContainLabels("abs", "def");
        }

        [DataRow(PythonLanguageMajorVersion.LatestV2)]
        [DataRow(PythonLanguageMajorVersion.LatestV3)]
        [ServerTestMethod(VersionArgumentIndex = 1), Priority(0)]
        public async Task InRaise(Server server, PythonLanguageVersion version) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("raise ");
            (await server.SendCompletion(uri, 0, 6)).Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            if (version.Is3x()) {
                await server.ChangeDefaultDocumentAndGetAnalysisAsync("raise Exception from ");
                (await server.SendCompletion(uri, 0, 6)).Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");
                (await server.SendCompletion(uri, 0, 16)).Should().HaveInsertTexts("from").And.NotContainInsertTexts("Exception", "def", "abs");
                (await server.SendCompletion(uri, 0, 21)).Should().HaveAnyCompletions();

                await server.ChangeDefaultDocumentAndGetAnalysisAsync("raise Exception fr");
                (await server.SendCompletion(uri, 0, 18)).Should().HaveInsertTexts("from")
                    .And.NotContainInsertTexts("Exception", "def", "abs")
                    .And.Subject._applicableSpan.Should().Be(0, 16, 0, 18);
            }

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("raise Exception, x, y");
            (await server.SendCompletion(uri, 0, 16)).Should().HaveAnyCompletions();
            (await server.SendCompletion(uri, 0, 19)).Should().HaveAnyCompletions();
        }

        [ServerTestMethod, Priority(0)]
        public async Task InExcept(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("try:\n    pass\nexcept ");
            (await server.SendCompletion(uri, 2, 7)).Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("try:\n    pass\nexcept (");
            (await server.SendCompletion(uri, 2, 8)).Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("try:\n    pass\nexcept Exception  as ");
            (await server.SendCompletion(uri, 2, 7)).Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");
            (await server.SendCompletion(uri, 2, 17)).Should().HaveInsertTexts("as").And.NotContainInsertTexts("def", "abs");
            (await server.SendCompletion(uri, 2, 21)).Should().HaveNoCompletion();

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("try:\n    pass\nexc");
            (await server.SendCompletion(uri, 2, 17)).Should().HaveInsertTexts("except", "def", "abs");

            await server.ChangeDefaultDocumentAndGetAnalysisAsync("try:\n    pass\nexcept Exception a");
            (await server.SendCompletion(uri, 2, 18)).Should().HaveInsertTexts("as")
                .And.NotContainInsertTexts("Exception", "def", "abs")
                .And.Subject._applicableSpan.Should().Be(2, 17, 2, 18);
            await server.UnloadFileAsync(uri);
        }

        [ServerTestMethod, Priority(0)]
        public async Task AfterDot(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("x = 1\nx. n\nx.(  )\nx(x.  )\nx.  \nx  ");
            (await server.SendCompletion(uri, 1, 2)).Should().HaveLabels("real", "imag").And.NotContainLabels("abs");
            (await server.SendCompletion(uri, 1, 3)).Should().HaveLabels("real", "imag").And.NotContainLabels("abs");
            (await server.SendCompletion(uri, 1, 4)).Should().HaveLabels("real", "imag").And.NotContainLabels("abs");
            (await server.SendCompletion(uri, 2, 2)).Should().HaveLabels("real", "imag").And.NotContainLabels("abs");
            (await server.SendCompletion(uri, 3, 4)).Should().HaveLabels("real", "imag").And.NotContainLabels("abs");
            (await server.SendCompletion(uri, 4, 3)).Should().HaveLabels("real", "imag").And.NotContainLabels("abs");
            (await server.SendCompletion(uri, 5, 1)).Should().HaveLabels("abs").And.NotContainLabels("real", "imag");
            (await server.SendCompletion(uri, 5, 2)).Should().HaveNoCompletion();
        }

        [ServerTestMethod, Priority(0)]
        public async Task AfterAssign(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("x = x\ny = ");
            (await server.SendCompletion(uri, 0, 4)).Should().HaveLabels("x", "abs");
            (await server.SendCompletion(uri, 1, 4)).Should().HaveLabels("x", "abs");
        }

        [ServerTestMethod, Priority(0)]
        public async Task WithNewDot(Server server) {
            // LSP assumes that the text buffer is up to date with typing,
            // which means the language server must know about dot for a
            // dot completion.
            // To do this, we have to support using a newer tree than the
            // current analysis, so that we can quickly parse the new text
            // with the dot but not block on reanalysis.
            var code = @"
class MyClass:
    def f(self): pass

mc = MyClass()
mc
";
            var testLine = 5;
            var testChar = 2;

            var mod = await server.OpenDefaultDocumentAndGetUriAsync(code);

            // Completion after "mc " should normally be blank
            var completion = await server.SendCompletion(mod, testLine, testChar + 1);
            completion.Should().HaveNoCompletion();

            // While we're here, test with the special override field
            completion = await server.Completion(new CompletionParams {
                textDocument = new TextDocumentIdentifier {
                    uri = mod
                },
                position = new Position {
                    line = testLine,
                    character = testChar + 1
                },
                _expr = "mc"
            }, CancellationToken.None);
            completion.Should().HaveInsertTexts("f").And.NotContainInsertTexts("abs", "bin", "int", "mc");

            // Send the document update.
            await server.SendDidChangeTextDocumentAsync(mod, ".", new Position {line = testLine, character = testChar});

            // Now with the "." event sent, we should see this as a dot completion
            completion = await server.SendCompletion(mod, testLine, testChar + 1);
            completion.Should().HaveInsertTexts("f").And.NotContainInsertTexts("abs", "bin", "int", "mc");
        }

        [ServerTestMethod, Priority(0)]
        public async Task AfterLoad(Server server) {
            var mod1 = await server.OpenDocumentAndGetUriAsync("mod1.py", @"import mod2

mod2.");
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            var completion = await server.SendCompletion(mod1, 2, 5);
            completion.Should().NotContainInsertTexts("value");

            var mod2 = await server.OpenDocumentAndGetUriAsync("mod2.py", "value = 123");
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            completion = await server.SendCompletion(mod1, 2, 5);
            completion.Should().HaveInsertTexts("value");

            await server.UnloadFileAsync(mod2);
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            completion = await server.SendCompletion(mod1, 2, 5);
            completion.Should().NotContainInsertTexts("value");
        }

        [ServerTestMethod(LatestAvailable2X = true), Priority(0)]
        public async Task MethodFromBaseClass2X(Server server) {
            var code = @"
import unittest
class Simple(unittest.TestCase):
    def test_exception(self):
        self.assertRaises().
";
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            var completion = await server.SendCompletion(uri, 4, 28);
            completion.Should().HaveInsertTexts("exception");
        }

        [ServerTestMethod(LatestAvailable3X = true, DefaultTypeshedPath = true), Priority(0)]
        public async Task MethodFromBaseClass3X(Server server) {
            var code = @"
import unittest
class Simple(unittest.TestCase):
    def test_exception(self):
        self.assertRaises().
";
            
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            var completion = await server.SendCompletion(uri, 4, 28);
            completion.Should().HaveInsertTexts("exception");
        }

        [ServerTestMethod(LatestAvailable3X = true, DefaultTypeshedPath = true), Priority(0)]
        public async Task CollectionsNamedTuple(Server server) {
                var code = @"
from collections import namedtuple
nt = namedtuple('Point', ['x', 'y'])
pt = nt(1, 2)
pt.
";
            server.Analyzer.Limits = new AnalysisLimits { UseTypeStubPackages = true, UseTypeStubPackagesExclusively = false };

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
            var completion = await server.SendCompletion(uri, 4, 3);

            completion.Should().HaveLabels("count", "index");
        }

        [ServerTestMethod, Priority(0)]
        public async Task Hook(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("x = 123\nx.");
            (await server.SendCompletion(uri, 1, 2)).Should().HaveInsertTexts("real", "imag");

            await server.LoadExtensionAsync(new PythonAnalysisExtensionParams {
                assembly = typeof(TestCompletionHookProvider).Assembly.FullName,
                typeName = typeof(TestCompletionHookProvider).FullName
            }, null, CancellationToken.None);
            
            (await server.SendCompletion(uri, 1, 2)).Should().HaveInsertTexts("*real", "*imag").And.NotContainInsertTexts("real");
        }

        [ServerTestMethod, Priority(0)]
        public async Task MultiPartDocument(Server server) {
            var mod = await AddModule(server, "x = 1", "mod");
            var modP2 = new Uri(mod, "#2");
            var modP3 = new Uri(mod, "#3");

            (await server.SendCompletion(mod, 0, 0)).Should().HaveLabels("x");

            await server.SendDidChangeTextDocumentAsync(modP2, "y = 2", new Position());
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            var text = await ((IDocument)server.GetProjectEntry(modP2)).ReadDocument(2, out var version).ReadToEndAsync();
            text.Should().Be("y = 2");
            version.Should().Be(1);

            (await server.SendCompletion(modP2, 0, 0)).Should().HaveLabels("x", "y");

            await server.SendDidChangeTextDocumentAsync(modP3, "z = 3", new Position());
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            text = await ((IDocument)server.GetProjectEntry(modP2)).ReadDocument(3, out version).ReadToEndAsync();
            text.Should().Be("z = 3");
            version.Should().Be(1);

            (await server.SendCompletion(modP3, 0, 0)).Should().HaveLabels("x", "y", "z");
            (await server.SendCompletion(mod, 0, 0)).Should().HaveLabels("x", "y", "z");

            await server.SendDidChangeTextDocumentAsync(mod, "", new Position(), new Position { line = 0, character = 5});
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            (await server.SendCompletion(modP2, 0, 0)).Should().HaveLabels("y", "z").And.NotContainLabels("x");
            (await server.SendCompletion(modP3, 0, 0)).Should().HaveLabels("y", "z").And.NotContainLabels("x");
        }

        [ServerTestMethod, Priority(0)]
        public async Task WithWhitespaceAroundDot(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(@"import sys
sys  .  version
");
            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
            (await server.SendCompletion(uri, 1, 6)).Should().HaveLabels("argv");
        }

        [ServerTestMethod, Priority(0)]
        public async Task MarkupKindValid(Server server) {
            var u = await server.OpenDefaultDocumentAndGetUriAsync("import sys\nsys.\n");

            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
            var completion = await server.SendCompletion(u, 1, 4);

            completion.items?.Select(i => i.documentation.kind).Should().NotBeEmpty().And.BeSubsetOf(new[] { MarkupKind.PlainText, MarkupKind.Markdown });
        }

        [ServerTestMethod(LatestAvailable3X = true), Priority(0)]
        public async Task NewType(Server server) {
            var code = @"
from typing import NewType

Foo = NewType('Foo', dict)
foo: Foo = Foo({ })
foo.
";
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            await server.GetAnalysisAsync(uri);

            var completions = await server.SendCompletion(uri, 5, 4);
            completions.Should().HaveLabels("clear", "copy", "items", "keys", "update", "values");
        }

        [ServerTestMethod(LatestAvailable3X = true), Priority(0)]
        public async Task GenericListBase(Server server) {
            var code = @"
from typing import List

def func(a: List[str]):
    a.
    a[0].
    pass
";
            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            await server.GetAnalysisAsync(uri);

            var completions = await server.SendCompletion(uri, 4, 6);
            completions.Should().HaveLabels("clear", "copy", "count", "index", "remove", "reverse");

            completions = await server.SendCompletion(uri, 5, 9);
            completions.Should().HaveLabels("capitalize");
        }

        [ServerTestMethod(LatestAvailable3X = true), Priority(0)]
        public async Task GenericDictBase(Server server) {
            var code = @"
from typing import Dict

def func(a: Dict[int, str]):
    a.
    a[0].
    pass
";
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                await server.GetAnalysisAsync(uri);

                var completions = await server.SendCompletion(uri, 4, 6);
                completions.Should().HaveLabels("keys", "values");

                completions = await server.SendCompletion(uri, 5, 9);
                completions.Should().HaveLabels("capitalize");
        }
        
        class TestCompletionHookProvider : ILanguageServerExtensionProvider {
            public Task<ILanguageServerExtension> CreateAsync(IPythonLanguageServer server, IReadOnlyDictionary<string, object> properties, CancellationToken cancellationToken) {
                return Task.FromResult<ILanguageServerExtension>(new TestCompletionHook());
            }
        }

        class TestCompletionHook : ILanguageServerExtension, ICompletionExtension {
            public void Dispose() { }

            #region ILanguageServerExtension
            public string Name => "Test completion extension";
            public Task Initialize(IServiceContainer services, CancellationToken token) => Task.CompletedTask;
            public Task<IReadOnlyDictionary<string, object>> ExecuteCommand(string command, IReadOnlyDictionary<string, object> properties, CancellationToken token)
                => Task.FromResult<IReadOnlyDictionary<string, object>>(null);
            #endregion

            #region ICompletionExtension
            public Task HandleCompletionAsync(Uri documentUri, IModuleAnalysis analysis, PythonAst tree, SourceLocation location, CompletionList completions, CancellationToken token) {
                Assert.IsNotNull(tree);
                Assert.IsNotNull(analysis);
                for (int i = 0; i < completions.items.Length; ++i) {
                    completions.items[i].insertText = "*" + completions.items[i].insertText;
                }
                return Task.CompletedTask;
            }
            #endregion
        }

        private static async Task<Uri> AddModule(Server s, string content, string moduleName = null, Uri uri = null, string language = null) {
            uri = uri ?? new Uri($"python://test/{moduleName ?? "test_module"}.py");
            await s.DidOpenTextDocument(new DidOpenTextDocumentParams {
                textDocument = new TextDocumentItem {
                    uri = uri,
                    text = content,
                    languageId = language ?? "python"
                }
            }, CancellationToken.None).ConfigureAwait(false);
            return uri;
        }
    }
}
