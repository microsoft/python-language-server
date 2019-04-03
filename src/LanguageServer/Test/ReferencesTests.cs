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
using System.IO;
using System.Threading;
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
    public class ReferencesTests : LanguageServerTestBase {
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
            var rs = new ReferenceSource(Services);
            var refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(8, 1), ReferenceSearchOptions.All);

            refs.Should().HaveCount(3);
            refs[0].range.Should().Be(1, 0, 1, 1);
            refs[1].range.Should().Be(6, 9, 6, 10);
            refs[2].range.Should().Be(7, 0, 7, 1);
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
            rdt.OpenDocument(uri1, code1);
            rdt.OpenDocument(uri2, code2);

            var doc1 = rdt.GetDocument(uri1);
            var analysis = await GetDocumentAnalysisAsync(doc1);

            var rs = new ReferenceSource(Services);
            var refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(7, 10), ReferenceSearchOptions.All);

            refs.Should().HaveCount(5);

            refs[0].range.Should().Be(1, 0, 1, 1);
            refs[0].uri.Should().Be(uri1);
            refs[1].range.Should().Be(6, 9, 6, 10);
            refs[1].uri.Should().Be(uri1);
            refs[2].range.Should().Be(7, 0, 7, 1);
            refs[2].uri.Should().Be(uri1);

            refs[3].range.Should().Be(1, 19, 1, 20);
            refs[3].uri.Should().Be(uri2);
            refs[4].range.Should().Be(2, 4, 2, 5);
            refs[4].uri.Should().Be(uri2);
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
            var rs = new ReferenceSource(Services);
            var refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(7, 10), ReferenceSearchOptions.All);

            refs.Should().HaveCount(7);

            refs[0].range.Should().Be(1, 0, 1, 1);
            refs[0].uri.Should().Be(analysis.Document.Uri);
            refs[1].range.Should().Be(6, 9, 6, 10);
            refs[1].uri.Should().Be(analysis.Document.Uri);
            refs[2].range.Should().Be(7, 0, 7, 1);
            refs[2].uri.Should().Be(analysis.Document.Uri);

            refs[3].range.Should().Be(1, 19, 1, 20);
            refs[3].uri.Should().Be(uri2);
            refs[4].range.Should().Be(2, 4, 2, 5);
            refs[4].uri.Should().Be(uri2);

            refs[5].range.Should().Be(1, 19, 1, 20);
            refs[5].uri.Should().Be(uri3);
            refs[6].range.Should().Be(2, 4, 2, 5);
            refs[6].uri.Should().Be(uri3);
        }

        [TestMethod, Priority(0)]
        public async Task NestedClosedFiles() {
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
from module2 import x
y = x
";
            var uri2 = await TestData.CreateTestSpecificFileAsync("module2.py", mod2Code);
            var uri3 = await TestData.CreateTestSpecificFileAsync("module3.py", mod3Code);

            var analysis = await GetAnalysisAsync(code);

            var rs = new ReferenceSource(Services);
            var refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(7, 10), ReferenceSearchOptions.All);

            refs.Should().HaveCount(7);

            refs[0].range.Should().Be(1, 0, 1, 1);
            refs[0].uri.Should().Be(analysis.Document.Uri);
            refs[1].range.Should().Be(6, 9, 6, 10);
            refs[1].uri.Should().Be(analysis.Document.Uri);
            refs[2].range.Should().Be(7, 0, 7, 1);
            refs[2].uri.Should().Be(analysis.Document.Uri);

            refs[3].range.Should().Be(1, 19, 1, 20);
            refs[3].uri.Should().Be(uri2);
            refs[4].range.Should().Be(2, 4, 2, 5);
            refs[4].uri.Should().Be(uri2);

            refs[5].range.Should().Be(1, 20, 1, 21);
            refs[5].uri.Should().Be(uri3);
            refs[6].range.Should().Be(2, 4, 2, 5);
            refs[6].uri.Should().Be(uri3);
        }

        [TestMethod, Priority(0)]
        public async Task UnrelatedFiles() {
            const string code = @"
from bar import baz

class spam:
    __bug__ = 0

def eggs(ham: spam):
    return baz(ham.__bug__)
";
            const string barCode = @"
def baz(quux):
    pass
";
            await TestData.CreateTestSpecificFileAsync("bar.py", barCode);
            var analysis = await GetAnalysisAsync(code);

            var rs = new ReferenceSource(Services);
            var refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(5, 8), ReferenceSearchOptions.All);

            refs.Should().HaveCount(2);

            refs[0].range.Should().Be(4, 4, 4, 11);
            refs[0].uri.Should().Be(analysis.Document.Uri);
            refs[1].range.Should().Be(7, 19, 7, 26);
            refs[1].uri.Should().Be(analysis.Document.Uri);
        }

        [TestMethod, Priority(0)]
        public async Task RemoveReference() {
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
from module import x, y
a = x
b = y
";
            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri1.AbsolutePath);

            var rdt = Services.GetService<IRunningDocumentTable>();
            var doc1 = rdt.OpenDocument(uri1, code1);
            var doc2 = rdt.OpenDocument(uri2, code2);

            var analysis = await GetDocumentAnalysisAsync(doc1);

            var rs = new ReferenceSource(Services);
            var refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(7, 1), ReferenceSearchOptions.All);

            refs.Should().HaveCount(3);
            refs[0].range.Should().Be(6, 0, 6, 1);
            refs[0].uri.Should().Be(uri1);
            refs[1].range.Should().Be(1, 22, 1, 23);
            refs[1].uri.Should().Be(uri2);
            refs[2].range.Should().Be(3, 4, 3, 5);
            refs[2].uri.Should().Be(uri2);

            doc2.Update(new[] {
                new DocumentChange {
                    InsertedText = string.Empty,
                    ReplacedSpan = new SourceSpan(4, 1, 4, 6)
                },
                new DocumentChange {
                    InsertedText = string.Empty,
                    ReplacedSpan = new SourceSpan(2, 21, 2, 24)
                }
            });
            await GetDocumentAnalysisAsync(doc2);

            refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(7, 1), ReferenceSearchOptions.All);

            refs.Should().HaveCount(1);
            refs[0].range.Should().Be(6, 0, 6, 1);
            refs[0].uri.Should().Be(uri1);
        }

        [TestMethod, Priority(0)]
        public async Task UpdateReferencesOnEdit() {
            const string code = @"
import logging

logging.getLogger()
";
            var uri = TestData.GetDefaultModuleUri();
            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri.AbsolutePath);

            var rdt = Services.GetService<IRunningDocumentTable>();
            var doc = rdt.OpenDocument(uri, code);
            var analysis = await GetDocumentAnalysisAsync(doc);

            var rs = new ReferenceSource(Services);
            var refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(4, 12), ReferenceSearchOptions.All);

            refs.Should().HaveCount(2);
            refs[0].range.start.line.Should().BeGreaterThan(1);
            refs[0].uri.AbsolutePath.Should().Contain("logging");
            refs[1].range.Should().Be(3, 8, 3, 17);
            refs[1].uri.Should().Be(uri);

            doc.Update(new[] {
                new DocumentChange {
                    InsertedText = Environment.NewLine,
                    ReplacedSpan = new SourceSpan(3, 1, 3, 1)
                },
            });
            await GetDocumentAnalysisAsync(doc);

            refs = await rs.FindAllReferencesAsync(analysis.Document.Uri, new SourceLocation(5, 12), ReferenceSearchOptions.All);

            refs.Should().HaveCount(2);
            refs[0].range.start.line.Should().BeGreaterThan(1);
            refs[0].uri.AbsolutePath.Should().Contain("logging");
            refs[1].range.Should().Be(4, 8, 4, 17);
            refs[1].uri.Should().Be(uri);
        }

        [TestMethod, Priority(0)]
        public async Task EmptyAnalysis() {
            await GetAnalysisAsync(string.Empty);
            var rs = new ReferenceSource(Services);
            var references = await rs.FindAllReferencesAsync(null, new SourceLocation(1, 1), ReferenceSearchOptions.All);
            references.Should().BeEmpty();
        }
    }
}
