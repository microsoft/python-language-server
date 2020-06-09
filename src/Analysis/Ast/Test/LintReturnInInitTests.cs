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
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintReturnInInitTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ReturnInInit() {
            const string code = @"
class Rectangle:
    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.area = width * height
        return self.area

r = Rectangle(10, 10)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(7, 9, 7, 25);
            diagnostic.Severity.Should().Be(Severity.Warning);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.ReturnInInit);
            diagnostic.Message.Should().Be(Resources.ReturnInInit);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnInitBasic() {
            const string code = @"
class Rectangle:
    def __init__(self, width, height):
        return 2

r = Rectangle(10, 10)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(4, 9, 4, 17);
            diagnostic.Severity.Should().Be(Severity.Warning);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.ReturnInInit);
            diagnostic.Message.Should().Be(Resources.ReturnInInit);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnInInitConditional() {
            const string code = @"
class A:
    def __init__(self, x):
        self.x = x
        if x > 0:
            return 10

a = A(1)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(6, 13, 6, 22);
            diagnostic.Severity.Should().Be(Severity.Warning);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.ReturnInInit);
            diagnostic.Message.Should().Be(Resources.ReturnInInit);
        }

        [TestMethod, Priority(0)]
        public async Task ReturnNoneInInit() {
            const string code = @"
class A:
    def __init__(self, x):
        self.x = x
        self.x += 1
        return None

a = A(1)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task EmptyReturnInInit() {
            const string code = @"
class A:
    def __init__(self, x):
        self.x = x
        return 
a = A(1)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
