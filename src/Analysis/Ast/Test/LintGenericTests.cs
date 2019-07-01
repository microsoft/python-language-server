using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class LintGenericTests : AnalysisTestBase {

        public const string GenericSetup = @"from typing import Generic, TypeVar
T = TypeVar('T', int, str)
T1 = TypeVar('T1', int, str)
";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow(GenericSetup + @"
class Map(Generic[T, str]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
class Map(Generic[T, T1, int]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
class Map(Generic[T, str, int, T1]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
class Map(Generic[str, int]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
class Map(Generic[str]):
    def hello():
        pass
")]


        [DataTestMethod, Priority(0)]
        public async Task GenericNotAllTypeParameters(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingGenericArguments);
            diagnostic.Message.Should().Be(Resources.GenericNotAllTypeParameters);
        }

        [DataRow(GenericSetup + @"
_X = TypeVar('_X', str, int)
_T = _X

class Map(Generic[_T, _X]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
_X = TypeVar('_X', str, int)
_T = _X

class Map(Generic[_T, T, T1, _X]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
_X = TypeVar('_X', str, int)
_T = _X

class Map(Generic[_T,_T, T]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
class Map(Generic[T,T]):
    def hello():
        pass
")]
        [DataTestMethod, Priority(0)]
        public async Task GenericDuplicateArguments(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingGenericArguments);
            diagnostic.Message.Should().Be(Resources.GenericNotAllUnique);
        }

        [DataRow(GenericSetup + @"
_X = TypeVar('_X', str, int)

class Map(Generic[_X, T]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
class Map(Generic[T1, T]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
class Map(Generic[T]):
    def hello():
        pass
")]
        [DataRow(GenericSetup + @"
_X = TypeVar('_X', str, int)
class Map(Generic[T,T1, _X]):
    def hello():
        pass
")]
        [DataTestMethod, Priority(0)]
        public async Task GenericArgumentsNoDiagnosticOnValid(string code) {
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }
    }
}
