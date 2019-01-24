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

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.LanguageServer.Tooltips;
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
";
            var analysis = await GetAnalysisAsync(code);
            var hs = new HoverSource(new PlainTextDocSource());

            var hover = await hs.GetHoverAsync(analysis, new SourceLocation(2, 2));
            hover.contents.value.Should().Be("x: str");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(2, 7));
            hover.contents.value.Should().StartWith("class str\n\nstr(object='') -> str");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(4, 7));
            hover.contents.value.Should().Be("class C\n\nClass C is awesome");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(6, 9));
            hover.contents.value.Should().Be("C.method(a, b) -> float\n\nReturns a float!!!");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(10, 7));
            hover.contents.value.Should().Be("func(a, b)\n\nDoes nothing useful");

            hover = await hs.GetHoverAsync(analysis, new SourceLocation(14, 2));
            hover.contents.value.Should().Be("y: int");
        }
    }
}
