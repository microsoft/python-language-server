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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.UnitTests.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintUnusedImportTests : AnalysisTestBase {
        private readonly static HashSet<string> UnusedImportErrorCodes =
            new HashSet<string>() {
                ErrorCodes.UnusedImport
            };

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicUnusedImport() {
            await TestAsync(@"{|diagnostic:import os|}", PythonMemberType.Module, "os", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task BasicUnusedImportWithAsName() {
            await TestAsync(@"{|diagnostic:import os as o|}", PythonMemberType.Module, "o", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInImport() {
            await TestAsync(@"import {|diagnostic:os|}, math

e = math.e", PythonMemberType.Module, "os", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInImportWithAsName() {
            await TestAsync(@"import {|diagnostic:os as o|}, math

e = math.e", PythonMemberType.Module, "o", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInImportWithAsName2() {
            await TestAsync(@"import os as o, {|diagnostic:math as m|}

p = o.path", PythonMemberType.Module, "m", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task BasicUnusedFromImport() {
            await TestAsync(@"{|diagnostic:from os import path|}", PythonMemberType.Module, "path", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task BasicUnusedFromImportWithAsName() {
            await TestAsync(@"{|diagnostic:from os import path as p|}", PythonMemberType.Module, "p", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInFromImport() {
            await TestAsync(@"from os import {|diagnostic:path|}, pathconf
p = pathconf('', '')", PythonMemberType.Module, "path", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInFromImportWithAsName() {
            await TestAsync(@"from os import {|diagnostic:path as p1|}, pathconf
p = pathconf('', '')", PythonMemberType.Module, "p1", multiple: false);
        }

        private async Task TestAsync(string markup, PythonMemberType type, string name, bool multiple) {
            MarkupUtils.GetNamedSpans(markup, out var code, out var spans);

            var analysis = await GetAnalysisAsync(code);
            var actual = Lint(analysis, code);

            var expected = spans["diagnostic"];
            actual.Should().HaveCount(expected.Count);

            var message = GetMessage(type, name, multiple);
            var set = new HashSet<SourceSpan>(expected.Select(i => i.ToSourceSpan(analysis.Ast)));
            foreach (var diagnostic in actual) {
                diagnostic.ErrorCode.Should().Be(ErrorCodes.UnusedImport);
                diagnostic.Message.Should().Be(message);
                diagnostic.Tags.Should().HaveCount(1);
                diagnostic.Tags[0].Should().Be(DiagnosticsEntry.DiagnosticTags.Unnecessary);

                set.Remove(diagnostic.SourceSpan).Should().BeTrue();
            }

            set.Should().BeEmpty();
        }

        private string GetMessage(PythonMemberType type, string name, bool multiple) {
            if (!multiple) {
                return string.Format(CultureInfo.CurrentCulture, Resources._0_1_is_declared_but_it_is_never_used_within_the_current_file, type, name);
            }

            return string.Format(CultureInfo.CurrentCulture, Resources._0_1_are_declared_but_they_are_never_used_within_the_current_file, type, name);
        }

        private IReadOnlyList<DiagnosticsEntry> Lint(IDocumentAnalysis analysis, string code) {
            var a = Services.GetService<IPythonAnalyzer>();
            return a.LintModule(analysis.Document).Where(d => UnusedImportErrorCodes.Contains(d.ErrorCode)).ToList();
        }
    }
}
