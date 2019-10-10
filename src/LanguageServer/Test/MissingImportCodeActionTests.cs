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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.UnitTests.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Microsoft.Python.LanguageServer.Diagnostics;
using Microsoft.Python.LanguageServer.CodeActions;
using System.Collections.Generic;

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
        public async Task TopModule() {
            const string markup = @"{|insertionSpan:|}{|diagnostic:ntpath|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            var codeAction = codeActions.Single();
            var newText = "import ntpath" + Environment.NewLine + Environment.NewLine;
            TestCodeAction(analysis.Document.Uri, codeAction, title: "import ntpath", insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task TopModuleFunctionDefinition() {
            const string markup = @"{|insertionSpan:|}def TestMethod():
    {|diagnostic:ntpath|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            codeActions.Should().HaveCount(2);

            var codeAction = codeActions.First();
            var newText = "import ntpath" + Environment.NewLine + Environment.NewLine;
            TestCodeAction(analysis.Document.Uri, codeAction, title: "import ntpath", insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task TopModuleFunctionDefinitionLocally() {
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
            const string markup = @"{|insertionSpan:|}{|diagnostic:util|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            var title = "from ctypes import util";
            var codeAction = codeActions.Single(c => c.title == title);
            var newText = "from ctypes import util" + Environment.NewLine + Environment.NewLine;

            TestCodeAction(analysis.Document.Uri, codeAction, title, insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task SubModuleUpdate() {
            const string markup = @"{|insertionSpan:from ctypes import util|}
{|diagnostic:test|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            var title = "from ctypes import test, util";
            var codeAction = codeActions.Single(c => c.title == title);
            var newText = "from ctypes import test, util";

            TestCodeAction(analysis.Document.Uri, codeAction, title, insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task SubModuleFunctionDefinitionUpdateLocally() {
            const string markup = @"def TestMethod():
    {|insertionSpan:from ctypes import util|}
    {|diagnostic:test|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            var title = string.Format(Resources.ImportLocally, "from ctypes import test, util");
            var codeAction = codeActions.Single(c => c.title == title);
            var newText = "from ctypes import test, util";

            TestCodeAction(analysis.Document.Uri, codeAction, title, insertionSpan, newText);
        }

        [TestMethod, Priority(0)]
        public async Task SubModuleFunctionDefinition() {
            const string markup = @"{|insertionSpan:|}def TestMethod():
    from ctypes import util
    {|diagnostic:test|}";

            var (analysis, codeActions, insertionSpan) = await GetAnalysisAndCodeActionsAndSpanAsync(markup, MissingImportCodeActionProvider.Instance.FixableDiagnostics);

            var title = "from ctypes import test";
            var codeAction = codeActions.Single(c => c.title == title);
            var newText = "from ctypes import test" + Environment.NewLine + Environment.NewLine;

            TestCodeAction(analysis.Document.Uri, codeAction, title, insertionSpan, newText);
        }

        private async Task<(IDocumentAnalysis analysis, CodeAction[] diagnostics, SourceSpan insertionSpan)> GetAnalysisAndCodeActionsAndSpanAsync(
            string markup, IEnumerable<string> codes) {
            MarkupUtiles.GetNamedSpans(markup, out var code, out var spans);

            var analysis = await GetAnalysisAsync(code);
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
