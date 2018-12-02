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
    public class CompletionTests : ServerBasedTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

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

        [TestMethod, Priority(0)]
        public async Task TopLevelCompletions() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = GetDocument(Path.Combine("TestData", "AstAnalysis", "TopLevelCompletions.py"));
                await server.LoadFileAsync(uri);

                await AssertCompletion(
                    server, uri,
                    new[] { "x", "y", "z", "int", "float", "class", "def", "while", "in" },
                    new[] { "return", "sys", "yield" }
                );

                // Completions in function body
                await AssertCompletion(
                    server, uri,
                    new[] { "x", "y", "z", "int", "float", "class", "def", "while", "in", "return", "yield" },
                    new[] { "sys" },
                    position: new Position { line = 5, character = 5 }
                );
            }
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

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task InForStatement(bool is2X) {
            using (var server = await CreateServerAsync(is2X ? PythonVersions.LatestAvailable2X : PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("for  ");
                await AssertCompletion(server, uri, new[] { "for" }, new string[0], new SourceLocation(1, 4));
                (await server.SendCompletion(uri, 0, 4)).Should().HaveNoCompletion();
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("for  x ");
                await AssertCompletion(server, uri, new[] { "for" }, new string[0], new SourceLocation(1, 4));
                (await server.SendCompletion(uri, 0, 4)).Should().HaveNoCompletion();
                (await server.SendCompletion(uri, 0, 5)).Should().HaveNoCompletion();
                (await server.SendCompletion(uri, 0, 6)).Should().HaveNoCompletion();
                await AssertCompletion(server, uri, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 8));
                await server.UnloadFileAsync(uri);

                // TODO: Fix parser to parse "for x i" as ForStatement and not ForStatement+ExpressionStatement
                //u = await s.OpenDefaultDocumentAndGetUriAsync("for x i");
                //await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 8), applicableSpan: new SourceSpan(1, 7, 1, 8));
                //await s.UnloadFileAsync(u);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("for x in ");
                await AssertCompletion(server, uri, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 7));
                await AssertCompletion(server, uri, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 9));
                await AssertCompletion(server, uri, new[] { "abs", "x" }, new string[0], new SourceLocation(1, 10));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("def f():\n    for ");
                (await server.SendCompletion(uri, 1, 8)).Should().HaveNoCompletion();
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("def f():\n    for x in ");
                await AssertCompletion(server, uri, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(2, 11));
                await AssertCompletion(server, uri, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(2, 13));
                await AssertCompletion(server, uri, new[] { "abs", "x" }, new string[0], new SourceLocation(2, 14));
                await server.UnloadFileAsync(uri);

                if (!is2X) {
                    uri = await server.OpenDefaultDocumentAndGetUriAsync("async def f():\n    async for x in ");
                    await AssertCompletion(server, uri, new[] { "async", "for" }, new string[0], new SourceLocation(2, 5));
                    await AssertCompletion(server, uri, new[] { "async", "for" }, new string[0], new SourceLocation(2, 10));
                    await AssertCompletion(server, uri, new[] { "async", "for" }, new string[0], new SourceLocation(2, 14));
                    (await server.SendCompletion(uri, 1, 14)).Should().HaveNoCompletion();
                    await server.UnloadFileAsync(uri);
                }
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

        [TestMethod, Priority(0)]
        public async Task InWithStatement() {
            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("with x as y, z as w: pass");
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 6));
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
                (await server.SendCompletion(uri, 0, 10)).Should().HaveNoCompletion();
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 14));
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 17));
                (await server.SendCompletion(uri, 0, 19)).Should().HaveNoCompletion();
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 21));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("with ");
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 6));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("with x ");
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 6));
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("with x as ");
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 6));
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
                (await server.SendCompletion(uri, 0, 10)).Should().HaveNoCompletion();
                await server.UnloadFileAsync(uri);
            }
        }

        [TestMethod, Priority(0)]
        public async Task InImport() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("import unittest.case as C, unittest\nfrom unittest.case import TestCase as TC, TestCase");

                await AssertCompletion(server, uri, new[] { "from", "import", "abs", "dir" }, new[] { "abc" }, new SourceLocation(1, 7));
                await AssertCompletion(server, uri, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
                await AssertCompletion(server, uri, new[] { "case" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 17));
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 22));
                (await server.SendCompletion(uri, 0, 24)).Should().HaveNoCompletion();
                await AssertCompletion(server, uri, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(1, 28));

                await AssertCompletion(server, uri, new[] { "from", "import", "abs", "dir" }, new[] { "abc" }, new SourceLocation(2, 5));
                await AssertCompletion(server, uri, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(2, 6));
                await AssertCompletion(server, uri, new[] { "case" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 15));
                await AssertCompletion(server, uri, new[] { "import" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 20));
                await AssertCompletion(server, uri, new[] { "import" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 22), applicableSpan: new SourceSpan(2, 20, 2, 26));
                await AssertCompletion(server, uri, new[] { "TestCase" }, new[] { "abs", "dir", "case" }, new SourceLocation(2, 27));
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 36));
                (await server.SendCompletion(uri, 1, 38)).Should().HaveNoCompletion();
                await AssertCompletion(server, uri, new[] { "TestCase" }, new[] { "abs", "dir", "case" }, new SourceLocation(2, 44));

                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("from unittest.case imp\n\npass");
                await AssertCompletion(server, uri, new[] { "import" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 22), applicableSpan: new SourceSpan(1, 20, 1, 23));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("import unittest.case a\n\npass");
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 23), applicableSpan: new SourceSpan(1, 22, 1, 23));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("from unittest.case import TestCase a\n\npass");
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 37), applicableSpan: new SourceSpan(1, 36, 1, 37));
                await server.UnloadFileAsync(uri);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ForOverride() {
            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("class A(object):\n    def i(): pass\n    def \npass");

                (await server.SendCompletion(uri, 1, 8)).Should().HaveNoCompletion();
                await AssertCompletion(server, uri, new[] { "def" }, new[] { "__init__" }, new SourceLocation(3, 8));
                await AssertCompletion(server, uri, new[] { "__init__" }, new[] { "def" }, new SourceLocation(3, 9), cmpKey: ci => ci.label);
            }
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task ForOverrideArgs(bool is2X) {
            var code = @"
class A(object):
    def foo(self, a, b=None, *args, **kwargs):
        pass

class B(A):
    def f";

            using (var server = await CreateServerAsync(is2X ? PythonVersions.LatestAvailable2X : PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);

                (await server.SendCompletion(uri, 2, 8)).Should().HaveNoCompletion();
                if (!is2X) {
                    await AssertCompletion(server, uri,
                        new[] { $"foo(self, a, b=None, *args, **kwargs):{Environment.NewLine}    return super().foo(a, b=b, *args, **kwargs)" },
                        new[] { $"foo(self, a, b = None, *args, **kwargs):{Environment.NewLine}    return super().foo(a, b = b, *args, **kwargs)" },
                        new SourceLocation(7, 10));
                } else {
                    await AssertCompletion(server, uri,
                        new[] { $"foo(self, a, b=None, *args, **kwargs):{Environment.NewLine}    return super(B, self).foo(a, b=b, *args, **kwargs)" },
                        new[] { $"foo(self, a, b = None, *args, **kwargs):{Environment.NewLine}    return super(B, self).foo(a, b = b, *args, **kwargs)" },
                        new SourceLocation(7, 10));
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task InDecorator() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("@dec\ndef f(): pass\n\nx = a @ b");

                await AssertCompletion(server, uri, new[] { "f", "x", "property", "abs" }, new[] { "def" }, new SourceLocation(1, 2));
                await AssertCompletion(server, uri, new[] { "f", "x", "property", "abs" }, new[] { "def" }, new SourceLocation(4, 8));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("@");
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 2));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("import unittest\n\n@unittest.\n");
                await AssertCompletion(server, uri, new[] { "TestCase", "skip", "skipUnless" }, new[] { "abs", "def" }, new SourceLocation(3, 11));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("import unittest\n\n@unittest.\ndef f(): pass");
                await AssertCompletion(server, uri, new[] { "TestCase", "skip", "skipUnless" }, new[] { "abs", "def" }, new SourceLocation(3, 11));
                await server.UnloadFileAsync(uri);
            }
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task InRaise(bool is2X) {
            using (var server = await CreateServerAsync(is2X ? PythonVersions.LatestAvailable2X : PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("raise ");
                await AssertCompletion(server, uri, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(1, 7));
                await server.UnloadFileAsync(uri);

                if (!is2X) {
                    uri = await server.OpenDefaultDocumentAndGetUriAsync("raise Exception from ");
                    await AssertCompletion(server, uri, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(1, 7));
                    await AssertCompletion(server, uri, new[] { "from" }, new[] { "Exception", "def", "abs" }, new SourceLocation(1, 17));
                    await AssertAnyCompletion(server, uri, new SourceLocation(1, 22));
                    await server.UnloadFileAsync(uri);

                    uri = await server.OpenDefaultDocumentAndGetUriAsync("raise Exception fr");
                    await AssertCompletion(server, uri, new[] { "from" }, new[] { "Exception", "def", "abs" }, new SourceLocation(1, 19), applicableSpan: new SourceSpan(1, 17, 1, 19));
                    await server.UnloadFileAsync(uri);
                }

                uri = await server.OpenDefaultDocumentAndGetUriAsync("raise Exception, x, y");
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 17));
                await AssertAnyCompletion(server, uri, new SourceLocation(1, 20));
                await server.UnloadFileAsync(uri);
            }
        }

        [TestMethod, Priority(0)]
        public async Task InExcept() {
            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("try:\n    pass\nexcept ");
                await AssertCompletion(server, uri, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 8));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("try:\n    pass\nexcept (");
                await AssertCompletion(server, uri, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 9));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("try:\n    pass\nexcept Exception  as ");
                await AssertCompletion(server, uri, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 8));
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "Exception", "def", "abs" }, new SourceLocation(3, 18));
                (await server.SendCompletion(uri, 2, 21)).Should().HaveNoCompletion();
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("try:\n    pass\nexc");
                await AssertCompletion(server, uri, new[] { "except", "def", "abs" }, new string[0], new SourceLocation(3, 3));
                await server.UnloadFileAsync(uri);

                uri = await server.OpenDefaultDocumentAndGetUriAsync("try:\n    pass\nexcept Exception a");
                await AssertCompletion(server, uri, new[] { "as" }, new[] { "Exception", "def", "abs" }, new SourceLocation(3, 19), applicableSpan: new SourceSpan(3, 18, 3, 19));
                await server.UnloadFileAsync(uri);
            }
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

        [TestMethod, Priority(0)]
        public async Task WithNewDot() {
            // LSP assumes that the text buffer is up to date with typing,
            // which means the language server must know about dot for a
            // dot completion.
            // To do this, we have to support using a newer tree than the
            // current analysis, so that we can quickly parse the new text
            // with the dot but not block on reanalysis.
            using (var server = await CreateServerAsync()) {
                var code = @"
class MyClass:
    def f(self): pass

mc = MyClass()
mc
";
                int testLine = 5;
                int testChar = 2;

                var mod = await server.OpenDefaultDocumentAndGetUriAsync(code);

                // Completion after "mc " should normally be blank
                await AssertCompletion(server, mod,
                    new string[0],
                    new string[0],
                    position: new Position { line = testLine, character = testChar + 1 }
                );

                // While we're here, test with the special override field
                await AssertCompletion(server, mod,
                    new[] { "f" },
                    new[] { "abs", "bin", "int", "mc" },
                    position: new Position { line = testLine, character = testChar + 1 },
                    expr: "mc"
                );

                // Send the document update.
                await server.DidChangeTextDocument(new DidChangeTextDocumentParams {
                    textDocument = new VersionedTextDocumentIdentifier { uri = mod, version = 1 },
                    contentChanges = new[] { new TextDocumentContentChangedEvent {
                    text = ".",
                    range = new Range {
                        start = new Position { line = testLine, character = testChar },
                        end = new Position { line = testLine, character = testChar }
                    }
                } },
                }, CancellationToken.None);

                // Now with the "." event sent, we should see this as a dot completion
                await AssertCompletion(server, mod,
                    new[] { "f" },
                    new[] { "abs", "bin", "int", "mc" },
                    position: new Position { line = testLine, character = testChar + 1 }
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task AfterLoad() {
            using (var server = await CreateServerAsync()) {
                var mod1 = await server.OpenDocumentAndGetUriAsync("mod1.py", "import mod2\n\nmod2.");

                await AssertCompletion(server, mod1,
                    position: new Position { line = 2, character = 5 },
                    contains: new string[0],
                    excludes: new[] { "value" }
                );

                var mod2 = await server.OpenDocumentAndGetUriAsync("mod2.py", "value = 123");

                await AssertCompletion(server, mod1,
                    position: new Position { line = 2, character = 5 },
                    contains: new[] { "value" },
                    excludes: new string[0]
                );

                await server.UnloadFileAsync(mod2);
                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

                await AssertCompletion(server, mod1,
                    position: new Position { line = 2, character = 5 },
                    contains: new string[0],
                    excludes: new[] { "value" }
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task MethodFromBaseClass2X() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var code = @"
import unittest
class Simple(unittest.TestCase):
    def test_exception(self):
        self.assertRaises().
";
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);

                await AssertCompletion(server, uri,
                     position: new Position { line = 4, character = 28 },
                     contains: new[] { "exception" },
                     excludes: Array.Empty<string>()
                 );
            }
        }

        [TestMethod, Priority(0)]
        public async Task MethodFromBaseClass3X() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var code = @"
import unittest
class Simple(unittest.TestCase):
    def test_exception(self):
        self.assertRaises().
";

                var analysis = await GetStubBasedAnalysis(
                                    server, code,
                                    new AnalysisLimits { UseTypeStubPackages = true },
                                    searchPaths: Enumerable.Empty<string>(),
                                    stubPaths: new[] { GetTypeshedPath() });

                await AssertCompletion(server, analysis.DocumentUri,
                    position: new Position { line = 4, character = 28 },
                    contains: new[] { "exception" },
                    excludes: Array.Empty<string>()
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task CollectionsNamedTuple() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var code = @"
from collections import namedtuple
nt = namedtuple('Point', ['x', 'y'])
pt = nt(1, 2)
pt.
";
                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                server.Analyzer.Limits = new AnalysisLimits { UseTypeStubPackages = true, UseTypeStubPackagesExclusively = false };

                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var completion = await server.SendCompletion(uri, 4, 3);

                completion.Should().HaveLabels("count", "index");
            }
        }

        [TestMethod, Priority(0)]
        public async Task Hook() {
            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("x = 123\nx.");

                await AssertCompletion(server, uri, new[] { "real", "imag" }, new string[0], new Position { line = 1, character = 2 });

                await server.LoadExtensionAsync(new PythonAnalysisExtensionParams {
                    assembly = typeof(TestCompletionHookProvider).Assembly.FullName,
                    typeName = typeof(TestCompletionHookProvider).FullName
                }, null, CancellationToken.None);

                await AssertCompletion(server, uri, new[] { "*real", "*imag" }, new[] { "real" }, new Position { line = 1, character = 2 });
            }
        }

        [TestMethod, Priority(0)]
        public async Task MultiPartDocument() {
            using (var server = await CreateServerAsync()) {
                var mod = await AddModule(server, "x = 1", "mod");
                var modP2 = new Uri(mod, "#2");
                var modP3 = new Uri(mod, "#3");

                await AssertCompletion(server, mod, new[] { "x" }, null);

                Assert.AreEqual(Tuple.Create("y = 2", 1), await ApplyChange(server, modP2, DocumentChange.Insert("y = 2", SourceLocation.MinValue)));
                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

                await AssertCompletion(server, modP2, new[] { "x", "y" }, null);

                Assert.AreEqual(Tuple.Create("z = 3", 1), await ApplyChange(server, modP3, DocumentChange.Insert("z = 3", SourceLocation.MinValue)));
                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

                await AssertCompletion(server, modP3, new[] { "x", "y", "z" }, null);
                await AssertCompletion(server, mod, new[] { "x", "y", "z" }, null);

                await ApplyChange(server, mod, DocumentChange.Delete(SourceLocation.MinValue, SourceLocation.MinValue.AddColumns(5)));
                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                await AssertCompletion(server, modP2, new[] { "y", "z" }, new[] { "x" });
                await AssertCompletion(server, modP3, new[] { "y", "z" }, new[] { "x" });
            }
        }

        [TestMethod, Priority(0)]
        public async Task WithWhitespaceAroundDot() {
            using (var s = await CreateServerAsync()) {
                var u = await s.OpenDefaultDocumentAndGetUriAsync("import sys\nsys  .  version\n");
                await AssertCompletion(s, u, new[] { "argv" }, null, new SourceLocation(2, 7),
                    new CompletionContext { triggerCharacter = ".", triggerKind = CompletionTriggerKind.TriggerCharacter });
            }
        }

        [TestMethod, Priority(0)]
        public async Task MarkupKindValid() {
            using (var s = await CreateServerAsync()) {
                var u = await s.OpenDefaultDocumentAndGetUriAsync("import sys\nsys.\n");

                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var res = await s.Completion(new CompletionParams {
                    textDocument = new TextDocumentIdentifier { uri = u },
                    position = new SourceLocation(2, 5),
                    context = new CompletionContext { triggerCharacter = ".", triggerKind = CompletionTriggerKind.TriggerCharacter },
                }, CancellationToken.None);

                res.items?.Select(i => i.documentation.kind).Should().NotBeEmpty().And.BeSubsetOf(new[] { MarkupKind.PlainText, MarkupKind.Markdown });
            }
        }

        [TestMethod, Priority(0)]
        public async Task NewType() {
            var code = @"
from typing import NewType

Foo = NewType('Foo', dict)
foo: Foo = Foo({ })
foo.
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                await server.GetAnalysisAsync(uri);

                var completions = await server.SendCompletion(uri, 5, 4);
                completions.Should().HaveLabels("clear", "copy", "items", "keys", "update", "values");
            }
        }

        [TestMethod, Priority(0)]
        public async Task GenericListBase() {
            var code = @"
from typing import List

def func(a: List[str]):
    a.
    a[0].
    pass
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                await server.GetAnalysisAsync(uri);

                var completions = await server.SendCompletion(uri, 4, 6);
                completions.Should().HaveLabels("clear", "copy", "count", "index", "remove", "reverse");

                completions = await server.SendCompletion(uri, 5, 9);
                completions.Should().HaveLabels("capitalize");
            }
        }

        [TestMethod, Priority(0)]
        public async Task GenericDictBase() {
            var code = @"
from typing import Dict

def func(a: Dict[int, str]):
    a.
    a[0].
    pass
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                await server.GetAnalysisAsync(uri);

                var completions = await server.SendCompletion(uri, 4, 6);
                completions.Should().HaveLabels("keys", "values");

                completions = await server.SendCompletion(uri, 5, 9);
                completions.Should().HaveLabels("capitalize");
            }
        }
        private static async Task AssertCompletion(
            Server s, 
            Uri uri, 
            IReadOnlyCollection<string> contains, 
            IReadOnlyCollection<string> excludes, 
            Position? position = null, 
            CompletionContext? context = null, 
            Func<CompletionItem, string> cmpKey = null, 
            string expr = null, 
            Range? applicableSpan = null,
            InsertTextFormat? allFormat = InsertTextFormat.PlainText) {
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
            var res = await s.Completion(new CompletionParams {
                textDocument = new TextDocumentIdentifier { uri = uri },
                position = position ?? new Position(),
                context = context,
                _expr = expr
            }, CancellationToken.None);
            DumpDetails(res);

            cmpKey = cmpKey ?? (c => c.insertText);
            var items = res.items?.Select(i => (cmpKey(i), i.insertTextFormat)).ToList() ?? new List<(string, InsertTextFormat)>();

            if (contains != null && contains.Any()) {
                items.Select(i => i.Item1).Should().Contain(contains);

                if (allFormat != null) {
                    items.Where(i => contains.Contains(i.Item1)).Select(i => i.Item2).Should().AllBeEquivalentTo(allFormat);
                }
            }

            if (excludes != null && excludes.Any()) {
                items.Should().NotContain(excludes);
            }

            if (applicableSpan.HasValue) {
                res._applicableSpan.Should().Be(applicableSpan);
            }
        }

        private static async Task AssertAnyCompletion(Server s, TextDocumentIdentifier document, Position position) {
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None).ConfigureAwait(false);
            var res = await s.Completion(new CompletionParams { textDocument = document, position = position }, CancellationToken.None);
            DumpDetails(res);
            if (res.items == null || !res.items.Any()) {
                Assert.Fail("Completions were not returned");
            }
        }

        private static void DumpDetails(CompletionList completions) {
            var span = ((SourceSpan?)completions._applicableSpan) ?? SourceSpan.None;
            Debug.WriteLine($"Completed {completions._expr ?? "(null)"} at {span}");
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
