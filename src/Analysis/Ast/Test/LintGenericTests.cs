using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;


namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class LintGenericTests : AnalysisTestBase {

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

        [DataRow("x = Generic[T, str]")]
        [DataRow("x = Generic[T, T1, int]")]
        [DataRow("x = Generic[T, str, int, T1]")]
        [DataRow("x = Generic[str, int]")]
        [DataRow("x = Generic[str]")]
        [DataTestMethod, Priority(0)]
        public async Task GenericNotAllTypeParameters(string decl) {
            string code = GenericSetup + decl;

            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            var start = decl.IndexOf("Generic") + 1;
            // adding 1 because SourceSpan.End is exclusive and another 1 because SourceSpan is 1-indexed
            var end = decl.IndexOf("]", start) + 2;

            diagnostic.SourceSpan.Should().Be(8, start, 8, end);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingGenericArguments);
            diagnostic.Message.Should().Be(Resources.GenericNotAllTypeParameters);
        }

        [DataRow("x = Generic[_T, _X]")]
        [DataRow("x = Generic[_T, T, T1, _X]")]
        [DataRow("x = Generic[_T,_T, T]")]
        [DataRow("x = Generic[T,T]")]
        [DataTestMethod, Priority(0)]
        public async Task GenericDuplicateArguments(string decl) {
            string code = GenericSetup + decl;
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            var start = decl.IndexOf("Generic") + 1;
            // adding 1 because SourceSpan.End is exclusive and another 1 because SourceSpan is 1-indexed
            var end = decl.IndexOf("]", start) + 2;
            diagnostic.SourceSpan.Should().Be(8, start, 8, end);

            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingGenericArguments);
            diagnostic.Message.Should().Be(Resources.GenericNotAllUnique);
        }

        [DataRow("x = Generic[_X, T]")]
        [DataRow("x = Generic[T1, T]")]
        [DataRow("x = Generic[T]")]
        [DataRow("x = Generic[T,T1, _X]")]
        [DataTestMethod, Priority(0)]
        public async Task GenericArgumentsNoDiagnosticOnValid(string decl) {
            string code = GenericSetup + decl;
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task GenericNoArgumentsNoDiagnostic() {
            string code = GenericSetup + @"
x = Generic[]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task GenericArgumentSpaceNoDiagnostic() {
            string code = GenericSetup + @"
x = Generic[  ]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
