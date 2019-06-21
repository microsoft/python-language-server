using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class LintTypeVarTests : AnalysisTestBase {

        internal static class Utils {
            public const string TYPEVAR_IMPORT = "from typing import TypeVar";
        }

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar(1, 2, 3)")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar(2.0, 3)")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
class C:
    int: t
    __init__(t):
        self.x = t

test = C(5)
T = TypeVar(C, 3)
")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar(1f)
")]
        [DataTestMethod, Priority(0)]
        public async Task TypeVarFirstArgumentNotString(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().BeEquivalentTo(Diagnostics.ErrorCodes.TypeVarArguments);
            diagnostic.Message.Should().BeEquivalentTo(Resources.TypeVarFirstArgumentNotString);

        }

        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T', int, str)")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
F = TypeVar('F',double, complex)")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T')
")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T', float, int)
")]
        [DataTestMethod, Priority(0)]
        public async Task TypeVarNoDiagnosticOnValidUse(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }


        [TestMethod, Priority(0)]
        public async Task TypeVarNoArguments() {
            const string code = @"
from typing import TypeVar

T = TypeVar()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().BeEquivalentTo(Diagnostics.ErrorCodes.TypeVarArguments);
            diagnostic.Message.Should().BeEquivalentTo(Resources.TypeVarMissingFirstArgument);
        }

        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T', 'test_constraint')
")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T', int)
")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T', complex)
")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T', str)
")]
        [DataRow(Utils.TYPEVAR_IMPORT + @"
T = TypeVar('T', 5)
")]
        [DataTestMethod, Priority(0)]
        public async Task TypeVarOneConstraint(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().BeEquivalentTo(Diagnostics.ErrorCodes.TypeVarArguments);
            diagnostic.Message.Should().BeEquivalentTo(Resources.TypeVarSingleConstraint);
        }
    }
}
