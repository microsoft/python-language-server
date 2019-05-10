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

        [DataRow("add", "+")]
        [DataRow("sub", "-")]
        [DataRow("mul", "*")]
        [DataRow("matmul", "@")]
        [DataRow("truediv", "/")]
        [DataRow("and", "&")]
        [DataRow("or", "|")]
        [DataRow("xor", "^")]
        [DataRow("lshift", "<<")]
        [DataRow("rshift", ">>")]
        [DataRow("pow", "**")]
        [DataRow("floordiv", "//")]
        [DataTestMethod, Priority(0)]
        public async Task CustomOperators(string func, string op) {
            var code = $@"
class C:
    def __{func}__(self, other):
        return 42
    def __r{func}__(self, other):
        return 3.0

a = C()
b = a {op} 'abc'
c = 'abc' {op} a
d = a {op} a
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Int);
        }

        [DataRow("lt", "ge", "<")]
        [DataRow("gt", "le", ">")]
        [DataRow("ge", "lt", ">=")]
        [DataRow("eq", "ne", "==")]
        [DataTestMethod, Priority(0)]
        public async Task CustomComparison(string func1, string func2, string op) {
            var code = $@"
class C:
    def __{func1}__(self, other):
        return 42

    def __{func2}__(self, other):
        return 3.0

a = C()
b = a {op} 'abc'
c = 'abc' {op} a
d = a {op} a
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ModWithString() {
            const string code = @"
class C:
    def __mod__(self, other):
        return 42
    def __rmod__(self, other):
        return 3.0

a = C()
b = a % 'abc'
c = 'abc' % a
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinMatrixMul() {
            const string code = "x = 1 @ 2";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Unknown);
        }

        [DataRow("x = 1 ^ 2", BuiltinTypeId.Int, true)]
        [DataRow("x = True ^ False", BuiltinTypeId.Bool, true)]
        [DataRow("x = 1 ^ False", BuiltinTypeId.Int, true)]
        [DataRow("x = False ^ 1", BuiltinTypeId.Int, true)]
        [DataRow("x = 1L | 2L", BuiltinTypeId.Long, false)]
        [DataRow("x = False | 1L", BuiltinTypeId.Long, false)]
        [DataRow("x = 1L | False", BuiltinTypeId.Long, false)]
        [DataRow("x = 1L & 2", BuiltinTypeId.Long, false)]
        [DataRow("x = 1 & 2L", BuiltinTypeId.Long, false)]
        [DataRow("x = 1.0 & 3j", BuiltinTypeId.Unknown, true)]
        [DataTestMethod, Priority(0)]
        public async Task BuiltinBitwise(string code, BuiltinTypeId typ, bool is3x) {
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(typ);
        }

        [DataRow("x = 1 << 2", BuiltinTypeId.Int, true)]
        [DataRow("x = True << False", BuiltinTypeId.Int, true)]
        [DataRow("x = 1 >> False", BuiltinTypeId.Int, true)]
        [DataRow("x = False >> 1", BuiltinTypeId.Int, true)]
        [DataRow("x = 1L << 2L", BuiltinTypeId.Long, false)]
        [DataRow("x = False << 1L", BuiltinTypeId.Long, false)]
        [DataRow("x = 1L >> False", BuiltinTypeId.Long, false)]
        [DataRow("x = 1L >> 2", BuiltinTypeId.Long, false)]
        [DataRow("x = 1 << 2L", BuiltinTypeId.Long, false)]
        // [DataRow("x = 1.0 << 3j", BuiltinTypeId.Unknown, true)]
        [DataTestMethod, Priority(0)]
        public async Task BuiltinShifting(string code, BuiltinTypeId typ, bool is3x) {
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(typ);
        }

        [DataRow("x = 1 < 2", true)]
        [DataRow("x = True > False", true)]
        [DataRow("x = 1 <= False", true)]
        [DataRow("x = False >= 1", true)]
        [DataRow("x = 1L == 2L", false)]
        [DataRow("x = False != 1L", false)]
        [DataRow("x = 1L < False", false)]
        [DataRow("x = 1L > 2", false)]
        [DataRow("x = 1 != 2L", false)]
        [DataRow("x = 1.0 == 3j", true)]
        [DataTestMethod, Priority(0)]
        public async Task BuiltinComparison(string code, bool is3x) {
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Bool);
        }

        [DataRow("x = 'x' + u'x'")]
        [DataRow("x = u'x' + u'x'")]
        [DataRow("x = u'x' + 'x'")]
        [DataTestMethod, Priority(0)]
        public async Task UnicodeConcat(string code) {
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task OperatorNotImplementedDefault() {
            const string code = @"
class C:
    pass

a = C()
b = a + a
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("b").OfType("C");
        }

        [TestMethod, Priority(0)]
        public async Task DatetimeTimedelta() {
            const string code = @"
from datetime import datetime

x = datetime() - datetime()
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType("timedelta");
        }

        [TestMethod, Priority(0)]
        public async Task CompareModules() {
            const string code = @"
import os
import sys

x = os < sys
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task CompareUnknown() {
            const string code = @"
x = foo < bar
";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Bool);
        }
    }
}
