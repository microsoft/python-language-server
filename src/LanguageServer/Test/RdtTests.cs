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
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class RdtTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();


        [TestMethod, Priority(0)]
        public async Task OpenCloseDocuments() {
            const string code1 = @"x = ";
            const string code2 = @"y = ";
            var uri1 = TestData.GetDefaultModuleUri();
            var uri2 = TestData.GetNextModuleUri();

            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri1.AbsolutePath);
            var ds = Services.GetService<IDiagnosticsService>();
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(uri1, code1);
            rdt.OpenDocument(uri2, code2);

            var doc1 = rdt.GetDocument(uri1);
            var doc2 = rdt.GetDocument(uri2);

            await doc1.GetAnalysisAsync(-1);
            await doc2.GetAnalysisAsync(-1);

            ds.Diagnostics[doc1.Uri].Count.Should().Be(1);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);

            rdt.CloseDocument(uri1);

            ds.Diagnostics[uri2].Count.Should().Be(1);
            rdt.GetDocument(uri1).Should().BeNull();
            ds.Diagnostics.TryGetValue(uri1, out _).Should().BeFalse();
        }

        [TestMethod, Priority(0)]
        public async Task LockCount() {
            var uri1 = TestData.GetDefaultModuleUri();
            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri1.AbsolutePath);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(uri1, "from LockCount1 import *");
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();

            var docLc1 = rdt.GetDocuments().First(d => d.Name.Contains("LockCount1"));
            var docLc2 = rdt.GetDocuments().First(d => d.Name.Contains("LockCount2"));
            var docLc3 = rdt.GetDocuments().First(d => d.Name.Contains("LockCount3"));

            VerifyLockCount(rdt, docLc1.Uri, 1);
            VerifyLockCount(rdt, docLc2.Uri, 1);
            VerifyLockCount(rdt, docLc3.Uri, 1);

            rdt.OpenDocument(docLc1.Uri, null);
            VerifyLockCount(rdt, docLc1.Uri, 2);

            rdt.OpenDocument(docLc3.Uri, null);
            VerifyLockCount(rdt, docLc3.Uri, 2);

            rdt.CloseDocument(docLc1.Uri);
            VerifyLockCount(rdt, docLc1.Uri, 1);

            rdt.CloseDocument(docLc3.Uri);
            VerifyLockCount(rdt, docLc3.Uri, 1);
        }

        [TestMethod, Priority(0)]
        public async Task OpenCloseAnalysis() {
            const string lockCount1 = "LockCount1.py";
            const string lockCount2 = "LockCount2.py";
            const string lockCount3 = "LockCount3.py";

            var uri = TestData.GetDefaultModuleUri();
            var testSourceDataPath = GetAnalysisTestDataFilesPath();
            var testCasePath = Path.GetDirectoryName(uri.LocalPath);

            var lockCount1Path = TestData.GetTestSpecificPath(lockCount1);
            var lockCount2Path = TestData.GetTestSpecificPath(lockCount2);
            var lockCount3Path = TestData.GetTestSpecificPath(lockCount3);

            File.Copy(Path.Combine(testSourceDataPath, lockCount1), lockCount1Path);
            File.Copy(Path.Combine(testSourceDataPath, lockCount2), lockCount2Path);
            File.Copy(Path.Combine(testSourceDataPath, lockCount3), lockCount3Path);

            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri.AbsolutePath);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(uri, "from LockCount1 import *");
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();

            var docLc1 = rdt.GetDocument(new Uri(lockCount1Path));
            var docLc2 = rdt.GetDocument(new Uri(lockCount2Path));
            var docLc3 = rdt.GetDocument(new Uri(lockCount3Path));

            var ds = GetDiagnosticsService();
            PublishDiagnostics();
            ds.Diagnostics.Count.Should().Be(4);
            ds.Diagnostics[uri].Should().BeEmpty();
            ds.Diagnostics[docLc1.Uri].Should().BeEmpty();
            ds.Diagnostics[docLc2.Uri].Should().BeEmpty();
            ds.Diagnostics[docLc3.Uri].Should().BeEmpty();

            rdt.OpenDocument(docLc1.Uri, null);
            PublishDiagnostics();
            ds.Diagnostics[uri].Should().BeEmpty();
            ds.Diagnostics[docLc1.Uri].Count.Should().Be(3);
            ds.Diagnostics[docLc2.Uri].Should().BeEmpty();
            ds.Diagnostics[docLc3.Uri].Should().BeEmpty();

            rdt.CloseDocument(docLc1.Uri);
            PublishDiagnostics();
            ds.Diagnostics[uri].Should().BeEmpty();
            ds.Diagnostics[docLc1.Uri].Should().BeEmpty();
            ds.Diagnostics[docLc2.Uri].Should().BeEmpty();
            ds.Diagnostics[docLc3.Uri].Should().BeEmpty();

            rdt.OpenDocument(docLc1.Uri, null);
            var analysis = await docLc1.GetAnalysisAsync(-1);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);

            PublishDiagnostics();
            ds.Diagnostics[uri].Should().BeEmpty();
            ds.Diagnostics[docLc1.Uri].Count.Should().Be(3);
            ds.Diagnostics[docLc2.Uri].Should().BeEmpty();
            ds.Diagnostics[docLc3.Uri].Should().BeEmpty();
        }

        [DataRow("a.b.py")]
        [DataRow("a-b.py")]
        [DataRow("01.py")]
        [DataTestMethod, Priority(0)]
        public async Task OpenOddNames(string fileName) {
            var uri = await TestData.CreateTestSpecificFileAsync(fileName, "x = 1");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X, uri.AbsolutePath);
            var rdt = Services.GetService<IRunningDocumentTable>();
            rdt.OpenDocument(uri, null, uri.AbsolutePath);
        }

        private void VerifyLockCount(IRunningDocumentTable rdt, Uri uri, int expected) {
            rdt.LockDocument(uri).Should().Be(expected + 1);
            rdt.UnlockDocument(uri);
        }
    }
}
