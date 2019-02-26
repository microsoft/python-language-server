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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
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
    }
}
