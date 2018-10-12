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

using System.Threading.Tasks;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class CompletionTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        [ServerTestMethod, Priority(0)]
        public async Task CompletionInWithStatementDerivedClass(Server server) {
            var uri = await server.OpenDefaultDocumentAndGetUriAsync("with open(x) as fs:\n  fs. ");
            await server.GetAnalysisAsync(uri);
            var completions = await server.SendCompletion(uri, 1, 5);

            completions.Should().HaveLabels("read", "write");
            await server.UnloadFileAsync(uri);
        }

        [ServerTestMethod(LatestAvailable2X = true), Priority(0)]
        public async Task PrivateMembers(Server server) {
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
                .Which.Should().HaveInsertText("append(self, value):\r\n    return super(oar, self).append(value)");
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
                .Which.Should().HaveInsertText($"append(self, {parameterName}):\r\n    return super().append({parameterName})");
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
        [Ignore("https://github.com/Microsoft/python-language-server/issues/237")]
        public async Task CompletionDocumentation(Server server) {
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
        public async Task Completion_AddBracketsEnabled(Server server, string code, int row, int character, string expectedLabel, string expectedInsertText) {
            await server.SendDidChangeConfiguration(new ServerSettings.PythonCompletionOptions {addBrackets = true});

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            var completion = await server.SendCompletion(uri, row, character);

            completion.Should().HaveItem(expectedLabel)
                .Which.Should().HaveInsertText(expectedInsertText);
        }

        [ServerTestMethod, Priority(0)]
        public async Task Completion_AddBracketsEnabled_MethodOverride(Server server) {
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
                .Which.Should().HaveInsertText("foo(self):\r\n    return super().foo()");
        }
    }
}