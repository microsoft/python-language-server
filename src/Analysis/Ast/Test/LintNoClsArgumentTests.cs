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
    public class LintNoClsArgumentTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task FirstArgumentClassMethodNotCls() {
            const string code = @"
class Test:
    @classmethod
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoClsArgument);
            diagnostic.SourceSpan.Should().Be(4, 9, 4, 13);
            diagnostic.Message.Should().Be(Resources.NoClsArgument.FormatInvariant("test"));
        }

        // Putting property and classmethod is a bug per https://bugs.python.org/issue20659
        [Ignore]
        [TestMethod, Priority(0)]
        public async Task FirstArgumentClassMethodAndPropertyNotCls() {
            const string code = @"
class Test:
    @property
    @classmethod
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoClsArgument);
            diagnostic.SourceSpan.Should().Be(5, 9, 5, 13);
            diagnostic.Message.Should().Be(Resources.NoClsArgument.FormatInvariant("test"));
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentClassMethodSpecialCase() {
            const string code = @"
class Test:
    def __new__(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoClsArgument);
            diagnostic.SourceSpan.Should().Be(3, 9, 3, 16);
            diagnostic.Message.Should().Be(Resources.NoClsArgument.FormatInvariant("__new__"));
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentNotClsMultiple() {
            const string code = @"
class Test:
    @classmethod
    def test(x, y, z):
        pass

    @classmethod
    def test2(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(2);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoClsArgument);
            diagnostic.SourceSpan.Should().Be(4, 9, 4, 13);
            diagnostic.Message.Should().Be(Resources.NoClsArgument.FormatInvariant("test"));

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.NoClsArgument);
            diagnostic.SourceSpan.Should().Be(8, 9, 8, 14);
            diagnostic.Message.Should().Be(Resources.NoClsArgument.FormatInvariant("test2"));
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentIsCls() {
            const string code = @"
class Test:
    @classmethod
    def test(cls):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FirstArgumentIsClsManyParams() {
            const string code = @"
class Test:
    @classmethod
    def test(cls, a, b, c, d, e):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
