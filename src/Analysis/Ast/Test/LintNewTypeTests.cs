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
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintNewTypeTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task NewTypeIntFirstArg() {
            const string code = @"
from typing import NewType

T = NewType(5, int)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(4, 5, 4, 20);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingNewTypeArguments);
            diagnostic.Message.Should().Be(Resources.NewTypeFirstArgNotString.FormatInvariant("int"));
        }

        [DataRow("float", "float")]
        [DataRow("int", "int")]
        [DataRow("complex", "str")]
        [DataTestMethod, Priority(0)]
        public async Task DifferentTypesFirstArg(string nameType, string type) {
            string code = $@"
from typing import NewType

T = NewType({nameType}(10), {type})

";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingNewTypeArguments);
            diagnostic.Message.Should().Be(Resources.NewTypeFirstArgNotString.FormatInvariant(nameType));
        }

        [TestMethod, Priority(0)]
        public async Task ObjectFirstArg() {
            string code = $@"
from typing import NewType

class X:
    def hello():
        pass

h = X()

T = NewType(h, int)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(10, 5, 10, 20);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingNewTypeArguments);
            diagnostic.Message.Should().Be(Resources.NewTypeFirstArgNotString.FormatInvariant("X"));
        }

        [TestMethod, Priority(0)]
        public async Task GenericFirstArg() {
            string code = $@"
from typing import NewType, Generic, TypeVar

T = TypeVar('T', str, int)

class X(Generic[T]):
    def __init__(self, p: T):
        self.x = p

h = X(5)
T = NewType(h, int)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(11, 5, 11, 20);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingNewTypeArguments);
            diagnostic.Message.Should().Be(Resources.NewTypeFirstArgNotString.FormatInvariant("X[int]"));
        }

        [DataRow("test", "float")]
        [DataRow("testing", "int")]
        [DataTestMethod, Priority(0)]
        public async Task NoDiagnosticOnStringFirstArg(string name, string type) {
            string code = $@"
from typing import NewType

T = NewType('{name}', {type})
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }
    }
}
