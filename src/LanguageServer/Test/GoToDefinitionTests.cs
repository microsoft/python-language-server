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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

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
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(4, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(1, 7, 1, 9);

            reference = ds.FindDefinition(analysis, new SourceLocation(9, 9), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(7, 15, 7, 19);

            reference = ds.FindDefinition(analysis, new SourceLocation(9, 14), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(6, 4, 6, 5);

            reference = ds.FindDefinition(analysis, new SourceLocation(13, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(11, 9, 11, 10);

            reference = ds.FindDefinition(analysis, new SourceLocation(14, 9), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(11, 9, 11, 10);

            reference = ds.FindDefinition(analysis, new SourceLocation(17, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(11, 4, 11, 8);

            reference = ds.FindDefinition(analysis, new SourceLocation(18, 1), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(3, 0, 3, 1);

            reference = ds.FindDefinition(analysis, new SourceLocation(19, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(5, 6, 5, 7);

            reference = ds.FindDefinition(analysis, new SourceLocation(20, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(7, 8, 7, 14);
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSource() {
            const string code = @"
import sys
import logging

logging.info('')
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 9), out _);
            reference.Should().BeNull();

            reference = ds.FindDefinition(analysis, new SourceLocation(5, 3), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 7, 2, 14);

            reference = ds.FindDefinition(analysis, new SourceLocation(3, 10), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");

            reference = ds.FindDefinition(analysis, new SourceLocation(5, 11), out _);
            reference.Should().NotBeNull();
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
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 10), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");

            reference = ds.FindDefinition(analysis, new SourceLocation(3, 2), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(1, 18, 1, 21);

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 20), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceFromImport1() {
            const string code = @"from logging import A";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(1, 7), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceFromImport2() {
            const string code = @"
from MultiValues import t
x = t
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(1, 24, 1, 25);

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 25), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 0, 2, 1);
            reference.uri.AbsolutePath.Should().Contain("MultiValues.py");
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceFromImportAs() {
            const string code = @"from logging import RootLogger as rl";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(1, 23), out _);
            reference.Should().NotBeNull();
            reference.range.start.line.Should().BeGreaterThan(500);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoBuiltinObject() {
            const string code = @"
class A(object):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 12), out _);
            reference.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task GotoRelativeImportInExplicitPackage() {
            var pkgPath = TestData.GetTestSpecificUri("pkg", "__init__.py");
            var modPath = TestData.GetTestSpecificUri("pkg", "mod.py");
            var subpkgPath = TestData.GetTestSpecificUri("pkg", "subpkg", "__init__.py");
            var submodPath = TestData.GetTestSpecificUri("pkg", "submod", "__init__.py");

            var root = TestData.GetTestSpecificRootUri().AbsolutePath;
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(pkgPath, string.Empty);
            rdt.OpenDocument(modPath, "hello = 'World'");
            rdt.OpenDocument(subpkgPath, string.Empty);
            var submod = rdt.OpenDocument(submodPath, "from .. import mod");

            var analysis = await submod.GetAnalysisAsync(-1);
            var ds = new DefinitionSource(Services);
            var reference = ds.FindDefinition(analysis, new SourceLocation(1, 18), out _);
            reference.Should().NotBeNull();
            reference.uri.Should().Be(modPath);
        }

        [TestMethod, Priority(0)]
        public async Task EmptyAnalysis() {
            var analysis = await GetAnalysisAsync(string.Empty);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(new EmptyAnalysis(Services, analysis.Document), new SourceLocation(1, 1), out _);
            reference.Should().BeNull();

            reference = ds.FindDefinition(null, new SourceLocation(1, 1), out _);
            reference.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task ReCompile() {
            const string code = @"
import re
x = re.compile(r'hello', re.IGNORECASE)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 10), out _);
            reference.Should().NotBeNull();
            reference.range.start.line.Should().BeGreaterThan(0);
            reference.uri.AbsolutePath.Should().Contain("re.py");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }
    }
}
