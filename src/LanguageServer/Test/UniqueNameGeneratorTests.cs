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

using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.LanguageServer.Utilities;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.UnitTests.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class UniqueNameGeneratorTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task NoConflict() {
            MarkupUtils.GetPosition(@"$$", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name");
            Test(analysis, "name", "name");
        }

        [TestMethod, Priority(0)]
        public async Task Conflict_TopLevel() {
            MarkupUtils.GetPosition(@"$$

name = 1", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name1");
            Test(analysis, "name", "name1");
        }

        [TestMethod, Priority(0)]
        public async Task Conflict_TopLevel2() {
            MarkupUtils.GetPosition(@"$$

class name:
    pass", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name1");
            Test(analysis, "name", "name1");
        }

        [TestMethod, Priority(0)]
        public async Task Conflict_Function() {
            MarkupUtils.GetPosition(@"def Test():
    $$

name = 1", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name1");
            Test(analysis, "name", "name1");
        }

        [TestMethod, Priority(0)]
        public async Task Conflict_Function2() {
            MarkupUtils.GetPosition(@"def Test():
    name = 1
    $$
    pass", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name1");
            Test(analysis, "name", "name1");
        }

        [TestMethod, Priority(0)]
        public async Task Conflict_Function3() {
            MarkupUtils.GetPosition(@"def Test():
    name = 1

def Test2():
    $$
    pass", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name");
            Test(analysis, "name", "name1");
        }

        [TestMethod, Priority(0)]
        public async Task Conflict_TopLevel3() {
            MarkupUtils.GetPosition(@"def Test():
    name = 1

$$", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name");
            Test(analysis, "name", "name1");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleConflicts() {
            MarkupUtils.GetPosition(@"
name1 = 1

class name3:
    name2 = 1

def Test():
    name = 1

    def name4():
        pass
    
$$", out var code, out int position);

            var analysis = await GetAnalysisAsync(code);
            Test(analysis, position, "name", "name");
            Test(analysis, "name", "name5");
        }

        private static void Test(IDocumentAnalysis analysis, int position, string name, string expected) {
            var actual = UniqueNameGenerator.Generate(analysis, position, name);
            actual.Should().Be(expected);
        }

        private static void Test(IDocumentAnalysis analysis, string name, string expected) {
            Test(analysis, position: -1, name, expected);
        }
    }
}
