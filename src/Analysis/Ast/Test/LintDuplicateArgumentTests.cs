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
    public class LintDuplicateArgumentTests : AnalysisTestBase {

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task DuplicateArgsInFunc() {
            const string code = @"
def test(a, a, b):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 9);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));
        }

        [TestMethod, Priority(0)]
        public async Task DuplicateArgsInNestedFunc() {
            const string code = @"
def test(a, a, b):
    print('hi')
    def test2(c, c, e):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(2);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 9);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.SourceSpan.Should().Be(4, 9, 4, 14);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("c"));
        }

        [TestMethod, Priority(0)]
        public async Task DuplicateNamedArgs() {
            const string code = @"
def test(a=1, a=2):
    print('hi')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 9);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));
        }

        [TestMethod, Priority(0)]
        public async Task DuplicateNamedAndKeywordArgs() {
            const string code = @"
def test(a=1, **a):
    print('hi')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 9);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));
        }

        [TestMethod, Priority(0)]
        public async Task DuplicateListAndKeywordArgs() {
            const string code = @"
def test(*a, **a):
    print('hi')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 9);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));
        }

        [TestMethod, Priority(0)]
        public async Task DuplicateNamedAndListAndKeywordArgs() {
            const string code = @"
def test(a=1, *a, **a):
    print('hi')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 9);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));
        }

        [TestMethod, Priority(0)]
        public async Task DuplicateNamedAndUnnamedArgs() {
            const string code = @"
def test(a, a=2):
    print('hi')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 5, 2, 9);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));
        }

        [TestMethod, Priority(0)]
        public async Task DuplicateArgsInClassFunc() {
            const string code = @"
class Test: 
    def test(a, a, b):
        print('hi')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(3, 9, 3, 13);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.DuplicateArgumentName);
            diagnostic.Message.Should().Be(Resources.DuplicateArgumentName.FormatInvariant("a"));
        }

        [TestMethod, Priority(0)]
        public async Task NormalFuncDefinition() {
            const string code = @"
class Test: 
    def test(a, b, c=1, *args, **kwargs):
        print('hi')
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }
    }
}
