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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintAbstractClassInstantiatedTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicInstantiateAbstractClass() {
            const string code = @"
from abc import ABC, abstractmethod

class C(ABC):
    @abstractmethod
    def method(self):
        return 4

h = C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.AbstractClassInstantiated);
            diagnostic.Message.Should().Be(Resources.AbstractClassInstantiated.FormatInvariant("C"));
            diagnostic.SourceSpan.Should().Be(9, 5, 9, 8);

            analysis.Should().HaveVariable("h").Which.Should().HaveType(BuiltinTypeId.Unknown);
        }

        [TestMethod, Priority(0)]
        public async Task InstantiateAbstractClassWithInheritance() {
            const string code = @"
from abc import ABC, abstractmethod

class B:
    def test(self):
        return 5

class C(ABC, B):
    @abstractmethod
    def method(self):
        return 4

h = C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.AbstractClassInstantiated);
            diagnostic.Message.Should().Be(Resources.AbstractClassInstantiated.FormatInvariant("C"));
            diagnostic.SourceSpan.Should().Be(13, 5, 13, 8);


            analysis.Should().HaveVariable("h").Which.Should().HaveType(BuiltinTypeId.Unknown);
        }

        [TestMethod, Priority(0)]
        public async Task InstantiateClassInheritsABCNoAbstractMethods() {
            const string code = @"
from abc import ABC

class C(ABC):
    def method(self):
        return 4

h = C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);

            analysis.Should().HaveVariable("h").Which.Should().HaveType(BuiltinTypeId.Type);
        }

        [TestMethod, Priority(0)]
        public async Task InstantiateNormalClass() {
            const string code = @"
class C():
    def method(self):
        return 4

h = C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);

            analysis.Should().HaveVariable("h").Which.Should().HaveType(BuiltinTypeId.Type);
        }
    }
}
