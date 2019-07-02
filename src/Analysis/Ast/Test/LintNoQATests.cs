using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;


namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class LintNoQATests : AnalysisTestBase {

        public const string GenericSetup = @"
from typing import Generic, TypeVar
T = TypeVar('T', int, str)
T1 = TypeVar('T1', int, str)

_X = TypeVar('_X', str, int)
_T = _X
";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow("x = Generic[T, str] #noqa")]
        [DataRow("x = Generic[T, T] #noqa ")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreGenerics(string decl) {
            string code = GenericSetup + decl;

            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }

        [DataRow("x = 'str' + 5 #noqa")]
        [DataRow("x = float(1) * 'str'  #noqa")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreBadBinaryOp(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }

        [DataRow("x = 'str' + 5 #    noqa")]
        [DataRow("x = float(1) * 'str'  #           noqa")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreNoQAWithSpace(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }




        [DataRow("x = y #noqa")]
        [DataRow("x = z + 2  #noqa")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreUndefinedVar(string code) {
            var d = await LintAsync(code);
            d.Should().HaveCount(0);
        }

        [DataRow(@"
def hello(a, b, c):
    pass

h = hello(1, 2)
")]
        [DataRow(@"
class Hello:
    def tmp(self, a):
        pass

h = Hello()
t = h()
")]
        [DataTestMethod, Priority(0)]
        public async Task IgnoreMissingParameter(string code) {
            var d = await LintAsync(code);
            d.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task IgnoreMissingImport() {
            string code = @"
from fake_module import User         #noqa
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }

        private async Task<IReadOnlyList<DiagnosticsEntry>> LintAsync(string code, InterpreterConfiguration configuration = null) {
            var analysis = await GetAnalysisAsync(code, configuration ?? PythonVersions.LatestAvailable3X);
            var a = Services.GetService<IPythonAnalyzer>();
            return a.LintModule(analysis.Document);
        }
    }
}
