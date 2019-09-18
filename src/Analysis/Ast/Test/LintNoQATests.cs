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

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class LintNoQATests : LinterTestBase {

        public const string GenericSetup = @"
from typing import Generic, TypeVar
T = TypeVar('T', int, str)
T1 = TypeVar('T1', int, str)

_X = TypeVar('_X', str, int)
_T = _X
";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() { 
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow("x = Generic[T, str] #noqa")]
        [DataRow("x = Generic[T, T] #noqa ")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreGenerics(string decl) {
            var code = GenericSetup + decl;

            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [DataRow("x = 'str' + 5 #noqa")]
        [DataRow("x = float(1) * 'str'  #noqa")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreBadBinaryOp(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [DataRow("x = 'str' + 5 #    noqa")]
        [DataRow("x = float(1) * 'str'  #           noqa")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreNoQAWithSpace(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [DataRow("x = 'str' + 5 #                   noqa")]
        [DataRow("x = float(1) * 'str'  #                       noqa")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreNoQAWithTab(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [DataRow("x = 'str' + 5 #NOQA")]
        [DataRow("x = float(1) * 'str'  # NOQA")]
        [DataRow("x = float(1) * 'str'  #   NOQA")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreNoQAUppercase(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task VarNamedNoQAStillGivesDiagnostic() {
            const string code = GenericSetup + "NOQA = Generic[T, T]";

            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.Message.Should().Be(Resources.GenericNotAllUnique);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.TypingGenericArguments);
            diagnostic.SourceSpan.Should().Be(8, 8, 8, 21);
            diagnostic.Severity.Should().Be(Severity.Warning);
        }

        [DataRow("x = y #noqa")]
        [DataRow("x = z + 2  #noqa")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreUndefinedVar(string code) {
            var d = await LintAsync(code);
            d.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task IgnoreMissingImport() {
            const string code = @"
from fake_module import User         #noqa
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
