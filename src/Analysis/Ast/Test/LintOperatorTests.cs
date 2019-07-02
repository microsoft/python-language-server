using FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintOperatorTests : AnalysisTestBase {

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task IncompatibleTypesBinaryOpBasic() {
            var code = $@"
a = 5 + 'str'
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 14);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.UnsupportedOperandType);
            diagnostic.Message.Should().Be(Resources.UnsupporedOperandType.FormatInvariant("+", "int", "str"));
        }

        [DataRow("str", "int", "+")]
        [DataRow("str", "int", "-")]
        [DataRow("str", "int", "/")]
        [DataRow("str", "float", "+")]
        [DataRow("str", "float", "-")]
        [DataRow("str", "float", "*")]
        [DataTestMethod, Priority(0)]
        public async Task IncompatibleTypesBinaryOp(string leftType, string rightType, string op) {
 var code = $@"
x = 1
y = 2

z = {leftType}(x) {op} {rightType}(y)
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);


            string line = $"z = {leftType}(x) {op} {rightType}(y)";
            // source span is 1 indexed
            diagnostic.SourceSpan.Should().Be(5, line.IndexOf(leftType) + 1, 5, line.IndexOf("(y)") + 4);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.UnsupportedOperandType);
            diagnostic.Message.Should().Be(Resources.UnsupporedOperandType.FormatInvariant(op, leftType, rightType));
        }

        [DataRow("str", "str", "+")]
        [DataRow("int", "int", "-")]
        [DataRow("bool", "int", "/")]
        [DataRow("float", "int", "+")]
        [DataRow("complex", "float", "-")]
        [DataRow("str", "int", "*")]
        [DataTestMethod, Priority(0)]
        public async Task CompatibleTypesBinaryOp(string leftType, string rightType, string op) {
            var code = $@"
x = 1
y = 2

z = {leftType}(x) {op} {rightType}(y)
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }

}
