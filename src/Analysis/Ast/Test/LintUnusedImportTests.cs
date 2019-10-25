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

        [TestMethod, Priority(0)]
        public async Task DottedNameImport() {
            await TestAsync(@"{|diagnostic:import os.path|}", PythonMemberType.Module, "os", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task DottedNameImport2() {
            await TestAsync(@"import {|diagnostic:os.path|}, math
e = math.e", PythonMemberType.Module, "os", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task DottedNameImport3() {
            await TestAsync(@"import {|diagnostic:os.path as p|}, math
e = math.e", PythonMemberType.Module, "p", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task DottedNameImport4() {
            await TestAsync(@"import os.path as p, {|diagnostic:xml.dom as d|}
a = p.join('', '')", PythonMemberType.Module, "d", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNames() {
            await TestAsync(@"{|diagnostic:import os.path, math|}", PythonMemberType.Module, "os", multiple: true);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNames2() {
            await TestAsync(@"{|diagnostic:import math, os.path|}", PythonMemberType.Module, "math", multiple: true);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNames3() {
            await TestAsync(@"{|diagnostic:import os.path as p, xml.dom as d|}", PythonMemberType.Module, "p", multiple: true);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNamesFromImport() {
            await TestAsync(@"{|diagnostic:from os import path, pathconf|}", PythonMemberType.Module, "path", multiple: true);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNamesFromImport2() {
            await TestAsync(@"{|diagnostic:from os import pathconf, path|}", PythonMemberType.Function, "pathconf", multiple: true);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNamesFromImport3() {
            await TestAsync(@"{|diagnostic:from os import path as p, pathconf as c|}", PythonMemberType.Module, "p", multiple: true);
        }

        [TestMethod, Priority(0)]
        public async Task ReferenceInAllVariable() {
            await TestAsync(@"from os import path as p, {|diagnostic:pathconf as c|}
__all__ = [ 'p' ]
", PythonMemberType.Function, "c", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused() {
            await TestNoUnusedAsync(@"import os
path = os.path");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused2() {
            await TestNoUnusedAsync(@"import os as o, math as m
p = o.path
c = m.acos(10)");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused3() {
            await TestNoUnusedAsync(@"import os.path
s = os.sys");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused4() {
            await TestNoUnusedAsync(@"import os.path as s
p = s.join('', '')");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused5() {
            await TestNoUnusedAsync(@"import os.path
o = os");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused6() {
            await TestNoUnusedAsync(@"from os import path as p, pathconf as c
s = p.join('', '')
v = c('')");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused7() {
            await TestNoUnusedAsync(@"import os.path
__all__ = [ 'os' ]
");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused8() {
            await TestNoUnusedAsync(@"import os.path as p
__all__ = [ 'p' ]
");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports() {
            await TestAsync(@"{|diagnostic:import os|}
from os import path
p = path.join('', '')", PythonMemberType.Module, "os", multiple: false);
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports2() {
            await TestAsync(@"{|diagnostic:import os|}
{|diagnostic:import math|}", 
                (PythonMemberType.Module, "os", multiple: false), 
                (PythonMemberType.Module, "math", multiple: false));
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports3() {
            await TestAsync(@"{|diagnostic:import os|}
{|diagnostic:import os.path|}",
                (PythonMemberType.Module, "os", multiple: false),
                (PythonMemberType.Module, "os", multiple: false));
        }

        private Task TestNoUnusedAsync(string markup) {
            return TestAsync(markup);
        }

        private Task TestAsync(string markup, PythonMemberType type, string name, bool multiple) {
            // another way of doing this is putting (type, name, multiple) as a annotation in markup itself.
            return TestAsync(markup, (type, name, multiple));
        }

        private async Task TestAsync(string markup, params (PythonMemberType type, string name, bool multiple)[] expected) {
            MarkupUtils.GetNamedSpans(markup, out var code, out var spans);

            var analysis = await GetAnalysisAsync(code);
            var actual = Lint(analysis);

            spans.TryGetValue("diagnostic", out var expectedDiagnostics);

            expectedDiagnostics = expectedDiagnostics ?? new List<IndexSpan>();
            actual.Should().HaveCount(expectedDiagnostics.Count);
            actual.Should().HaveCount(expected.Length);

            var set = new HashSet<SourceSpan>(expectedDiagnostics.Select(i => i.ToSourceSpan(analysis.Ast)));
            foreach (var item in actual.Zip(expected, (d, e) => (d: d, e: e))) {
                item.d.ErrorCode.Should().Be(ErrorCodes.UnusedImport);
                item.d.Message.Should().Be(GetMessage(item.e.type, item.e.name, item.e.multiple));
                item.d.Tags.Should().HaveCount(1);
                item.d.Tags[0].Should().Be(DiagnosticsEntry.DiagnosticTags.Unnecessary);

                set.Remove(item.d.SourceSpan).Should().BeTrue();
            }

            set.Should().BeEmpty();
        }

        private string GetMessage(PythonMemberType type, string name, bool multiple) {
            if (!multiple) {
                return string.Format(CultureInfo.CurrentCulture, Resources._0_1_is_declared_but_it_is_never_used_within_the_current_file, type, name);
            }

            return string.Format(CultureInfo.CurrentCulture, Resources._0_1_are_declared_but_they_are_never_used_within_the_current_file, type, name);
        }

        private IReadOnlyList<DiagnosticsEntry> Lint(IDocumentAnalysis analysis) {
            var a = Services.GetService<IPythonAnalyzer>();
            return a.LintModule(analysis.Document).Where(d => UnusedImportErrorCodes.Contains(d.ErrorCode)).ToList();
        }
    }
}
