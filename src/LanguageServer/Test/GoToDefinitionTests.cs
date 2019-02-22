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
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class GoToDefinitionTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicDefinitions() {
            const string code = @"
import os

x = os.path

class C:
    z: int
    def method(self, a, b):
        self.z = 1
        return 1.0

def func(a, b):
    a = 3
    b = a
    return 1

y = func(1, 2)
x = 1
c = C()
c.method(1, 2)
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource();

            var reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(4, 5));
            reference.range.Should().Be(1, 7, 1, 9);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(9, 9));
            reference.range.Should().Be(7, 15, 7, 19);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(9, 14));
            reference.range.Should().Be(6, 4, 6, 5);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(13, 5));
            reference.range.Should().Be(11, 9, 11, 10);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(14, 9));
            reference.range.Should().Be(11, 9, 11, 10);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(17, 5));
            reference.range.Should().Be(11, 0, 14, 12);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(18, 1));
            reference.range.Should().Be(17, 0, 17, 1); // TODO: store all locations

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(19, 5));
            reference.range.Should().Be(5, 6, 9, 18);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(20, 5));
            reference.range.Should().Be(7, 4, 9, 18);
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSource() {
            const string code = @"
import sys
import logging

logging.info('')
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource();

            var reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(2, 9));
            reference.Should().BeNull();

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(5, 3));
            reference.range.Should().Be(2, 7, 2, 14);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(3, 10));
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(5, 11));
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceImportAs() {
            const string code = @"
import logging as log
log
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource();

            var reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(3, 2));
            reference.range.Should().Be(1, 18, 1, 21);

            reference = await ds.FindDefinitionAsync(analysis, new SourceLocation(2, 20));
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }
    }
}
