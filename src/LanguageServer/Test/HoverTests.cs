﻿// Copyright(c) Microsoft Corporation
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
using FluentAssertions;
using Microsoft.Python.Analysis;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class HoverTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicTypes() {
            const string code = @"
x = 'str'

class C:
    '''Class C is awesome'''
    def method(self, a, b) -> float:
        '''Returns a float!!!'''
        return 1.0

def func(a, b):
    '''Does nothing useful'''
    return 1

y = func(1, 2)
string = str
";
            var analysis = await GetAnalysisAsync(code);
            var hs = new HoverSource(new PlainTextDocumentationSource());

            var hover = await hs.GetHoverAsync(analysis, new SourceLocation(2, 2));
            hover.contents.value.Should().Be("x: str");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(2, 7));
            hover.Should().BeNull();

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(4, 7));
            hover.contents.value.Should().Be("class C\n\nClass C is awesome");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(6, 9));
            hover.contents.value.Should().Be("C.method(a, b) -> float\n\nReturns a float!!!");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(10, 7));
            hover.contents.value.Should().Be("func(a, b)\n\nDoes nothing useful");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(14, 2));
            hover.contents.value.Should().Be("y: int");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(15, 2));
            hover.contents.value.Should().StartWith("class str\n\nstr(object='') -> str");
        }

        [TestMethod, Priority(0)]
        public async Task HoverSpanCheck_V2() {
            const string code = @"
import datetime
datetime.datetime.now().day
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            var hs = new HoverSource(new PlainTextDocumentationSource());

            await AssertHover(hs, analysis, new SourceLocation(3, 2), "module datetime*", new SourceSpan(3, 1, 3, 9));
            await AssertHover(hs, analysis, new SourceLocation(3, 11), "class datetime*", new SourceSpan(3, 1, 3, 18));
            await AssertHover(hs, analysis, new SourceLocation(3, 20), "datetime.now(tz: ...) -> datetime*", new SourceSpan(3, 1, 3, 22));
        }

        [TestMethod, Priority(0)]
        public async Task HoverSpanCheck_V3() {
            const string code = @"
import datetime
datetime.datetime.now().day
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var hs = new HoverSource(new PlainTextDocumentationSource());

            await AssertHover(hs, analysis, new SourceLocation(3, 2), "module datetime*", new SourceSpan(3, 1, 3, 9));
            await AssertHover(hs, analysis, new SourceLocation(3, 11), "class datetime*", new SourceSpan(3, 1, 3, 18));
            await AssertHover(hs, analysis, new SourceLocation(3, 20), "datetime.now(tz: Optional[tzinfo]) -> datetime*", new SourceSpan(3, 1, 3, 22));
        }

        //        [TestMethod, Priority(0)]
        //        public async Task FromImportHover() {
        //            var mod = await server.OpenDefaultDocumentAndGetUriAsync("from os import path as p\n");
        //            await AssertHover(server, mod, new SourceLocation(1, 6), "module os*", null, new SourceSpan(1, 6, 1, 8));
        //            await AssertHover(server, mod, new SourceLocation(1, 16), "module posixpath*", new[] { "posixpath" }, new SourceSpan(1, 16, 1, 20));
        //            await AssertHover(server, mod, new SourceLocation(1, 24), "module posixpath*", new[] { "posixpath" }, new SourceSpan(1, 24, 1, 25));
        //        }

        //        [TestMethod, Priority(0)]
        //        public async Task FromImportRelativeHover() {
        //            var mod1 = await server.OpenDocumentAndGetUriAsync("mod1.py", "from . import mod2\n");
        //            var mod2 = await server.OpenDocumentAndGetUriAsync("mod2.py", "def foo():\n  pass\n");
        //            await AssertHover(server, mod1, new SourceLocation(1, 16), "module mod2", null, new SourceSpan(1, 15, 1, 19));
        //        }

        //        [TestMethod, Priority(0)]
        //        public async Task SelfHover() {
        //            var code =  @"
        //class Base(object):
        //    def fob_base(self):
        //       pass

        //class Derived(Base):
        //    def fob_derived(self):
        //       self.fob_base()
        //       pass
        //";
        //            var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
        //            await AssertHover(server, uri, new SourceLocation(3, 19), "class module.Base(object)", null, new SourceSpan(2, 7, 2, 11));
        //            await AssertHover(server, uri, new SourceLocation(8, 8), "class module.Derived(Base)", null, new SourceSpan(6, 7, 6, 14));
        //        }

        //        [TestMethod, Priority(0)]
        //        public async Task TupleFunctionArgumentsHover() {
        //            var uri = await server.OpenDefaultDocumentAndGetUriAsync("def what(a, b):\n    return a, b, 1\n");
        //            await AssertHover(server, uri, new SourceLocation(1, 6), "function module.what(a, b) -> tuple[Any, Any, int]", null, new SourceSpan(1, 5, 1, 9));
        //        }

        //        [TestMethod, Priority(0)]
        //        public async Task TupleFunctionArgumentsWithCallHover() {
        //            var uri = await server.OpenDefaultDocumentAndGetUriAsync("def what(a, b):\n    return a, b, 1\n\nwhat(1, 2)");
        //            await AssertHover(server, uri, new SourceLocation(1, 6), "function module.what(a, b) -> tuple[int, int, int]", null, new SourceSpan(1, 5, 1, 9));
        //        }

        private static async Task AssertHover(HoverSource hs, IDocumentAnalysis analysis, SourceLocation position, string hoverText, SourceSpan? span = null) {
            var hover = await hs.GetHoverAsync(analysis, position);

            if (hoverText.EndsWith("*")) {
                // Check prefix first, but then show usual message for mismatched value
                if (!hover.contents.value.StartsWith(hoverText.Remove(hoverText.Length - 1))) {
                    Assert.AreEqual(hoverText, hover.contents.value);
                }
            } else {
                Assert.AreEqual(hoverText, hover.contents.value);
            }
            if (span.HasValue) {
                hover.range.Should().Be((Range)span.Value);
            }
        }
    }
}
