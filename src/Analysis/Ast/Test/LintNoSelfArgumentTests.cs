// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintNoSelfArgumentTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task FirstArgumentMethodNotSelf() {
            const string code = @"
class Test:
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoSelfArgument);
            diagnostic.SourceSpan.Should().Be(3, 14, 3, 15);
            diagnostic.Message.Should().Be(Resources.NoSelfArgument.FormatInvariant("test"));
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentPropertyNotSelf() {
            const string code = @"
class Test:
    @property
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoSelfArgument);
            diagnostic.SourceSpan.Should().Be(4, 14, 4, 15);
            diagnostic.Message.Should().Be(Resources.NoSelfArgument.FormatInvariant("test"));
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentAbstractPropertyNotSelf() {
            const string code = @"
class Test:
    @abstractproperty
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoSelfArgument);
            diagnostic.SourceSpan.Should().Be(4, 14, 4, 15);
            diagnostic.Message.Should().Be(Resources.NoSelfArgument.FormatInvariant("test"));
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentNotSelfMultiple() {
            const string code = @"
class Test:
    def test(x, y, z):
        pass

    def test2(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(2);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoSelfArgument);
            diagnostic.SourceSpan.Should().Be(3, 14, 3, 15);
            diagnostic.Message.Should().Be(Resources.NoSelfArgument.FormatInvariant("test"));

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoSelfArgument);
            diagnostic.SourceSpan.Should().Be(6, 15, 6, 16);
            diagnostic.Message.Should().Be(Resources.NoSelfArgument.FormatInvariant("test2"));
        }

        [TestMethod, Priority(0)]
        public async Task NestedClassFuncNoSelfArg() {
            const string code = @"
class Test:
    class Test2:
        def hello(x, y, z):
            pass

    def test(x, y, z): ...
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(2);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoSelfArgument);
            diagnostic.SourceSpan.Should().Be(4, 19, 4, 20);
            diagnostic.Message.Should().Be(Resources.NoClsArgument.FormatInvariant("hello"));

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoSelfArgument);
            diagnostic.SourceSpan.Should().Be(7, 14, 7, 15);
            diagnostic.Message.Should().Be(Resources.NoClsArgument.FormatInvariant("test"));
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentIsSelf() {
            const string code = @"
class Test:
    def test(self):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentIsSelfManyParams() {
            const string code = @"
class Test:
    def test(self, a, b, c, d, e):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task StaticMethodNoSelfValid() {
            const string code = @"
class C:
    @staticmethod
    def test(a, b):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task NormalFunction() {
            const string code = @"
def test():
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
