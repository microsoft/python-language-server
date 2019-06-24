using FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;
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
        public async Task IncompatibleTypesBasic() {
            var code = $@"
a = 5 + 'str'
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.UnsupportedOperandType);
            diagnostic.Message.Should().Be(Resources.UnsupporedOperandType);
        }

        [DataRow("str", "int", "+")]
        [DataRow("str", "int", "-")]
        [DataRow("str", "int", "/")]
        [DataRow("str", "float", "+")]
        [DataRow("str", "float", "-")]
        [DataRow("str", "float", "*")]
        [DataTestMethod, Priority(0)]
        public async Task IncompatibleTypes(string leftType, string rightType, string op) {
 var code = $@"
x = 1
y = 2

z = {leftType}(x) {op} {rightType}(y)
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.UnsupportedOperandType);
            diagnostic.Message.Should().Be(Resources.UnsupporedOperandType);

        }
    }

}
