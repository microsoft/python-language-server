using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

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

        [DataRow("x = hello < test")]
        [DataRow("x = hello > test")]
        [DataRow("x = hello == test")]
        [DataRow("x = hello < 5")]
        [DataRow("x = hello > 5")]
        [DataRow("x = hello == 5")]
        [DataRow("x = 5 < hello")]
        [DataRow("x = 5 > hello")]
        [DataRow("x = 5 == hello")]
        [DataTestMethod, Priority(0)]
        public async Task ComparisonsWithFuncObjs(string decl) {
            const string setup = @"
def hello():
    return 1

def test():
    return 2
";
            string code = setup + decl;

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.Severity.Should().Be(Severity.Warning);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionComparison);
            diagnostic.Message.Should().Be(Resources.FunctionComparison);
            diagnostic.SourceSpan.Should().Be(7, 5, 7, decl.Length + 1);
        }

        [DataRow("x = hello() < test()")]
        [DataRow("x = hello() > test()")]
        [DataRow("x = hello() == test()")]
        [DataRow("x = hello() < 5")]
        [DataRow("x = hello() > 5")]
        [DataRow("x = hello() == 5")]
        [DataRow("x = 5 < hello()")]
        [DataRow("x = 5 > hello()")]
        [DataRow("x = 5 == hello()")]
        [DataTestMethod, Priority(0)]
        public async Task ComparisonsWithCalledFuncs(string decl) {
            const string setup = @"
def hello():
    return 1

def test():
    return 2
";
            string code = setup + decl;

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ComparisonsWithMethods() {
            const string code = @"
class C:
    def hello(self):
        pass

h = C()

x = C.hello < 5
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.Severity.Should().Be(Severity.Warning);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionComparison);
            diagnostic.Message.Should().Be(Resources.FunctionComparison);
            diagnostic.SourceSpan.Should().Be(8, 5, 8, 16);
        }

        [TestMethod, Priority(0)]
        public async Task ComparisonsWithClassConstructors() {
            const string code = @"
class C:
    def hello(self):
        pass

h = C < 5
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ComparisonsWithProperties() {
            const string code = @"
class C:
    def __init__(self):
        self.x = 10

    @property
    def get_x(self):
        return self.x

h = C()

x = C.get_x < 5
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
