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
    public class LintDecoratorCombinationTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ClassMethodAndProperty() {
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
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InvalidDecoratorCombination);
            diagnostic.SourceSpan.Should().Be(4, 6, 4, 17);
            diagnostic.Message.Should().Be(Resources.InvalidDecoratorForProperty.FormatInvariant("Classmethods"));
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethodStaticMethod() {
            const string code = @"
class Test:
    @staticmethod
    @classmethod
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(2);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InvalidDecoratorCombination);
            diagnostic.SourceSpan.Should().Be(3, 6, 3, 18);
            diagnostic.Message.Should().Be(Resources.InvalidDecoratorForFunction.FormatInvariant("Staticmethod", "class"));

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InvalidDecoratorCombination);
            diagnostic.SourceSpan.Should().Be(4, 6, 4, 17);
            diagnostic.Message.Should().Be(Resources.InvalidDecoratorForFunction.FormatInvariant("Classmethod", "static"));
        }

        [TestMethod, Priority(0)]
        public async Task StaticMethodAndProperty() {
            const string code = @"
class Test:
    @property
    @staticmethod
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InvalidDecoratorCombination);
            diagnostic.SourceSpan.Should().Be(4, 6, 4, 18);
            diagnostic.Message.Should().Be(Resources.InvalidDecoratorForProperty.FormatInvariant("Staticmethods"));
        }


        [TestMethod, Priority(0)]
        public async Task StaticMethodClassMethodAndProperty() {
            const string code = @"
class Test:
    @property
    @staticmethod
    @classmethod
    def test(x, y, z):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(2);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InvalidDecoratorCombination);
            diagnostic.SourceSpan.Should().Be(4, 6, 4, 18);
            diagnostic.Message.Should().Be(Resources.InvalidDecoratorForProperty.FormatInvariant("Staticmethods"));

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InvalidDecoratorCombination);
            diagnostic.SourceSpan.Should().Be(5, 6, 5, 17);
            diagnostic.Message.Should().Be(Resources.InvalidDecoratorForProperty.FormatInvariant("Classmethods"));
        }

        [TestMethod, Priority(0)]
        public async Task UnboundStaticMethodClassMethodAndProperty() {
            const string code = @"
@staticmethod
@classmethod
def test(x, y, z):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task NoDecoratorMethods() {
            const string code = @"
def test(x, y, z):
    pass

class A:
    def test(self, x):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task AbstractClassPropertyNoErrors() {
            const string code = @"
from abc import abstractmethod

class A:
    @property
    @classmethod
    @abstractmethod
    def test(self, x):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task AbstractStaticClassMethodNoErrors() {
            const string code = @"
from abc import abstractmethod

class A:
    @property
    @staticmethod
    @classmethod
    @abstractmethod
    def test(self, x):
        pass

    @staticmethod
    @classmethod
    @abstractmethod
    def test1(cls, x):
        pass

    @property
    @staticmethod
    @classmethod
    @abstractmethod
    def test2(x, y):
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
