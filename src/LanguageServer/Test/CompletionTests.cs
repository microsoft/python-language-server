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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class CompletionTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task TopLevelVariables() {
            const string code = @"
x = 'str'
y = 1

class C:
    def method(self):
        return 1.0

";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(8, 1));
            comps.Should().HaveLabels("C", "x", "y", "while", "for");
        }

        [TestMethod, Priority(0)]
        public async Task StringMembers() {
            const string code = @"
x = 'str'
x.
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(3, 3));
            comps.Should().HaveLabels(@"isupper", @"capitalize", @"split");
        }

        [TestMethod, Priority(0)]
        public async Task ModuleMembers() {
            const string code = @"
import datetime
datetime.datetime.
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(3, 19));
            comps.Should().HaveLabels("now", @"tzinfo", @"ctime");
        }

        [TestMethod, Priority(0)]
        public async Task MembersIncomplete() {
            const string code = @"
class ABCDE:
    def method1(self): pass

ABC
ABCDE.me
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(5, 4));
            comps.Should().HaveLabels(@"ABCDE");

            comps = cs.GetCompletions(analysis, new SourceLocation(6, 9));
            comps.Should().HaveLabels("method1");
        }

        [DataRow(PythonLanguageVersion.V36, "value")]
        [DataRow(PythonLanguageVersion.V37, "object")]
        [DataTestMethod]
        public async Task OverrideCompletions3X(PythonLanguageVersion version, string parameterName) {
            const string code = @"
class oar(list):
    def 
    pass
";
            var analysis = await GetAnalysisAsync(code, version);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(3, 9));

            result.Should().HaveItem("append")
                .Which.Should().HaveInsertText($"append(self, {parameterName}):{Environment.NewLine}    return super().append({parameterName})")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [TestMethod]
        public async Task OverrideInit3X() {
            const string code = @"
class Test():
    def __
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(3, 10));

            result.Should().HaveItem("__init__")
                .Which.Should().HaveInsertText($"__init__(self, *args, **kwargs):{Environment.NewLine}    super().__init__(*args, **kwargs)")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [DataRow(PythonLanguageVersion.V26, "value")]
        [DataRow(PythonLanguageVersion.V27, "value")]
        [DataTestMethod]
        public async Task OverrideCompletions2X(PythonLanguageVersion version, string parameterName) {
            const string code = @"
class oar(list):
    def 
    pass
";
            var analysis = await GetAnalysisAsync(code, version);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(3, 9));

            result.Should().HaveItem("append")
                .Which.Should().HaveInsertText($"append(self, {parameterName}):{Environment.NewLine}    return super(oar, self).append({parameterName})")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [TestMethod]
        public async Task OverrideInit2X() {
            const string code = @"
class A:
    def __init__(self):
        pass

class Test(A):
    def __
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(7, 10));

            result.Should().HaveItem("__init__")
                .Which.Should().HaveInsertText($"__init__(self):{Environment.NewLine}    super(Test, self).__init__()")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [TestMethod, Priority(0)]
        public async Task TypeAtEndOfMethod() {
            const string code = @"
class Fob(object):
    def oar(self, a):
        pass


    def fob(self):
        pass

x = Fob()
x.oar(100)
";

            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(4, 8));
            result.Should().HaveItem("a");
        }

        [TestMethod, Priority(0)]
        public async Task TypeAtEndOfIncompleteMethod() {
            const string code = @"
class Fob(object):
    def oar(self, a):


x = Fob()
x.oar(100)
";

            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(4, 8));
            result.Should().HaveItem("a");
        }

        [TestMethod, Priority(0)]
        public async Task TypeIntersectionUserDefinedTypes() {
            const string code = @"
class C1(object):
    def fob(self): pass

class C2(object):
    def oar(self): pass

c = C1()
c.fob()
c = C2()
c.
";


            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(11, 3));
            result.Should().NotContainLabels("fob");
            result.Should().HaveLabels("oar");
        }

        [DataRow(false, "B, self")]
        [DataRow(true, "")]
        [DataTestMethod, Priority(0)]
        public async Task ForOverrideArgs(bool is3x, string superArgs) {
            const string code = @"
class A(object):
    def foo(self, a, b=None, *args, **kwargs):
        pass

class B(A):
    def f";

            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(7, 10));
            result.Should()
                .HaveInsertTexts($"foo(self, a, b=None, *args, **kwargs):{Environment.NewLine}    return super({superArgs}).foo(a, b=b, *args, **kwargs)")
                .And.NotContainInsertTexts($"foo(self, a, b = None, *args, **kwargs):{Environment.NewLine}    return super({superArgs}).foo(a, b = b, *args, **kwargs)");
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task InRaise(bool is3X) {
            var version = is3X ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X;

            var analysis = await GetAnalysisAsync("raise ", version);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(1, 7));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            if (is3X) {
                analysis = await GetAnalysisAsync("raise Exception from ", PythonVersions.LatestAvailable3X);
                cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

                result = cs.GetCompletions(analysis, new SourceLocation(1, 7));
                result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

                result = cs.GetCompletions(analysis, new SourceLocation(1, 17));
                result.Should().HaveInsertTexts("from").And.NotContainInsertTexts("Exception", "def", "abs");

                result = cs.GetCompletions(analysis, new SourceLocation(1, 22));
                result.Should().HaveAnyCompletions();

                analysis = await GetAnalysisAsync("raise Exception fr ", PythonVersions.LatestAvailable3X);
                result = cs.GetCompletions(analysis, new SourceLocation(1, 19));
                result.Should().HaveInsertTexts("from")
                    .And.NotContainInsertTexts("Exception", "def", "abs")
                    .And.Subject.ApplicableSpan.Should().Be(1, 17, 1, 19);
            }

            analysis = await GetAnalysisAsync("raise Exception, x, y", version);

            result = cs.GetCompletions(analysis, new SourceLocation(1, 17));
            result.Should().HaveAnyCompletions();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 20));
            result.Should().HaveAnyCompletions();
        }

        [TestMethod, Priority(0)]
        public async Task InExcept() {
            var analysis = await GetAnalysisAsync("try:\n    pass\nexcept ");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(3, 8));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            analysis = await GetAnalysisAsync("try:\n    pass\nexcept (");
            result = cs.GetCompletions(analysis, new SourceLocation(3, 9));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            analysis = await GetAnalysisAsync("try:\n    pass\nexcept Exception  as ");
            result = cs.GetCompletions(analysis, new SourceLocation(3, 8));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 18));
            result.Should().HaveInsertTexts("as").And.NotContainInsertTexts("def", "abs");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 22));
            result.Should().HaveNoCompletion();

            analysis = await GetAnalysisAsync("try:\n    pass\nexc");
            result = cs.GetCompletions(analysis, new SourceLocation(3, 18));
            result.Should().HaveInsertTexts("except", "def", "abs");

            analysis = await GetAnalysisAsync("try:\n    pass\nexcept Exception a");
            result = cs.GetCompletions(analysis, new SourceLocation(3, 19));
            result.Should().HaveInsertTexts("as")
                .And.NotContainInsertTexts("Exception", "def", "abs")
                .And.Subject.ApplicableSpan.Should().Be(3, 18, 3, 19);
        }

        [TestMethod, Priority(0)]
        public async Task AfterDot() {
            const string code = @"
x = 1
x. 
x.(  )
x(x.  )
x.  
x  
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(3, 3));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 4));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 5));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = cs.GetCompletions(analysis, new SourceLocation(4, 3));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = cs.GetCompletions(analysis, new SourceLocation(5, 5));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = cs.GetCompletions(analysis, new SourceLocation(6, 4));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = cs.GetCompletions(analysis, new SourceLocation(7, 2));
            result.Should().HaveLabels("abs").And.NotContainLabels("real", @"imag");
        }

        [TestMethod, Priority(0)]
        public async Task AfterAssign() {
            var analysis = await GetAnalysisAsync("x = x\ny = ");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(2, 4));
            result.Should().HaveLabels("x", "abs");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 5));
            result.Should().HaveLabels("x", "abs");
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task MethodFromBaseClass(bool is3x) {
            const string code = @"
import unittest
class Simple(unittest.TestCase):
    def test_exception(self):
        self.assertRaises(TypeError).
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(5, 38));
            result.Should().HaveInsertTexts("exception");
        }

        [TestMethod, Priority(0)]
        public async Task WithWhitespaceAroundDot() {
            const string code = @"import sys
sys  .  version
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(2, 7));
            result.Should().HaveLabels("argv");
        }

        [TestMethod, Priority(0)]
        public async Task MarkupKindValid() {
            var analysis = await GetAnalysisAsync("import sys\nsys.\n");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(2, 5));
            result.Completions?.Select(i => i.documentation.kind)
                .Should().NotBeEmpty().And.BeSubsetOf(new[] { MarkupKind.PlainText, MarkupKind.Markdown });
        }

        [TestMethod, Priority(0)]
        public async Task NewType() {
            const string code = @"
from typing import NewType

Foo = NewType('Foo', dict)
foo: Foo = Foo({ })
foo.
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(6, 5));
            result.Should().HaveLabels("clear", "copy", "items", "keys", "update", "values");
        }

        [TestMethod, Priority(0)]
        public async Task GenericListBase() {
            const string code = @"
from typing import List

def func(a: List[str]):
    a.
    a[0].
    pass
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(5, 7));
            result.Should().HaveLabels("clear", "copy", "count", "index", "remove", "reverse");

            result = cs.GetCompletions(analysis, new SourceLocation(6, 10));
            result.Should().HaveLabels("capitalize");
        }

        [TestMethod, Priority(0)]
        public async Task GenericDictBase() {
            const string code = @"
from typing import Dict

def func(a: Dict[int, str]):
    a.
    a[0].
    pass
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(5, 7));
            result.Should().HaveLabels("keys", "values");

            result = cs.GetCompletions(analysis, new SourceLocation(6, 10));
            result.Should().HaveLabels("capitalize");
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassMethod() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T]):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxedint = Box(1234)
x = boxedint.

boxedstr = Box('str')
y = boxedstr.
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(14, 14));
            result.Should().HaveItem("get").Which.Should().HaveDocumentation("Box.get() -> int");
            result.Should().NotContainLabels("bit_length");

            result = cs.GetCompletions(analysis, new SourceLocation(17, 14));
            result.Should().HaveItem("get").Which.Should().HaveDocumentation("Box.get() -> str");
            result.Should().NotContainLabels("capitalize");
        }

        [TestMethod, Priority(0)]
        public async Task GenericAndRegularBases() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T], list):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxedint = Box(1234)
x = boxedint.

boxedstr = Box('str')
y = boxedstr.
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(14, 14));
            result.Should().HaveLabels("append", "index");
            result.Should().NotContainLabels("bit_length");

            result = cs.GetCompletions(analysis, new SourceLocation(17, 14));
            result.Should().HaveLabels("append", "index");
            result.Should().NotContainLabels("capitalize");
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRef() {
            const string code = @"
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
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var completionInD = cs.GetCompletions(analysis, new SourceLocation(3, 5));
            var completionInOar = cs.GetCompletions(analysis, new SourceLocation(5, 9));
            var completionForAbc = cs.GetCompletions(analysis, new SourceLocation(5, 13));

            completionInD.Should().HaveLabels("C", "D")
                .And.NotContainLabels("a", "abc", "self", "x", "fob", "baz");

            completionInOar.Should().HaveLabels("C", "D", "a", "abc", "self", "x")
                .And.NotContainLabels("fob", "baz");

            completionForAbc.Should().HaveLabels("baz", "fob");
        }

        [TestMethod, Priority(0)]
        public async Task SimpleGlobals() {
            const string code = @"
class x(object):
    def abc(self):
        pass
        
a = x()
x.abc()
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var objectMemberNames = analysis.Document.Interpreter.GetBuiltinType(BuiltinTypeId.Object).GetMemberNames();

            var completion = cs.GetCompletions(analysis, new SourceLocation(7, 1));
            var completionX = cs.GetCompletions(analysis, new SourceLocation(7, 3));

            completion.Should().HaveLabels("a", "x").And.NotContainLabels("abc", "self");
            completionX.Should().HaveLabels(objectMemberNames).And.HaveLabels("abc");
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task InFunctionDefinition(bool is3X) {
            var version = is3X ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X;

            var analysis = await GetAnalysisAsync("def f(a, b:int, c=2, d:float=None): pass", version);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 5));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 7));
            result.Should().HaveNoCompletion();
            result = cs.GetCompletions(analysis, new SourceLocation(1, 8));
            result.Should().HaveNoCompletion();
            result = cs.GetCompletions(analysis, new SourceLocation(1, 10));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 14));
            result.Should().HaveLabels("int");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 17));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 29));
            result.Should().HaveLabels("float");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 34));
            result.Should().HaveLabels("NotImplemented");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 35));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 36));
            result.Should().HaveLabels("any");
        }

        [TestMethod, Priority(0)]
        public async Task InFunctionDefinition_2X() {
            var analysis = await GetAnalysisAsync("@dec" + Environment.NewLine + "def  f(): pass", PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 1));
            result.Should().HaveLabels("any");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 2));
            result.Should().HaveLabels("abs").And.NotContainLabels("def");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 1));
            result.Should().HaveLabels("def");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 2));
            result.Should().HaveLabels("def");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 5));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(2, 6));
            result.Should().HaveNoCompletion();
        }

        [TestMethod, Priority(0)]
        public async Task InFunctionDefinition_3X() {
            var analysis = await GetAnalysisAsync("@dec" + Environment.NewLine + "async   def  f(): pass", PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 1));
            result.Should().HaveLabels("any");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 2));
            result.Should().HaveLabels("abs").And.NotContainLabels("def");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 1));
            result.Should().HaveLabels("def");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 12));
            result.Should().HaveLabels("def");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 13));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(2, 14));
            result.Should().HaveNoCompletion();
        }

        [DataRow(false)]
        [DataRow(true)]
        [TestMethod, Priority(0)]
        public async Task InClassDefinition(bool is3x) {
            var version = is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X;

            var analysis = await GetAnalysisAsync("class C(object, parameter=MC): pass", version);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 8));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 9));
            if (is3x) {
                result.Should().HaveLabels(@"metaclass=", "object");
            } else {
                result.Should().HaveLabels("object").And.NotContainLabels(@"metaclass=");
            }

            result = cs.GetCompletions(analysis, new SourceLocation(1, 15));
            result.Should().HaveLabels("any");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 17));
            result.Should().HaveLabels("any");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 29));
            result.Should().HaveLabels("object");

            result = cs.GetCompletions(analysis, new SourceLocation(1, 30));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 31));
            result.Should().HaveLabels("any");

            analysis = await GetAnalysisAsync("class D(o", version);
            result = cs.GetCompletions(analysis, new SourceLocation(1, 8));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 9));
            result.Should().HaveLabels("any");

            analysis = await GetAnalysisAsync(@"class E(metaclass=MC,o): pass", version);
            result = cs.GetCompletions(analysis, new SourceLocation(1, 22));
            result.Should().HaveLabels("object").And.NotContainLabels(@"metaclass=");
        }

        [TestMethod, Priority(0)]
        public async Task InWithStatement() {
            var analysis = await GetAnalysisAsync("with x as y, z as w: pass");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 6));
            result.Should().HaveAnyCompletions();

            result = cs.GetCompletions(analysis, new SourceLocation(1, 8));
            result.Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");
            result = cs.GetCompletions(analysis, new SourceLocation(1, 11));
            result.Should().HaveNoCompletion();
            result = cs.GetCompletions(analysis, new SourceLocation(1, 14));
            result.Should().HaveAnyCompletions();
            result = cs.GetCompletions(analysis, new SourceLocation(1, 17));
            result.Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");
            result = cs.GetCompletions(analysis, new SourceLocation(1, 21));
            result.Should().HaveAnyCompletions();


            analysis = await GetAnalysisAsync("with ");
            result = cs.GetCompletions(analysis, new SourceLocation(1, 6));
            result.Should().HaveAnyCompletions();

            analysis = await GetAnalysisAsync("with x ");
            result = cs.GetCompletions(analysis, new SourceLocation(1, 6));
            result.Should().HaveAnyCompletions();
            result = cs.GetCompletions(analysis, new SourceLocation(1, 8));
            result.Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");

            analysis = await GetAnalysisAsync("with x as ");
            result = cs.GetCompletions(analysis, new SourceLocation(1, 6));
            result.Should().HaveAnyCompletions();
            result = cs.GetCompletions(analysis, new SourceLocation(1, 8));
            result.Should().HaveInsertTexts("as").And.NotContainInsertTexts("abs", "dir");
            result = cs.GetCompletions(analysis, new SourceLocation(1, 11));
            result.Should().HaveNoCompletion();
        }


        [TestMethod, Priority(0)]
        public async Task ImportInPackage() {
            var module1Path = TestData.GetTestSpecificUri("package", "module1.py");
            var module2Path = TestData.GetTestSpecificUri("package", "module2.py");
            var module3Path = TestData.GetTestSpecificUri("package", "sub_package", "module3.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var module1 = rdt.OpenDocument(module1Path, "import package.");
            var module2 = rdt.OpenDocument(module2Path, "import package.sub_package.");
            var module3 = rdt.OpenDocument(module3Path, "import package.");

            var analysis1 = await module1.GetAnalysisAsync(-1);
            var analysis2 = await module2.GetAnalysisAsync(-1);
            var analysis3 = await module3.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis1, new SourceLocation(1, 16));
            result.Should().OnlyHaveLabels("module2", "sub_package");
            result = cs.GetCompletions(analysis2, new SourceLocation(1, 16));
            result.Should().OnlyHaveLabels("module1", "sub_package");
            result = cs.GetCompletions(analysis2, new SourceLocation(1, 28));
            result.Should().OnlyHaveLabels("module3");
            result = cs.GetCompletions(analysis3, new SourceLocation(1, 16));
            result.Should().OnlyHaveLabels("module1", "module2", "sub_package");
        }


        [TestMethod, Priority(0)]
        public async Task InImport() {
            var code = @"
import unittest.case as C, unittest
from unittest.case import TestCase as TC, TestCase
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(2, 7));
            result.Should().HaveLabels("from", "import", "abs", "dir").And.NotContainLabels("abc");
            result = cs.GetCompletions(analysis, new SourceLocation(2, 8));
            result.Should().HaveLabels("abc", @"unittest").And.NotContainLabels("abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 17));
            result.Should().HaveLabels("case").And.NotContainLabels("abc", @"unittest", "abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 23));
            result.Should().HaveLabels("as").And.NotContainLabels("abc", @"unittest", "abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(2, 25));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(2, 28));
            result.Should().HaveLabels("abc", @"unittest").And.NotContainLabels("abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 5));
            result.Should().HaveLabels("from", "import", "abs", "dir").And.NotContainLabels("abc");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 6));
            result.Should().HaveLabels("abc", @"unittest").And.NotContainLabels("abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 15));
            result.Should().HaveLabels("case").And.NotContainLabels("abc", @"unittest", "abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 20));
            result.Should().HaveLabels("import").And.NotContainLabels("abc", @"unittest", "abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 22));
            result.Should().HaveLabels("import")
                .And.NotContainLabels("abc", @"unittest", "abs", "dir")
                .And.Subject.ApplicableSpan.Should().Be(3, 20, 3, 26);

            result = cs.GetCompletions(analysis, new SourceLocation(3, 27));
            result.Should().HaveLabels("TestCase").And.NotContainLabels("abs", "dir", "case");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 36));
            result.Should().HaveLabels("as").And.NotContainLabels("abc", @"unittest", "abs", "dir");

            result = cs.GetCompletions(analysis, new SourceLocation(3, 39));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(3, 44));
            result.Should().HaveLabels("TestCase").And.NotContainLabels("abs", "dir", "case");

            code = @"
from unittest.case imp
pass
";
            analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            result = cs.GetCompletions(analysis, new SourceLocation(2, 22));
            result.Should().HaveLabels("import")
                .And.NotContainLabels("abc", @"unittest", "abs", "dir")
                .And.Subject.ApplicableSpan.Should().Be(2, 20, 2, 23);

            code = @"
import unittest.case a
pass";
            analysis = await GetAnalysisAsync(code);
            result = cs.GetCompletions(analysis, new SourceLocation(2, 23));
            result.Should().HaveLabels("as")
                .And.NotContainLabels("abc", @"unittest", "abs", "dir")
                .And.Subject.ApplicableSpan.Should().Be(2, 22, 2, 23);

            code = @"
from unittest.case import TestCase a
pass";
            analysis = await GetAnalysisAsync(code);
            result = cs.GetCompletions(analysis, new SourceLocation(2, 37));
            result.Should().HaveLabels("as")
                .And.NotContainLabels("abc", @"unittest", "abs", "dir")
                .And.Subject.ApplicableSpan.Should().Be(2, 36, 2, 37);
        }

        [TestMethod, Priority(0)]
        public async Task ForOverride() {
            const string code = @"
class A(object):
    def i(): pass
    def 
pass";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(3, 9));
            result.Should().HaveNoCompletion();

            result = cs.GetCompletions(analysis, new SourceLocation(4, 8));
            result.Should().HaveInsertTexts("def").And.NotContainInsertTexts("__init__");

            result = cs.GetCompletions(analysis, new SourceLocation(4, 9));
            result.Should().HaveLabels("__init__").And.NotContainLabels("def");
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task NoCompletionInEllipsis(bool is2x) {
            const string code = "...";
            var analysis = await GetAnalysisAsync(code, is2x ? PythonVersions.LatestAvailable2X : PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 4));
            result.Should().HaveNoCompletion();
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task NoCompletionInString(bool is2x) {
            var analysis = await GetAnalysisAsync("\"str.\"", is2x ? PythonVersions.LatestAvailable2X : PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(1, 6));
            result.Should().HaveNoCompletion();
        }

        [TestMethod, Priority(0)]
        public async Task NoCompletionInOpenString() {
            var analysis = await GetAnalysisAsync("'''.");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(1, 5));
            result.Should().HaveNoCompletion();
        }

        [DataRow("f'.")]
        [DataRow("f'a.")]
        [DataRow("f'a.'")]
        [DataTestMethod, Priority(0)]
        public async Task NoCompletionInFStringConstant(string openFString) {
            var analysis = await GetAnalysisAsync(openFString);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(1, 5));
            result.Should().HaveNoCompletion();
        }

        [TestMethod, Priority(0)]
        public async Task NoCompletionBadImportExpression() {
            var analysis = await GetAnalysisAsync("import os,.");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            cs.GetCompletions(analysis, new SourceLocation(1, 12)); // Should not crash.
        }

        [TestMethod, Priority(0)]
        public async Task NoCompletionInComment() {

            var analysis = await GetAnalysisAsync("x = 1 #str. more text");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(1, 12));
            result.Should().HaveNoCompletion();
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task OsMembers(bool is3x) {
            const string code = @"
import os
os.
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(3, 4));
            result.Should().HaveLabels("path", @"devnull", "SEEK_SET", @"curdir");
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task OsPathMembers(bool is3x) {
            const string code = @"
import os
os.path.
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(3, 9));
            result.Should().HaveLabels("split", @"getsize", @"islink", @"abspath");
        }

        [TestMethod, Priority(0)]
        public async Task FromDotInRoot() {
            const string code = "from .";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 7));
            result.Should().HaveNoCompletion();
        }

        [TestMethod, Priority(0)]
        public async Task FromDotInRootWithInitPy() {
            var initPyPath = TestData.GetTestSpecificUri("__init__.py");
            var module1Path = TestData.GetTestSpecificUri("module1.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(initPyPath, string.Empty);
            var module1 = rdt.OpenDocument(module1Path, "from .");
            module1.Interpreter.ModuleResolution.GetOrLoadModule("__init__");

            var analysis = await module1.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(1, 7));
            result.Should().OnlyHaveLabels("__dict__", "__file__", "__doc__", "__package__", "__debug__", "__name__", "__path__");
        }

        [TestMethod, Priority(0)]
        public async Task FromDotInExplicitPackage() {
            var initPyPath = TestData.GetTestSpecificUri("package", "__init__.py");
            var module1Path = TestData.GetTestSpecificUri("package", "module1.py");
            var module2Path = TestData.GetTestSpecificUri("package", "module2.py");
            var module3Path = TestData.GetTestSpecificUri("package", "sub_package", "module3.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var analyzer = Services.GetService<IPythonAnalyzer>();

            rdt.OpenDocument(initPyPath, "answer = 42");
            var module = rdt.OpenDocument(module1Path, "from .");
            rdt.OpenDocument(module2Path, string.Empty);
            rdt.OpenDocument(module3Path, string.Empty);

            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await module.GetAnalysisAsync(-1);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 7));
            result.Should().HaveLabels("module2", "sub_package", "answer");
        }

        [TestMethod, Priority(0)]
        public async Task FromPartialName() {
            var initPyPath = TestData.GetTestSpecificUri("package", "__init__.py");
            var module1Path = TestData.GetTestSpecificUri("package", "module1.py");
            var module2Path = TestData.GetTestSpecificUri("package", "sub_package", "module2.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var module = rdt.OpenDocument(initPyPath, "answer = 42");
            var module1 = rdt.OpenDocument(module1Path, "from pa");
            var module2 = rdt.OpenDocument(module2Path, "from package.su");
            module1.Interpreter.ModuleResolution.GetOrLoadModule("package");

            await module.GetAnalysisAsync(-1);
            var analysis1 = await module1.GetAnalysisAsync(-1);
            var analysis2 = await module2.GetAnalysisAsync(-1);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis1, new SourceLocation(1, 8));
            result.Should().HaveLabels("package").And.NotContainLabels("module2", "sub_package", "answer");
            result = cs.GetCompletions(analysis2, new SourceLocation(1, 16));
            result.Should().HaveLabels("module1", "sub_package", "answer");
        }

        [TestMethod, Priority(0)]
        public async Task FromDotInImplicitPackage() {
            var module1 = TestData.GetTestSpecificUri("package", "module1.py");
            var module2 = TestData.GetTestSpecificUri("package", "module2.py");
            var module3 = TestData.GetTestSpecificUri("package", "sub_package", "module3.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var module = rdt.OpenDocument(module1, "from .");
            rdt.OpenDocument(module2, string.Empty);
            rdt.OpenDocument(module3, string.Empty);

            var analysis = await module.GetAnalysisAsync(-1);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 7));
            result.Should().OnlyHaveLabels("module2", "sub_package");
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task FromOsPathAs(bool is3x) {
            const string code = @"
from os.path import exists as EX
E
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(3, 2));
            result.Should().HaveLabels("EX");

            var doc = is3x ? "exists(path: str) -> bool*" : "exists(path: unicode) -> bool*";
            result.Completions.FirstOrDefault(c => c.label == "EX").Should().HaveDocumentation(doc);
        }

        [TestMethod, Priority(0)]
        public async Task NoDuplicateMembers() {
            const string code = @"import sy";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(1, 10));
            result.Completions.Count(c => c.label.EqualsOrdinal(@"sys")).Should().Be(1);
            result.Completions.Count(c => c.label.EqualsOrdinal(@"sysconfig")).Should().Be(1);
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task ExtraClassMembers(bool is3x) {
            const string code = @"
class A: ...
a = A()
a.
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var extraMembers = new[] { "mro", "__dict__", @"__weakref__" };
            var result = cs.GetCompletions(analysis, new SourceLocation(4, 3));
            if (is3x) {
                result.Should().HaveLabels(extraMembers);
            } else {
                result.Should().NotContainLabels(extraMembers);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AzureFunctions() {
            const string code = @"
import azure.functions as func

def main(req: func.HttpRequest) -> func.HttpResponse:
    name = req.params.
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var v = analysis.GlobalScope.Variables["func"];
            v.Should().NotBeNull();
            if (v.Value.GetPythonType<IPythonModule>().ModuleType == ModuleType.Unresolved) {
                var ver = analysis.Document.Interpreter.Configuration.Version;
                Assert.Inconclusive(
                    $"'azure.functions' package is not installed for Python {ver}, see https://github.com/Microsoft/python-language-server/issues/462");
            }

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(5, 23));
            result.Should().HaveLabels("get");
            result.Completions.First(x => x.label == "get").Should().HaveDocumentation("dict.get*");
        }

        [TestMethod, Priority(0)]
        public async Task InForEnumeration() {
            var analysis = await GetAnalysisAsync(@"
for a, b in x:
    
");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(3, 4));
            result.Should().HaveLabels("a", "b");
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task NoCompletionForCurrentModuleName(bool empty) {
            var modulePath = TestData.GetNextModulePath();
            var code = empty ? string.Empty : $"{Path.GetFileNameWithoutExtension(modulePath)}.";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X, null, modulePath);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = cs.GetCompletions(analysis, new SourceLocation(1, code.Length + 1));
            result.Should().NotContainLabels(analysis.Document.Name);
        }

        [TestMethod, Priority(0)]
        public async Task OddNamedFile() {
            const string code = @"
import sys
sys.
";
            var uri = await TestData.CreateTestSpecificFileAsync("a.b.py", code);
            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri.AbsolutePath);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var doc = rdt.OpenDocument(uri, null, uri.AbsolutePath);

            var analysis = await GetDocumentAnalysisAsync(doc);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var completions = cs.GetCompletions(analysis, new SourceLocation(3, 5));
            completions.Should().HaveLabels("argv", "path", "exit");
        }

        [TestMethod, Priority(0)]
        public async Task FunctionScope() {
            const string code = @"
def func():
    aaa = 1
a";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(4, 2));
            result.Completions.Select(c => c.label).Should().NotContain("aaa");
        }

        [TestMethod, Priority(0)]
        public async Task PrivateMembers() {
            const string code = @"
class A:
    def __init__(self):
        self.__x = 123

    def func(self):
        self.

A().
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = cs.GetCompletions(analysis, new SourceLocation(7, 14));
            result.Completions.Select(c => c.label).Should().Contain("__x").And.NotContain("_A__x");

            result = cs.GetCompletions(analysis, new SourceLocation(9, 5));
            result.Completions.Select(c => c.label).Should().NotContain("_A__x").And.NotContain("__x");
        }

        [TestMethod, Priority(0)]
        public async Task ParameterAnnotatedDefault() {
            const string code = @"
from typing import Any

class Foo:
    z: int
    def __init__(self, name: str):
        self.name = name

def func() -> Any:
    return 123

def test(x: Foo = func()):
    x.
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(13, 7));
            comps.Should().HaveLabels("name", "z");
        }

        [TestMethod, Priority(0)]
        public async Task AddBrackets() {
            const string code = @"prin";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);

            ServerSettings.completion.addBrackets = true;
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var comps = cs.GetCompletions(analysis, new SourceLocation(1, 5));
            var print = comps.Completions.FirstOrDefault(x => x.label == "print");
            print.Should().NotBeNull();
            print.insertText.Should().Be("print($0)");

            cs.Options.addBrackets = false;
            comps = cs.GetCompletions(analysis, new SourceLocation(1, 5));
            print = comps.Completions.FirstOrDefault(x => x.label == "print");
            print.Should().NotBeNull();
            print.insertText.Should().Be("print");
        }

        [TestMethod, Priority(0)]
        public async Task ClassMemberAccess() {
            const string code = @"
class A:
    class B: ...

    x1 = 1

    def __init__(self):
        self.x2 = 1

    def method1(self):
        return self.

    def method2(self):
        
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var comps = cs.GetCompletions(analysis, new SourceLocation(11, 21));
            var names = comps.Completions.Select(c => c.label);
            names.Should().Contain(new[] { "x1", "x2", "method1", "method2", "B" });

            comps = cs.GetCompletions(analysis, new SourceLocation(14, 8));
            names = comps.Completions.Select(c => c.label);
            names.Should().NotContain(new[] { "x1", "x2", "method1", "method2", "B" });
        }
    }
}
