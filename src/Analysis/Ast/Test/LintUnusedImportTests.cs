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
            await TestAsync(@"{|Module.os.single:import os|}");
        }

        [TestMethod, Priority(0)]
        public async Task BasicUnusedImportWithAsName() {
            await TestAsync(@"{|Module.o.single:import os as o|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInImport() {
            await TestAsync(@"import {|Module.os.single:os|}, math

e = math.e");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInImportWithAsName() {
            await TestAsync(@"import {|Module.o.single:os as o|}, math

e = math.e");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInImportWithAsName2() {
            await TestAsync(@"import os as o, {|Module.m.single:math as m|}

p = o.path");
        }

        [TestMethod, Priority(0)]
        public async Task BasicUnusedFromImport() {
            await TestAsync(@"{|Module.path.single:from os import path|}");
        }

        [TestMethod, Priority(0)]
        public async Task BasicUnusedFromImportWithAsName() {
            await TestAsync(@"{|Module.p.single:from os import path as p|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInFromImport() {
            await TestAsync(@"from os import {|Module.path.single:path|}, pathconf
p = pathconf('', '')");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleModulesInFromImportWithAsName() {
            await TestAsync(@"from os import {|Module.p1.single:path as p1|}, pathconf
p = pathconf('', '')");
        }

        [TestMethod, Priority(0)]
        public async Task DottedNameImport() {
            await TestAsync(@"{|Module.os.single:import os.path|}");
        }

        [TestMethod, Priority(0)]
        public async Task DottedNameImport2() {
            await TestAsync(@"import {|Module.os.single:os.path|}, math
e = math.e");
        }

        [TestMethod, Priority(0)]
        public async Task DottedNameImport3() {
            await TestAsync(@"import {|Module.p.single:os.path as p|}, math
e = math.e");
        }

        [TestMethod, Priority(0)]
        public async Task DottedNameImport4() {
            await TestAsync(@"import os.path as p, {|Module.d.single:xml.dom as d|}
a = p.join('', '')");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNames() {
            await TestAsync(@"{|Module.os.multiple:import os.path, math|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNames2() {
            await TestAsync(@"{|Module.math.multiple:import math, os.path|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNames3() {
            await TestAsync(@"{|Module.p.multiple:import os.path as p, xml.dom as d|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNamesFromImport() {
            await TestAsync(@"{|Module.path.multiple:from os import path, pathconf|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNamesFromImport2() {
            await TestAsync(@"{|Function.pathconf.multiple:from os import pathconf, path|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleNamesFromImport3() {
            await TestAsync(@"{|Module.p.multiple:from os import path as p, pathconf as c|}");
        }

        [TestMethod, Priority(0)]
        public async Task ReferenceInAllVariable() {
            await TestAsync(@"from os import path as p, {|Function.c.single:pathconf as c|}
__all__ = [ 'p' ]
");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused() {
            await TestAsync(@"import os
path = os.path");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused2() {
            await TestAsync(@"import os as o, math as m
p = o.path
c = m.acos(10)");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused3() {
            await TestAsync(@"import os.path
s = os.sys");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused4() {
            await TestAsync(@"import os.path as s
p = s.join('', '')");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused5() {
            await TestAsync(@"import os.path
o = os");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused6() {
            await TestAsync(@"from os import path as p, pathconf as c
s = p.join('', '')
v = c('')");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused7() {
            await TestAsync(@"import os.path
__all__ = [ 'os' ]
");
        }

        [TestMethod, Priority(0)]
        public async Task NoUnused8() {
            await TestAsync(@"import os.path as p
__all__ = [ 'p' ]
");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports() {
            await TestAsync(@"{|Module.os.single:import os|}
from os import path
p = path.join('', '')");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports2() {
            await TestAsync(@"{|Module.os.single:import os|}
{|Module.math.single:import math|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports3() {
            await TestAsync(@"{|Module.os.single:import os|}
{|Module.os.single:import os.path|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports4() {
            await TestAsync(@"{|Module.o.single:import os as o|}
import {|Module.p.single:os.path as p|}, math
e = math.e");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports5() {
            await TestAsync(@"{|Module.os.single:import os|}
import {|Module.os.single:os.path|}, math
e = math.e");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports6() {
            await TestAsync(@"{|Module.os.multiple:import os, os.path|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports7() {
            await TestAsync(@"import {|Module.os.single:os|}, {|Module.os.single:os.path|}, math
e = math.e");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports8() {
            await TestAsync(@"{|Module.os.single:import os|}

def Method():
    {|Module.math.single:import math|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports9() {
            await TestAsync(@"{|Module.os.single:import os|}

def Method():
    {|Module.os.single:import os.path|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports10() {
            await TestAsync(@"{|Module.os.single:import os|}

def Method():
    import os.path
    p = os.path");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports11() {
            await TestAsync(@"import os
o = os.path

def Method():
    import os.path
    p = os.path");
        }


        [TestMethod, Priority(0)]
        public async Task MultipleImports12() {
            await TestAsync(@"import os
o = os.path

import os.path
p = os.path");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports13() {
            await TestAsync(@"{|Module.os.single:import os|}
import os.path

p = os.path");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports14() {
            await TestAsync(@"import os
p = os.path

{|Module.os.single:import os.path|}");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports15() {
            await TestAsync(@"import os
o = os.path

os = 1

import os
p = os.path");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports16() {
            // currently, our engine doesn't handle
            // varaible being reassigned to different type
            // in same scope. so it can't handle this case
            await TestAsync(@"import os

os = 1
p2 = os

import os
p = os.path");
        }

        [TestMethod, Priority(0)]
        public async Task MultipleImports17() {
            await TestAsync(@"os = 1
{|Module.os.single:import os.path|}

import os
p = os.path");
        }

        private async Task TestAsync(string markup) {
            MarkupUtils.GetNamedSpans(markup, out var code, out var spans);

            var analysis = await GetAnalysisAsync(code);
            var actual = Lint(analysis);

            var expectedDiagnostics = new List<(string type, string name, bool multiple, IndexSpan span)>();
            foreach (var kv in spans) {
                var (type, name, multiple) = GetAnnotatedInfo(kv.Key);
                foreach (var span in kv.Value) {
                    expectedDiagnostics.Add((type, name, multiple, span));
                }
            }

            actual.Should().HaveCount(expectedDiagnostics.Count);
            foreach (var item in actual.Zip(expectedDiagnostics, (d, e) => (d, e))) {
                item.d.ErrorCode.Should().Be(ErrorCodes.UnusedImport);
                item.d.SourceSpan.Should().Be(item.e.span.ToSourceSpan(analysis.Ast));
                item.d.Message.Should().Be(GetMessage(item.e.type, item.e.name, item.e.multiple));
                item.d.Tags.Should().HaveCount(1);
                item.d.Tags[0].Should().Be(DiagnosticsEntry.DiagnosticTags.Unnecessary);
            }
        }

        private (string type, string name, bool multiple) GetAnnotatedInfo(string key) {
            var data = key.Split(".");
            return (data[0], data[1], data[2] == "single" ? false : true);
        }

        private string GetMessage(string type, string name, bool multiple) {
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
