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
using Microsoft.Python.Analysis.Documents;
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
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class MissingImportCodeActionTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Missing() {
            MarkupUtils.GetSpan(@"[|missingModule|]", out var code, out var span);

            var analysis = await GetAnalysisAsync(code);
            var diagnostics = GetDiagnostics(analysis, span.ToSourceSpan(analysis.Ast), MissingImportCodeActionProvider.Instance.FixableDiagnostics);
            diagnostics.Should().NotBeEmpty();

            var codeActions = await new CodeActionSource(analysis.ExpressionEvaluator.Services).GetCodeActionsAsync(analysis, diagnostics, CancellationToken.None);
            codeActions.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task TopModule() {
            const string markup = @"{|insertionSpan:|}{|diagnostic:ntpath|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            var codeAction = codeActions.Single();
            var newText = "import ntpath" + Environment.NewLine + Environment.NewLine;
            TestCodeAction(analysis.Document.Uri, codeAction, title: "import ntpath", insertionSpan, newText);
        }

        [TestMethod, Priority(0), Ignore]
        public async Task TopModuleFromFunctionInsertTop() {
            const string markup = @"{|insertionSpan:|}def TestMethod():
    {|diagnostic:ntpath|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            codeActions.Should().HaveCount(2);

            var codeAction = codeActions.First();
            var newText = "import ntpath" + Environment.NewLine + Environment.NewLine;
            TestCodeAction(analysis.Document.Uri, codeAction, title: "import ntpath", insertionSpan, newText);
        }

        [TestMethod, Priority(0), Ignore]
        public async Task TopModuleLocally() {
            const string markup = @"def TestMethod():
{|insertionSpan:|}    {|diagnostic:ntpath|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            codeActions.Should().HaveCount(2);

            var codeAction = codeActions[1];
            var newText = "    import ntpath" + Environment.NewLine + Environment.NewLine;
            TestCodeAction(analysis.Document.Uri, codeAction, title: string.Format(Resources.ImportLocally, "import ntpath"), insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task SubModule() {
            await TestCodeActionAsync(
                @"{|insertionSpan:|}{|diagnostic:util|}",
                title: "from ctypes import util",
                newText: "from ctypes import util" + Environment.NewLine + Environment.NewLine);
        }

        [TestMethod, Priority(0)]
        public async Task SubModuleUpdate() {
            await TestCodeActionAsync(
                @"{|insertionSpan:from ctypes import util|}
{|diagnostic:test|}",
                title: "from ctypes import test, util",
                newText: "from ctypes import test, util");
        }

        [TestMethod, Priority(0), Ignore]
        public async Task SubModuleUpdateLocally() {
            await TestCodeActionAsync(
                @"def TestMethod():
    {|insertionSpan:from ctypes import util|}
    {|diagnostic:test|}",
                title: string.Format(Resources.ImportLocally, "from ctypes import test, util"),
                newText: "from ctypes import test, util");
        }

        [TestMethod, Priority(0)]
        public async Task SubModuleFromFunctionInsertTop() {
            await TestCodeActionAsync(
                @"{|insertionSpan:|}def TestMethod():
    from ctypes import util
    {|diagnostic:test|}",
                title: "from ctypes import test",
                newText: "from ctypes import test" + Environment.NewLine + Environment.NewLine);
        }

        [TestMethod, Priority(0)]
        public async Task AfterExistingImport() {
            await TestCodeActionAsync(
                @"from os import path
{|insertionSpan:|}
{|diagnostic:util|}",
                title: "from ctypes import util",
                newText: "from ctypes import util" + Environment.NewLine);
        }

        [TestMethod, Priority(0)]
        public async Task ReplaceExistingImport() {
            await TestCodeActionAsync(
                @"from os import path
{|insertionSpan:from ctypes import test|}
import socket

{|diagnostic:util|}",
                title: "from ctypes import test, util",
                newText: "from ctypes import test, util");
        }

        [TestMethod, Priority(0), Ignore]
        public async Task AfterExistingImportLocally() {
            await TestCodeActionAsync(
                @"def TestMethod():
    from os import path
{|insertionSpan:|}
    {|diagnostic:util|}",
                title: string.Format(Resources.ImportLocally, "from ctypes import util"),
                newText: "    from ctypes import util" + Environment.NewLine);
        }

        [TestMethod, Priority(0), Ignore]
        public async Task ReplaceExistingImportLocally() {
            await TestCodeActionAsync(
                @"def TestMethod():
    from os import path
    {|insertionSpan:from ctypes import test|}
    import socket

    {|diagnostic:util|}",
                title: string.Format(Resources.ImportLocally, "from ctypes import test, util"),
                newText: "from ctypes import test, util");
        }

        [TestMethod, Priority(0), Ignore]
        public async Task CodeActionOrdering() {
            MarkupUtils.GetSpan(@"def TestMethod():
    [|test|]", out var code, out var span);

            var analysis = await GetAnalysisAsync(code);
            var diagnostics = GetDiagnostics(analysis, span.ToSourceSpan(analysis.Ast), MissingImportCodeActionProvider.Instance.FixableDiagnostics);
            diagnostics.Should().NotBeEmpty();

            var codeActions = await new CodeActionSource(analysis.ExpressionEvaluator.Services).GetCodeActionsAsync(analysis, diagnostics, CancellationToken.None);

            var list = codeActions.Select(c => c.title).ToList();
            var zipList = Enumerable.Range(0, list.Count).Zip(list);

            var locallyImportedPrefix = Resources.ImportLocally.Substring(0, Resources.ImportLocally.IndexOf("'"));
            var maxIndexOfTopAddImports = zipList.Where(t => !t.Second.StartsWith(locallyImportedPrefix)).Max(t => t.First);
            var minIndexOfLocalAddImports = zipList.Where(t => t.Second.StartsWith(locallyImportedPrefix)).Min(t => t.First);

            maxIndexOfTopAddImports.Should().BeLessThan(minIndexOfLocalAddImports);
        }

        [TestMethod, Priority(0)]
        public async Task PreserveComment() {
            await TestCodeActionAsync(
                @"{|insertionSpan:from os import pathconf|} # test

{|diagnostic:path|}",
                title: "from os import path, pathconf",
                newText: "from os import path, pathconf");
        }

        [TestMethod, Priority(0)]
        public async Task MemberSymbol() {
            await TestCodeActionAsync(
                @"from os import path
{|insertionSpan:|}
{|diagnostic:socket|}",
                title: "from socket import socket",
                newText: "from socket import socket" + Environment.NewLine);
        }

        [TestMethod, Priority(0)]
        public async Task NoMemberSymbol() {
            var markup = @"{|insertionSpan:|}{|diagnostic:socket|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            codeActions.Select(c => c.title).Should().NotContain("from socket import socket");

            var title = "import socket";
            var codeAction = codeActions.Single(c => c.title == title);
            var newText = "import socket" + Environment.NewLine + Environment.NewLine;
            TestCodeAction(analysis.Document.Uri, codeAction, title, insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task SymbolOrdering() {
            var markup = @"from os import path
{|insertionSpan:|}
{|diagnostic:socket|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            var list = codeActions.Select(c => c.title).ToList();
            var zipList = Enumerable.Range(0, list.Count).Zip(list);

            var maxIndexOfPublicSymbol = zipList.Where(t => !t.Second.StartsWith("from _")).Max(t => t.First);
            var minIndexOfPrivateSymbol = zipList.Where(t => t.Second.StartsWith("from _")).Min(t => t.First);

            maxIndexOfPublicSymbol.Should().BeLessThan(minIndexOfPrivateSymbol);
        }

        [TestMethod, Priority(0)]
        public async Task SymbolOrdering2() {
            var markup = @"{|insertionSpan:|}{|diagnostic:join|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics, enableIndexManager: true);

            var list = codeActions.Select(c => c.title).ToList();
            var zipList = Enumerable.Range(0, list.Count).Zip(list);

            var sourceDeclIndex = zipList.First(t => t.Second == "from macpath import join").First;
            var importedMemberIndex = zipList.First(t => t.Second == "from os.path import join").First;
            var restIndex = zipList.First(t => t.Second == "from ntpath import join").First;

            sourceDeclIndex.Should().BeLessThan(importedMemberIndex);
            importedMemberIndex.Should().BeLessThan(restIndex);
        }

        [TestMethod, Priority(0)]
        public async Task ModuleNotReachableFromUserDocument() {
            await TestCodeActionAsync(
                @"{|insertionSpan:|}{|diagnostic:path|}",
                title: "from os import path",
                newText: "from os import path" + Environment.NewLine + Environment.NewLine,
                enableIndexManager: true);
        }

        [TestMethod, Priority(0)]
        public async Task SuggestAbbreviationForKnownModule() {
            MarkupUtils.GetNamedSpans(@"{|insertionSpan:|}{|diagnostic:pandas|}", out var code, out var spans);

            // get main analysis and add mock pandas module
            var analysis = await GetAnalysisAsync(code);
            await GetAnalysisAsync("", analysis.ExpressionEvaluator.Services, modulePath: TestData.GetTestSpecificPath("pandas.py"));

            // calculate actions
            var diagnostics = GetDiagnostics(analysis, spans["diagnostic"].First().ToSourceSpan(analysis.Ast), MissingImportCodeActionProvider.Instance.FixableDiagnostics);
            var codeActions = await new CodeActionSource(analysis.ExpressionEvaluator.Services).GetCodeActionsAsync(analysis, diagnostics, CancellationToken.None);

            var docTable = analysis.ExpressionEvaluator.Services.GetService<IRunningDocumentTable>();

            // verify results
            var codeAction = codeActions.Single(c => c.title == "import pandas as pd");
            codeAction.edit.changes.Should().HaveCount(1);

            var edits = codeAction.edit.changes[analysis.Document.Uri];
            edits.Should().HaveCount(2);

            var invocationEdit = edits.Single(e => e.newText == "pd");
            invocationEdit.range.Should().Be(spans["diagnostic"].First().ToSourceSpan(analysis.Ast));

            var insertEdit = edits.Single(e => e.newText == "import pandas as pd" + Environment.NewLine + Environment.NewLine);
            insertEdit.range.Should().Be(spans["insertionSpan"].First().ToSourceSpan(analysis.Ast));
        }

        [TestMethod, Priority(0)]
        public async Task ContextBasedSuggestion() {
            var markup =
                @"from os import path
{|insertionSpan:|}
{|diagnostic:socket|}()";

            var (analysis, codeActions, insertionSpan) =
                await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            codeActions.Should().NotContain(c => c.title == "import socket");

            var title = "from socket import socket";
            var newText = "from socket import socket" + Environment.NewLine;

            var codeAction = codeActions.Single(c => c.title == title);
            TestCodeAction(analysis.Document.Uri, codeAction, title, insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task ValidToBeUsedInImport() {
            await TestCodeActionAsync(
                @"from os import path
{|insertionSpan:|}
{|diagnostic:join|}",
                title: "from os.path import join",
                newText: "from os.path import join" + Environment.NewLine);
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
            var codeActions = await new CodeActionSource(analysis.ExpressionEvaluator.Services).GetCodeActionsAsync(analysis, diagnostics, CancellationToken.None);
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
    }
}
