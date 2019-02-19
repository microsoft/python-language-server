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
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

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
            var ds = GetDiagnosticsService();

            var doc = analysis.Document;
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);

            doc.Update(new[] {new DocumentChange {
                InsertedText = "1",
                    ReplacedSpan = new SourceSpan(1, 5, 1, 5)
            } });

            await doc.GetAstAsync();
            await doc.GetAnalysisAsync(10000);
            ds.Diagnostics[doc.Uri].Count.Should().Be(0);

            doc.Update(new[] {new DocumentChange {
                InsertedText = string.Empty,
                ReplacedSpan = new SourceSpan(1, 5, 1, 6)
            } });

            await doc.GetAstAsync();
            await doc.GetAnalysisAsync(0);
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);
        }

        [TestMethod, Priority(0)]
        public async Task TwoDocuments() {
            const string code1 = @"x = ";
            const string code2 = @"y = ";

            var analysis1 = await GetAnalysisAsync(code1);
            var analysis2 = await GetNextAnalysisAsync(code2);
            var ds = GetDiagnosticsService();

            var doc1 = analysis1.Document;
            var doc2 = analysis2.Document;

            ds.Diagnostics[doc1.Uri].Count.Should().Be(1);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);

            doc2.Update(new[] {new DocumentChange {
                InsertedText = "1",
                ReplacedSpan = new SourceSpan(1, 5, 1, 5)
            } });

            await doc2.GetAstAsync();
            await doc2.GetAnalysisAsync(0);
            ds.Diagnostics[doc1.Uri].Count.Should().Be(1);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(0);

            doc2.Update(new[] {new DocumentChange {
                InsertedText = string.Empty,
                ReplacedSpan = new SourceSpan(1, 5, 1, 6)
            } });

            await doc2.GetAstAsync();
            await doc2.GetAnalysisAsync(0);
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
            var reported = new List<PublishDiagnosticsParams>();
            clientApp.When(x => x.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", Arg.Any<object>()))
                .Do(x => reported.Add(x.Args()[1] as PublishDiagnosticsParams));

            PublishDiagnostics();
            reported.Count.Should().Be(1);
            reported[0].diagnostics.Length.Should().Be(1);
            reported[0].uri.Should().Be(doc.Uri);

            reported.Clear();
            doc.Update(new[] {new DocumentChange {
                InsertedText = "1",
                ReplacedSpan = new SourceSpan(1, 5, 1, 5)
            } });

            await doc.GetAstAsync();
            await doc.GetAnalysisAsync(10000);

            PublishDiagnostics();
            reported.Count.Should().Be(1);
            reported[0].diagnostics.Length.Should().Be(0);
        }

        [TestMethod, Priority(0)]
        public async Task CloseDocument() {
            const string code = @"x = ";

            var analysis = await GetAnalysisAsync(code);
            var ds = GetDiagnosticsService();

            var doc = analysis.Document;
            ds.Diagnostics[doc.Uri].Count.Should().Be(1);

            var clientApp = Services.GetService<IClientApplication>();
            var reported = new List<PublishDiagnosticsParams>();
            clientApp.When(x => x.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", Arg.Any<object>()))
                .Do(x => reported.Add(x.Args()[1] as PublishDiagnosticsParams));

            doc.Dispose();
            ds.Diagnostics.TryGetValue(doc.Uri, out _).Should().BeFalse();
            reported.Count.Should().Be(1);
            reported[0].uri.Should().Be(doc.Uri);
            reported[0].diagnostics.Length.Should().Be(0);
        }

        [TestMethod, Priority(0)]
        public async Task SeverityMapping() {
            const string code = @"import nonexistent";

            var analysis = await GetAnalysisAsync(code);
            var ds = GetDiagnosticsService();
            var doc = analysis.Document;

            var diags = ds.Diagnostics[doc.Uri];
            diags.Count.Should().Be(1);
            diags[0].Severity.Should().Be(Severity.Warning);

            var clientApp = Services.GetService<IClientApplication>();
            var reported = new List<PublishDiagnosticsParams>();
            clientApp.When(x => x.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", Arg.Any<object>()))
                .Do(x => reported.Add(x.Args()[1] as PublishDiagnosticsParams));

            ds.DiagnosticsSeverityMap = new DiagnosticsSeverityMap(new[] { ErrorCodes.UnresolvedImport }, null, null, null);
            reported.Count.Should().Be(1);
            reported[0].uri.Should().Be(doc.Uri);
            reported[0].diagnostics.Length.Should().Be(1);
            reported[0].diagnostics[0].severity.Should().Be(DiagnosticSeverity.Error);

            reported.Clear();
            ds.DiagnosticsSeverityMap = new DiagnosticsSeverityMap(null, null, new[] { ErrorCodes.UnresolvedImport }, null);
            reported.Count.Should().Be(1);
            reported[0].diagnostics[0].severity.Should().Be(DiagnosticSeverity.Information);

            reported.Clear();
            ds.DiagnosticsSeverityMap = new DiagnosticsSeverityMap(null, null, null, new[] { ErrorCodes.UnresolvedImport });
            reported.Count.Should().Be(1);
            reported[0].diagnostics.Length.Should().Be(0);
        }

        [TestMethod, Priority(0)]
        public async Task OnlyPublishChangedFile() {
            const string code1 = @"x = ";
            const string code2 = @"y = ";

            var analysis1 = await GetAnalysisAsync(code1);
            var analysis2 = await GetNextAnalysisAsync(code2);
            var ds = GetDiagnosticsService();

            var doc1 = analysis1.Document;
            var doc2 = analysis2.Document;

            ds.Diagnostics[doc1.Uri].Count.Should().Be(1);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);

            // Clear diagnostics 'changed' state by forcing publish.
            PublishDiagnostics();

            var reported = new List<PublishDiagnosticsParams>();
            var clientApp = Services.GetService<IClientApplication>();
            clientApp.When(x => x.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", Arg.Any<object>()))
                .Do(x => {
                    var dp = x.Args()[1] as PublishDiagnosticsParams;
                    reported.Add(dp);
                });


            doc1.Update(new[] {new DocumentChange {
                InsertedText = "1",
                ReplacedSpan = new SourceSpan(1, 5, 1, 5)
            } });

            await doc1.GetAstAsync();
            await doc1.GetAnalysisAsync(10000);

            ds.Diagnostics[doc1.Uri].Count.Should().Be(0);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);

            PublishDiagnostics();
            reported.Count.Should().Be(1);
            reported[0].uri.Should().Be(doc1.Uri);
            reported[0].diagnostics.Length.Should().Be(0);

            doc1.Update(new[] {new DocumentChange {
                InsertedText = string.Empty,
                ReplacedSpan = new SourceSpan(1, 5, 1, 6)
            } });

            await doc1.GetAstAsync();
            await doc1.GetAnalysisAsync(10000);
            ds.Diagnostics[doc1.Uri].Count.Should().Be(1);
            ds.Diagnostics[doc2.Uri].Count.Should().Be(1);

            reported.Clear();
            PublishDiagnostics();

            reported.Count.Should().Be(1);
            reported[0].uri.Should().Be(doc1.Uri);
            reported[0].diagnostics.Length.Should().Be(1);
        }

        private IDiagnosticsService GetDiagnosticsService() {
            var ds = Services.GetService<IDiagnosticsService>();
            ds.PublishingDelay = 0;
            return ds;
        }
        private void PublishDiagnostics() {
            var ds = Services.GetService<IDiagnosticsService>();
            ds.PublishingDelay = 0;
            var idle = Services.GetService<IIdleTimeService>();
            idle.Idle += Raise.EventWith(null, EventArgs.Empty);
        }
    }
}
