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

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ExpressionsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ConstantMathV2() {
            const string code = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
e = 1 + 1L # e is a 'float', should be 'long' under v2.x (error under v3.x)
f = 1 / 2 # f is 'int', should be 'float' under v3.x";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("e").OfType(BuiltinTypeId.Long)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ConstantMathV3() {
            const string code = @"
a = 1. + 2. + 3. # no type info for a, b or c
b = 1 + 2. + 3.
c = 1. + 2 + 3.
d = 1. + 2. + 3 # d is 'int', should be 'float'
f = 1 / 2 # f is 'int', should be 'float' under v3.x";

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("c").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("f").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task StringMultiplyV2() {
            const string code = @"
x = u'abc %d'
y = x * 100


x1 = 'abc %d'
y1 = x1 * 100

fob = 'abc %d'.lower()
oar = fob * 100

fob2 = u'abc' + u'%d'
oar2 = fob2 * 100"; ;

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("fob2").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("oar2").OfType(BuiltinTypeId.Unicode);
        }

        [TestMethod, Priority(0)]
        public async Task StringMultiplyV3() {
            const string code = @"
x = u'abc %d'
y = x * 100


x1 = 'abc %d'
y1 = x1 * 100

fob = 'abc %d'.lower()
oar = fob * 100

fob2 = u'abc' + u'%d'
oar2 = fob2 * 100";;

            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y1").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("fob2").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("oar2").OfType(BuiltinTypeId.Str);
        }
    }
}
