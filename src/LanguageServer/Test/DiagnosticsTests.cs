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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class DiagnosticsTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicChange() {
            const string code = @"x = ";

            var analysis = await GetAnalysisAsync(code);
            var ds = _sm.GetService<IDiagnosticsService>();

            var doc = analysis.Document;
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);

            doc.Update(new [] {new DocumentChange {
                InsertedText = "1",
                    ReplacedSpan = new SourceSpan(1, 5, 1, 5)
            } });

            await doc.GetAnalysisAsync();
            ds.Diagnostics[doc.Uri].Count.Should().Be(0);

            doc.Update(new[] {new DocumentChange {
                InsertedText = string.Empty,
                ReplacedSpan = new SourceSpan(1, 5, 1, 6)
            } });

            await doc.GetAnalysisAsync();
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);
        }

        [TestMethod, Priority(0)]
        public async Task CloseDocument() {
            const string code = @"x = ";

            var analysis = await GetAnalysisAsync(code);
            var ds = _sm.GetService<IDiagnosticsService>();

            var doc = analysis.Document;
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);
            doc.Dispose();

            ds.Diagnostics.TryGetValue(doc.Uri, out _).Should().BeFalse();
        }
    }
}
