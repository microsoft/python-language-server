using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class LintTypeVarTests : AnalysisTestBase {
        public const string TypeVarImport = "from typing import TypeVar";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow(TypeVarImport + @"
T = TypeVar(1, 2, 3)")]
        [DataRow(TypeVarImport + @"
T = TypeVar(2.0, 3)")]
        [DataRow(TypeVarImport + @"
class C:
    int: t
    __init__(t):
        self.x = t

test = C(5)
T = TypeVar(C, 3)
")]
        [DataRow(TypeVarImport + @"
T = TypeVar(1f)
")]
        [DataTestMethod, Priority(0)]
        public async Task TypeVarFirstArgumentNotString(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingTypeVarArguments);
            diagnostic.Message.Should().Be(Resources.TypeVarFirstArgumentNotString);

        }

        [DataRow(TypeVarImport + @"
T = TypeVar('T', int, str)")]
        [DataRow(TypeVarImport + @"
F = TypeVar('F',double, complex)")]
        [DataRow(TypeVarImport + @"
T = TypeVar('T')
")]
        [DataRow(TypeVarImport + @"
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
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingTypeVarArguments);
            diagnostic.Message.Should().Be(Resources.TypeVarMissingFirstArgument);
        }

        [DataRow(TypeVarImport + @"
T = TypeVar('T', 'test_constraint')
")]
        [DataRow(TypeVarImport + @"
T = TypeVar('T', int)
")]
        [DataRow(TypeVarImport + @"
T = TypeVar('T', complex)
")]
        [DataRow(TypeVarImport + @"
T = TypeVar('T', str)
")]
        [DataRow(TypeVarImport + @"
T = TypeVar('T', 5)
")]
        [DataTestMethod, Priority(0)]
        public async Task TypeVarOneConstraint(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingTypeVarArguments);
            diagnostic.Message.Should().Be(Resources.TypeVarSingleConstraint);
        }
    }
}
