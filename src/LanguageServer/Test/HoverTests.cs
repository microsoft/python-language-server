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
    def method(self, a:int, b) -> float:
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
            hover.contents.value.Should().Be("C.method(a: int, b) -> float\n\nReturns a float!!!");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(6, 22));
            hover.contents.value.Should().Be("a: int");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(10, 7));
            hover.contents.value.Should().Be("func(a, b)\n\nDoes nothing useful");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(14, 2));
            hover.contents.value.Should().Be("y: int");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(15, 2));
            hover.contents.value.Should().StartWith("class str\n\nstr(object='') -> str");
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        public async Task HoverSpanCheck(bool is3x) {
            const string code = @"
import datetime
datetime.datetime.now().day
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var hs = new HoverSource(new PlainTextDocumentationSource());

            await AssertHover(hs, analysis, new SourceLocation(3, 2), "module datetime*", new SourceSpan(3, 1, 3, 9));
            await AssertHover(hs, analysis, new SourceLocation(3, 11), "class datetime*", new SourceSpan(3, 9, 3, 18));
            await AssertHover(hs, analysis, new SourceLocation(3, 20), "datetime.now(tz: Optional[tzinfo]) -> datetime*", new SourceSpan(3, 18, 3, 22));
        }

        [TestMethod, Priority(0)]
        public async Task FromImportHover() {
            const string code = @"
from os import path as p
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var hs = new HoverSource(new PlainTextDocumentationSource());

            await AssertHover(hs, analysis, new SourceLocation(2, 6), "module os*", new SourceSpan(2, 6, 2, 8));
            await AssertHover(hs, analysis, new SourceLocation(2, 16), "module*", new SourceSpan(2, 16, 2, 20));
            await AssertHover(hs, analysis, new SourceLocation(2, 24), "module*", new SourceSpan(2, 24, 2, 25));
        }

        [TestMethod, Priority(0)]
        public async Task ImportAsNameHover() {
            const string code = @"
import datetime as d123
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var hs = new HoverSource(new PlainTextDocumentationSource());

            await AssertHover(hs, analysis, new SourceLocation(2, 11), "module datetime*", new SourceSpan(2, 8, 2, 16));
            await AssertHover(hs, analysis, new SourceLocation(2, 21), "module datetime*", new SourceSpan(2, 20, 2, 24));
        }

        [TestMethod, Priority(0)]
        public async Task SelfHover() {
            const string code = @"
class Base(object):
    def fob_base(self):
       pass

class Derived(Base):
    def fob_derived(self):
       self.fob_base()
       pass
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var hs = new HoverSource(new PlainTextDocumentationSource());
            await AssertHover(hs, analysis, new SourceLocation(3, 19), "class Base*", new SourceSpan(3, 18, 3, 22));
            await AssertHover(hs, analysis, new SourceLocation(8, 8), "class Derived*", new SourceSpan(8, 8, 8, 12));
        }

        [TestMethod, Priority(0)]
        public async Task HoverGenericClass() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T]):
    def __init__(self, v: _T):
        self.v = v

    def get(self) -> _T:
        return self.v

boxedint = Box(1234)
x = boxedint.get()

boxedstr = Box('str')
y = boxedstr.get()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var hs = new HoverSource(new PlainTextDocumentationSource());
            await AssertHover(hs, analysis, new SourceLocation(14, 15), "Box.get() -> int", new SourceSpan(14, 13, 14, 17));
            await AssertHover(hs, analysis, new SourceLocation(17, 15), "Box.get() -> str", new SourceSpan(17, 13, 17, 17));
        }

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
