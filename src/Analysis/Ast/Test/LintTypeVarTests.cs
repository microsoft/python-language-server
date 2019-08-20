using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class LintTypeVarTests : AnalysisTestBase {
        public const string TypeVarImport = @"
from typing import TypeVar
";

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

        [TestMethod, Priority(0)]
        public async Task TypeVarFirstArgumentFunction() {
            const string code = @"
from typing import TypeVar

def temp():
    return 'str'

T = TypeVar(temp(), int, str)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
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
            analysis.Diagnostics.Should().BeEmpty();
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
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.ParameterMissing);
            diagnostic.Message.Should().Be(Resources.Analysis_ParameterMissing.FormatInvariant("name"));
            diagnostic.SourceSpan.Should().Be(4, 5, 4, 12);
        }

        [DataRow("T = TypeVar('T', 'test_constraint')")]
        [DataRow("T = TypeVar('T', int)")]
        [DataRow("T = TypeVar('T', complex)")]
        [DataRow("T = TypeVar('T', str)")]
        [DataRow("T = TypeVar('T', 5)")]
        [DataTestMethod, Priority(0)]
        public async Task TypeVarOneConstraint(string decl) {
            string code = TypeVarImport + decl;
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingTypeVarArguments);
            diagnostic.Message.Should().Be(Resources.TypeVarSingleConstraint);
            diagnostic.SourceSpan.Should().Be(3, 5, 3, decl.IndexOf(")") + 2);
        }
    }
}
