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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ParserTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task RedeclareGlobal() {
            const string code = @"
def testGlobal(self):
    # 'global' NAME (',' NAME)*
    global a
    global a, b
";
            var analysis = await GetAnalysisAsync(code);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task RedeclareNonlocal() {
            const string code = @"
def test_nonlocal(self):
    x = 0
    y = 0
    def f():
        nonlocal x
        nonlocal x, y
";
            var analysis = await GetAnalysisAsync(code);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task DeclareNonlocalBeforeUse() {
            const string code = @"
class TestSuper(unittest.TestCase):
    def tearDown(self):
        nonlocal __class__
        __class__ = TestSuper
";
            var analysis = await GetAnalysisAsync(code);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().BeEmpty();
        }
    }
}
