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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Documentation;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class HoverTests : ServerBasedTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Hover() {
            using (var s = await CreateServerAsync()) {
                var mod = await s.OpenDefaultDocumentAndGetUriAsync(@"123
'abc'
f()
def f(): pass

class C:
    def f(self):
        def g(self):
            pass
        return g

C.f
c = C()
c_g = c.f()

x = 123
x = 3.14
");

                await AssertHover(s, mod, new SourceLocation(1, 1), "int", new[] { "int" }, new SourceSpan(1, 1, 1, 4));
                await AssertHover(s, mod, new SourceLocation(2, 1), "str", new[] { "str" }, new SourceSpan(2, 1, 2, 6));
                await AssertHover(s, mod, new SourceLocation(3, 1), "function module.f()", new[] { "module.f" }, new SourceSpan(3, 1, 3, 2));
                await AssertHover(s, mod, new SourceLocation(4, 6), "function module.f()", new[] { "module.f" }, new SourceSpan(4, 5, 4, 6));

                await AssertHover(s, mod, new SourceLocation(12, 1), "class module.C", new[] { "module.C" }, new SourceSpan(12, 1, 12, 2));
                await AssertHover(s, mod, new SourceLocation(13, 1), "c: C", new[] { "module.C" }, new SourceSpan(13, 1, 13, 2));
                await AssertHover(s, mod, new SourceLocation(14, 7), "c: C", new[] { "module.C" }, new SourceSpan(14, 7, 14, 8));
                await AssertHover(s, mod, new SourceLocation(14, 9), "c.f: method f of module.C objects*", new[] { "module.C.f" }, new SourceSpan(14, 7, 14, 10));
                await AssertHover(s, mod, new SourceLocation(14, 1), $"function module.C.f.g(self)  {Environment.NewLine}declared in C.f", new[] { "module.C.f.g" }, new SourceSpan(14, 1, 14, 4));

                await AssertHover(s, mod, new SourceLocation(16, 1), "x: int, float", new[] { "int", "float" }, new SourceSpan(16, 1, 16, 2));
            }
        }

        [TestMethod, Priority(0)]
        public async Task HoverSpanCheck_V2() {
            using (var s = await CreateServerAsync(DefaultV2)) {
                var mod = await s.OpenDefaultDocumentAndGetUriAsync(@"
import datetime
datetime.datetime.now().day
");
                await AssertHover(s, mod, new SourceLocation(3, 1), "module datetime*", new[] { "datetime" }, new SourceSpan(3, 1, 3, 9));
                await AssertHover(s, mod, new SourceLocation(3, 11), "class datetime.datetime*", new[] { "datetime.datetime" }, new SourceSpan(3, 1, 3, 18));
                await AssertHover(s, mod, new SourceLocation(3, 20), "datetime.datetime.now: datetime.datetime.now(cls)*", null, new SourceSpan(3, 1, 3, 22));
            }
        }

        [TestMethod, Priority(0)]
        public async Task HoverSpanCheck_V3() {
            using (var s = await CreateServerAsync(DefaultV3)) {
                var mod = await s.OpenDefaultDocumentAndGetUriAsync(@"
import datetime
datetime.datetime.now().day
");
                await AssertHover(s, mod, new SourceLocation(3, 1), "module datetime*", new[] { "datetime" }, new SourceSpan(3, 1, 3, 9));
                await AssertHover(s, mod, new SourceLocation(3, 11), "class datetime.datetime*", new[] { "datetime.datetime" }, new SourceSpan(3, 1, 3, 18));
                await AssertHover(s, mod, new SourceLocation(3, 20), "datetime.datetime.now: datetime.datetime.now(cls*", null, new SourceSpan(3, 1, 3, 22));
                await AssertHover(s, mod, new SourceLocation(3, 28), "datetime.datetime.now().day: int*", new[] { "int" }, new SourceSpan(3, 1, 3, 28));
            }
        }

        [TestMethod, Priority(0)]
        public async Task FromImportHover() {
            using (var s = await CreateServerAsync()) {
                var mod = await s.OpenDefaultDocumentAndGetUriAsync("from os import path as p\n");
                await AssertHover(s, mod, new SourceLocation(1, 6), "module os*", null, new SourceSpan(1, 6, 1, 8));
                await AssertHover(s, mod, new SourceLocation(1, 16), "module posixpath*", new[] { "posixpath" }, new SourceSpan(1, 16, 1, 20));
                await AssertHover(s, mod, new SourceLocation(1, 24), "module posixpath*", new[] { "posixpath" }, new SourceSpan(1, 24, 1, 25));
            }
        }

        [TestMethod, Priority(0)]
        public async Task FromImportRelativeHover() {
            using (var s = await CreateServerAsync()) {
                var mod1 = await s.OpenDocumentAndGetUriAsync("mod1.py", "from . import mod2\n");
                var mod2 = await s.OpenDocumentAndGetUriAsync("mod2.py", "def foo():\n  pass\n");
                await AssertHover(s, mod1, new SourceLocation(1, 16), "module mod2", null, new SourceSpan(1, 15, 1, 19));
            }
        }

        [TestMethod, Priority(0)]
        public async Task SelfHover() {
            var text = @"
class Base(object):
    def fob_base(self):
       pass

class Derived(Base):
    def fob_derived(self):
       self.fob_base()
       pass
";
            using (var s = await CreateServerAsync()) {
                var uri = await s.OpenDefaultDocumentAndGetUriAsync(text);
                await AssertHover(s, uri, new SourceLocation(3, 19), "class module.Base(object)", null, new SourceSpan(2, 7, 2, 11));
                await AssertHover(s, uri, new SourceLocation(8, 8), "class module.Derived(Base)", null, new SourceSpan(6, 7, 6, 14));
            }
        }

        [TestMethod, Priority(0)]
        public async Task TupleFunctionArgumentsHover() {
            using (var s = await CreateServerAsync()) {
                var uri = await s.OpenDefaultDocumentAndGetUriAsync("def what(a, b):\n    return a, b, 1\n");
                await AssertHover(s, uri, new SourceLocation(1, 6), "function module.what(a, b) -> tuple[Any, Any, int]", null, new SourceSpan(1, 5, 1, 9));
            }
        }

        [TestMethod, Priority(0)]
        public async Task TupleFunctionArgumentsWithCallHover() {
            using (var s = await CreateServerAsync()) {
                var uri = await s.OpenDefaultDocumentAndGetUriAsync("def what(a, b):\n    return a, b, 1\n\nwhat(1, 2)");
                await AssertHover(s, uri, new SourceLocation(1, 6), "function module.what(a, b) -> tuple[int, int, int]", null, new SourceSpan(1, 5, 1, 9));
            }
        }

        [TestMethod, Priority(0)]
        public async Task MarkupKindValid() {
            using (var s = await CreateServerAsync()) {
                var u = await s.OpenDefaultDocumentAndGetUriAsync("123");

                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var hover = await s.Hover(new TextDocumentPositionParams {
                    textDocument = new TextDocumentIdentifier { uri = u },
                    position = new SourceLocation(1, 1),
                }, CancellationToken.None);

                hover.contents.kind.Should().BeOneOf(MarkupKind.PlainText, MarkupKind.Markdown);
            }
        }

        private static async Task AssertHover(Server s, Uri uri, SourceLocation position, string hoverText, IEnumerable<string> typeNames, SourceSpan? range = null, string expr = null) {
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
            var hover = await s.Hover(new TextDocumentPositionParams {
                textDocument = new TextDocumentIdentifier { uri = uri },
                position = position,
                _expr = expr
            }, CancellationToken.None);

            if (hoverText.EndsWith("*")) {
                // Check prefix first, but then show usual message for mismatched value
                if (!hover.contents.value.StartsWith(hoverText.Remove(hoverText.Length - 1))) {
                    Assert.AreEqual(hoverText, hover.contents.value);
                }
            } else {
                Assert.AreEqual(hoverText, hover.contents.value);
            }
            if (typeNames != null) {
                hover._typeNames.Should().OnlyContain(typeNames.ToArray());
            }
            if (range.HasValue) {
                hover.range.Should().Be(range.Value);
            }
        }
        private static InterpreterConfiguration DefaultV3 {
            get {
                var ver = PythonVersions.Python36_x64 ?? PythonVersions.Python36 ??
                          PythonVersions.Python35_x64 ?? PythonVersions.Python35;
                ver.AssertInstalled();
                return ver;
            }
        }

        private static InterpreterConfiguration DefaultV2 {
            get {
                var ver = PythonVersions.Python27_x64 ?? PythonVersions.Python27;
                ver.AssertInstalled();
                return ver;
            }
        }
    }
}
