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
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(8, 1))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain("C", "x", "y", "while", "for", "yield");
        }

        [TestMethod, Priority(0)]
        public async Task StringMembers() {
            const string code = @"
x = 'str'
x.
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 3))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain(new[] {@"isupper", @"capitalize", @"split"});
        }

        [TestMethod, Priority(0)]
        public async Task ModuleMembers() {
            const string code = @"
import datetime
datetime.datetime.
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 19))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain(new[] {"now", @"tzinfo", @"ctime"});
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
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(5, 4))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain(@"ABCDE");

            comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(6, 9))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain("method1");
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
            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 9));

            result.Should().HaveItem("append")
                .Which.Should().HaveInsertText($"append(self, {parameterName}):{Environment.NewLine}    return super().append({parameterName})")
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
            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(4, 8));
            result.Should().HaveItem("a");
        }

        [TestMethod, Priority(0)]
        public async Task TypeAtEndOfIncompleteMethod() {
            var code = @"
class Fob(object):
    def oar(self, a):
        


x = Fob()
x.oar(100)
";

            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(4, 8));
            result.Should().HaveItem("a")
                .Which.Should().HaveDocumentation("int");
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
            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(11, 3));
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

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(7, 10));
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
            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(1, 7));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            if (is3X) {
                analysis = await GetAnalysisAsync("raise Exception from ", PythonVersions.LatestAvailable3X);
                cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

                result = await cs.GetCompletionsAsync(analysis, new SourceLocation(1, 7));
                result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

                result = await cs.GetCompletionsAsync(analysis, new SourceLocation(1, 17));
                result.Should().HaveInsertTexts("from").And.NotContainInsertTexts("Exception", "def", "abs");

                result = await cs.GetCompletionsAsync(analysis, new SourceLocation(1, 22));
                result.Should().HaveAnyCompletions();

                analysis = await GetAnalysisAsync("raise Exception fr ", PythonVersions.LatestAvailable3X);
                result = await cs.GetCompletionsAsync(analysis, new SourceLocation(1, 19));
                result.Should().HaveInsertTexts("from")
                    .And.NotContainInsertTexts("Exception", "def", "abs")
                    .And.Subject.ApplicableSpan.Should().Be(0, 16, 0, 18);
            }

            analysis = await GetAnalysisAsync("raise Exception, x, y", version);

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(1, 17));
            result.Should().HaveAnyCompletions();

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(1, 20));
            result.Should().HaveAnyCompletions();
        }

        [TestMethod, Priority(0)]
        public async Task InExcept() {
            var analysis = await GetAnalysisAsync("try:\n    pass\nexcept ");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(2, 18));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            analysis = await GetAnalysisAsync("try:\n    pass\nexcept (");
            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 9));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            analysis = await GetAnalysisAsync("try:\n    pass\nexcept Exception  as ");
            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 8));
            result.Should().HaveInsertTexts("Exception", "ValueError").And.NotContainInsertTexts("def", "abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 18));
            result.Should().HaveInsertTexts("as").And.NotContainInsertTexts("def", "abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 22));
            result.Should().HaveNoCompletion();

            analysis = await GetAnalysisAsync("try:\n    pass\nexc");
            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 18));
            result.Should().HaveInsertTexts("except", "def", "abs");

            analysis = await GetAnalysisAsync("try:\n    pass\nexcept Exception a");
            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 19));
            result.Should().HaveInsertTexts("as")
                .And.NotContainInsertTexts("Exception", "def", "abs")
                .And.Subject.ApplicableSpan.Should().Be(2, 17, 2, 18);
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

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 3));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 4));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 5));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(4, 3));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(5, 5));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(6, 4));
            result.Should().HaveLabels("real", @"imag").And.NotContainLabels("abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(7, 2));
            result.Should().HaveLabels("abs").And.NotContainLabels("real", @"imag");
        }

        [TestMethod, Priority(0)]
        public async Task AfterAssign() {
            var analysis = await GetAnalysisAsync("x = x\ny = ");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(2, 4));
            result.Should().HaveLabels("x", "abs");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(2, 5));
            result.Should().HaveLabels("x", "abs");
        }

        [TestMethod, Priority(0)]
        public async Task MethodFromBaseClass2X() {
            const string code = @"
import unittest
class Simple(unittest.TestCase):
    def test_exception(self):
        self.assertRaises().
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(5, 29));
            result.Should().HaveInsertTexts("exception");
        }

        [TestMethod, Priority(0)]
        public async Task MethodFromBaseClass3X() {
            const string code = @"
import unittest
class Simple(unittest.TestCase):
    def test_exception(self):
        self.assertRaises().
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(5, 29));
            result.Should().HaveInsertTexts("exception");
        }

        [TestMethod, Priority(0)]
        public async Task WithWhitespaceAroundDot() {
            const string code = @"import sys
sys  .  version
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(2, 7));
            result.Should().HaveLabels("argv");
        }

        [TestMethod, Priority(0)]
        public async Task MarkupKindValid() {
            var analysis = await GetAnalysisAsync("import sys\nsys.\n");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(2, 5));
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

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(6, 5));
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

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(5, 7));
            result.Should().HaveLabels("clear", "copy", "count", "index", "remove", "reverse");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(6, 10));
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

            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(5, 7));
            result.Should().HaveLabels("keys", "values");

            result = await cs.GetCompletionsAsync(analysis, new SourceLocation(6, 10));
            result.Should().HaveLabels("capitalize");
        }
    }
}
