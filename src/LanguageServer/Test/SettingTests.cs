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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.CodeActions;
using Microsoft.Python.LanguageServer.Diagnostics;
using Microsoft.Python.LanguageServer.Indexing;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.UnitTests.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class SettingTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }
        public CancellationToken CancellationToken => TestContext.CancellationTokenSource.Token;

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void Basic() {
            var refactoring = JToken.Parse("{ a: 1}");
            var codeActionSetting = CodeActionSettings.Parse(refactoring, quickFix: null, CancellationToken);

            codeActionSetting.GetRefactoringOption("a", -1).Should().Be(1);
        }

        private async Task TestCodeActionAsync(string markup, string title, string newText, bool enableIndexManager = false) {
            var (analysis, codeActions, insertionSpan) =
                await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics, enableIndexManager);

            var codeAction = codeActions.Single(c => c.title == title);
            TestCodeAction(analysis.Document.Uri, codeAction, title, insertionSpan, newText);
        }

        private async Task<(IDocumentAnalysis analysis, CodeAction[] diagnostics, SourceSpan insertionSpan)> GetAnalysisAndCodeActionsAndSpanAsync(
            string markup, IEnumerable<string> codes, bool enableIndexManager = false) {
            MarkupUtils.GetNamedSpans(markup, out var code, out var spans);

            var analysis = await GetAnalysisAsync(code);

            if (enableIndexManager) {
                var serviceManager = (IServiceManager)analysis.ExpressionEvaluator.Services;
                var indexManager = new IndexManager(
                    serviceManager.GetService<IFileSystem>(),
                    analysis.Document.Interpreter.LanguageVersion,
                    rootPath: null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    serviceManager.GetService<IIdleTimeService>());

                // make sure index is done
                await indexManager.IndexWorkspace(analysis.Document.Interpreter.ModuleResolution.CurrentPathResolver);

                serviceManager.AddService(indexManager);
            }

            var insertionSpan = spans["insertionSpan"].First().ToSourceSpan(analysis.Ast);

            var diagnostics = GetDiagnostics(analysis, spans["diagnostic"].First().ToSourceSpan(analysis.Ast), codes);
            var codeActions = await new QuickFixCodeActionSource(analysis.ExpressionEvaluator.Services).GetCodeActionsAsync(analysis, CodeActionSettings.Default, diagnostics, CancellationToken);
            return (analysis, codeActions.ToArray(), insertionSpan);
        }

        private static void TestCodeAction(Uri uri, CodeAction codeAction, string title, Core.Text.Range insertedSpan, string newText) {
            codeAction.title.Should().Be(title);
            codeAction.edit.changes.Should().HaveCount(1);

            var edit = codeAction.edit.changes[uri];
            edit.Single().range.Should().Be(insertedSpan);
            edit.Single().newText.Should().Be(newText);
        }

        private static Diagnostic[] GetDiagnostics(IDocumentAnalysis analysis, SourceSpan span, IEnumerable<string> codes) {
            var analyzer = analysis.ExpressionEvaluator.Services.GetService<IPythonAnalyzer>();
            return analyzer.LintModule(analysis.Document)
                           .Where(d => d.SourceSpan == span && codes.Any(e => string.Equals(e, d.ErrorCode)))
                           .Select(d => d.ToDiagnostic())
                           .ToArray();
        }

        private async Task TestCodeActionAsync(string markup, string title, string newText, string abbreviation, params string[] relativePaths) {
            MarkupUtils.GetNamedSpans(markup, out var code, out var spans);

            // get main analysis and add mock modules
            var analysis = await GetAnalysisAsync(code);

            foreach (var relativePath in relativePaths) {
                await GetAnalysisAsync("", analysis.ExpressionEvaluator.Services, modulePath: TestData.GetTestSpecificPath(relativePath));
            }

            // calculate actions
            var diagnosticSpan = spans["diagnostic"].First().ToSourceSpan(analysis.Ast);
            var diagnostics = GetDiagnostics(analysis, diagnosticSpan, MissingImportCodeActionProvider.Instance.FixableDiagnostics);
            var codeActions = await new QuickFixCodeActionSource(analysis.ExpressionEvaluator.Services).GetCodeActionsAsync(analysis, CodeActionSettings.Default, diagnostics, CancellationToken);

            // verify results
            var codeAction = codeActions.Single(c => c.title == title);
            codeAction.edit.changes.Should().HaveCount(1);

            var edits = codeAction.edit.changes[analysis.Document.Uri];
            edits.Should().HaveCount(2);

            var invocationEdit = edits.Single(e => e.newText == abbreviation);
            invocationEdit.range.Should().Be(diagnosticSpan);

            var insertEdit = edits.Single(e => e.newText == newText);
            insertEdit.range.Should().Be(spans["insertionSpan"].First().ToSourceSpan(analysis.Ast));
        }
    }
}
