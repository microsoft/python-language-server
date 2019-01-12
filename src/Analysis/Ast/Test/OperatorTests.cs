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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class OperatorTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();


        [TestMethod, Priority(0)]
        public async Task NotOperator() {
            const string code = @"

class C(object):
    def __nonzero__(self):
        pass

    def __bool__(self):
        pass

a = not C()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Bool);

            analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task UnaryOperatorPlus() {
            const string code = @"
class Result(object):
    pass

class C(object):
    def __pos__(self):
        return Result()

a = +C()
b = ++C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType("Result")
                .And.HaveVariable("b").OfType("Result");
        }

        [TestMethod, Priority(0)]
        public async Task UnaryOperatorMinus() {
            const string code = @"
class Result(object):
    pass

class C(object):
    def __neg__(self):
        return Result()

a = -C()
b = --C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType("Result")
                .And.HaveVariable("b").OfType("Result");
        }

        [TestMethod, Priority(0)]
        public async Task UnaryOperatorTilde() {
            const string code = @"
class Result(object):
    pass

class C(object):
    def __invert__(self):
        return Result()

a = ~C()
b = ~~C()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType("Result")
                .And.HaveVariable("b").OfType("Result");
        }

        [TestMethod, Priority(0)]
        [Ignore]
        public async Task TrueDividePython3X() {
            const string code = @"
class C:
    def __truediv__(self, other):
        return 42
    def __rtruediv__(self, other):
        return 3.0

a = C()
b = a / 'abc'
c = 'abc' / a
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float);
        }
    }
}
