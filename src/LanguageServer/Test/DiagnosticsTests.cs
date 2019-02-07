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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
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
            var ds = Services.GetService<IDiagnosticsService>();

            var doc = analysis.Document;
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);

            doc.Update(new[] {new DocumentChange {
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
        public async Task TwoDocuments() {
            const string code1 = @"x = ";
            const string code2 = @"y = ";

            var analysis1 = await GetAnalysisAsync(code1);
            var analysis2 = await GetNextAnalysisAsync(code2);
            var ds = Services.GetService<IDiagnosticsService>();

            var doc1 = analysis1.Document;
            var doc2 = analysis2.Document;

            ds.Diagnostics[doc1.Uri].Count.Should().Be(1);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);

            doc2.Update(new[] {new DocumentChange {
                InsertedText = "1",
                ReplacedSpan = new SourceSpan(1, 5, 1, 5)
            } });

            await doc2.GetAnalysisAsync();
            ds.Diagnostics[doc1.Uri].Count.Should().Be(1);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(0);

            doc2.Update(new[] {new DocumentChange {
                InsertedText = string.Empty,
                ReplacedSpan = new SourceSpan(1, 5, 1, 6)
            } });

            await doc2.GetAnalysisAsync();
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);

            doc1.Dispose();
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);
            ds.Diagnostics.TryGetValue(doc1.Uri, out _).Should().BeFalse();
        }

        [TestMethod, Priority(0)]
        public async Task Publish() {
            const string code = @"x = ";

            var analysis = await GetAnalysisAsync(code);
            var doc = analysis.Document;

            var clientApp = Services.GetService<IClientApplication>();
            var idle = Services.GetService<IIdleTimeService>();

            var expected = 1;
            clientApp.When(x => x.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", Arg.Any<object>()))
                .Do(x => {
                    var dp = x.Args()[1] as PublishDiagnosticsParams;
                    dp.Should().NotBeNull();
                    dp.diagnostics.Length.Should().Be(expected);
                    dp.uri.Should().Be(doc.Uri);
                });
            idle.Idle += Raise.EventWith(null, EventArgs.Empty);

            expected = 0;
            doc.Update(new[] {new DocumentChange {
                InsertedText = "1",
                ReplacedSpan = new SourceSpan(1, 5, 1, 5)
            } });

            await doc.GetAnalysisAsync();
            idle.Idle += Raise.EventWith(null, EventArgs.Empty);
        }

        [TestMethod, Priority(0)]
        public async Task CloseDocument() {
            const string code = @"x = ";

            var analysis = await GetAnalysisAsync(code);
            var ds = Services.GetService<IDiagnosticsService>();

            var doc = analysis.Document;
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);

            var clientApp = Services.GetService<IClientApplication>();
            var idle = Services.GetService<IIdleTimeService>();
            var uri = doc.Uri;
            clientApp.When(x => x.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", Arg.Any<object>()))
                .Do(x => {
                    var dp = x.Args()[1] as PublishDiagnosticsParams;
                    dp.Should().NotBeNull();
                    dp.diagnostics.Length.Should().Be(0);
                    dp.uri.Should().Be(uri);
                });

            doc.Dispose();
            idle.Idle += Raise.EventWith(null, EventArgs.Empty);
            ds.Diagnostics.TryGetValue(doc.Uri, out _).Should().BeFalse();
        }
    }
}
