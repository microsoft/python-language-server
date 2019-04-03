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

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class RenameTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();


        [TestMethod, Priority(0)]
        public async Task SingleFile() {
            const string code = @"
x = 1

def func(x):
    return x

y = func(x)
x = 2
";
            var analysis = await GetAnalysisAsync(code);
            var rs = new RenameSource(Services);
            var wse = await rs.RenameAsync(analysis.Document.Uri, new SourceLocation(8, 1), "z");

            var uri = analysis.Document.Uri;
            wse.changes.Should().HaveCount(1);
            wse.changes[uri].Should().HaveCount(3);
            wse.changes[uri][0].range.Should().Be(1, 0, 1, 1);
            wse.changes[uri][1].range.Should().Be(6, 9, 6, 10);
            wse.changes[uri][2].range.Should().Be(7, 0, 7, 1);
            wse.changes[uri].All(x => x.newText.EqualsOrdinal("z")).Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public async Task TwoOpenFiles() {
            const string code1 = @"
x = 1

def func(x):
    return x

y = func(x)
x = 2
";
            var uri1 = TestData.GetDefaultModuleUri();
            var uri2 = TestData.GetNextModuleUri();

            var code2 = $@"
from {Path.GetFileNameWithoutExtension(uri1.AbsolutePath)} import x
y = x
";
            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri1.AbsolutePath);

            var rdt = Services.GetService<IRunningDocumentTable>();
            var doc1 = rdt.OpenDocument(uri1, code1);
            var doc2 = rdt.OpenDocument(uri2, code2);

            var analysis = await GetDocumentAnalysisAsync(doc1);
            await GetDocumentAnalysisAsync(doc2);

            var rs = new RenameSource(Services);
            var wse = await rs.RenameAsync(analysis.Document.Uri, new SourceLocation(7, 10), "z");

            wse.changes.Should().HaveCount(2);

            wse.changes[uri1].Should().HaveCount(3);
            wse.changes[uri1][0].range.Should().Be(1, 0, 1, 1);
            wse.changes[uri1][1].range.Should().Be(6, 9, 6, 10);
            wse.changes[uri1][2].range.Should().Be(7, 0, 7, 1);

            wse.changes[uri2].Should().HaveCount(2);
            wse.changes[uri2][0].range.Should().Be(1, 19, 1, 20);
            wse.changes[uri2][1].range.Should().Be(2, 4, 2, 5);
        }

        [TestMethod, Priority(0)]
        public async Task ClosedFiles() {
            const string code = @"
x = 1

def func(x):
    return x

y = func(x)
x = 2
";
            const string mod2Code = @"
from module import x
y = x
";
            const string mod3Code = @"
from module import x
y = x
";
            var uri2 = await TestData.CreateTestSpecificFileAsync("module2.py", mod2Code);
            var uri3 = await TestData.CreateTestSpecificFileAsync("module3.py", mod3Code);

            var analysis = await GetAnalysisAsync(code);
            var uri1 = analysis.Document.Uri;

            var rs = new RenameSource(Services);
            var wse = await rs.RenameAsync(analysis.Document.Uri, new SourceLocation(7, 10), "z");

            wse.changes.Should().HaveCount(3);

            wse.changes[uri1].Should().HaveCount(3);
            wse.changes[uri1][0].range.Should().Be(1, 0, 1, 1);
            wse.changes[uri1][1].range.Should().Be(6, 9, 6, 10);
            wse.changes[uri1][2].range.Should().Be(7, 0, 7, 1);

            wse.changes[uri2].Should().HaveCount(2);
            wse.changes[uri2][0].range.Should().Be(1, 19, 1, 20);
            wse.changes[uri2][1].range.Should().Be(2, 4, 2, 5);

            wse.changes[uri3].Should().HaveCount(2);
            wse.changes[uri3][0].range.Should().Be(1, 19, 1, 20);
            wse.changes[uri3][1].range.Should().Be(2, 4, 2, 5);
        }

        [TestMethod, Priority(0)]
        public async Task NoRenameInCompiled() {
            const string code = "from sys import path";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var rs = new RenameSource(Services);

            var wse = await rs.RenameAsync(analysis.Document.Uri, new SourceLocation(1, 7), "z");
            wse.Should().BeNull();

            wse = await rs.RenameAsync(analysis.Document.Uri, new SourceLocation(1, 18), "z");
            wse.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task NoRenameInLibrary() {
            const string code = @"from logging import BASIC_FORMAT";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var rs = new RenameSource(Services);

            var wse = await rs.RenameAsync(analysis.Document.Uri, new SourceLocation(1, 10), "z");
            wse.Should().BeNull();

            wse = await rs.RenameAsync(analysis.Document.Uri, new SourceLocation(1, 23), "z");
            wse.Should().BeNull();
        }
    }
}
