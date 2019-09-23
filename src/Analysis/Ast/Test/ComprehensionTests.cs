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
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ComprehensionTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ListComprehension() {
            const string code = @"
x = [e for e in {1, 2, 3}]
y = x[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Int)
                .And.NotHaveVariable("e");
        }

        [TestMethod, Priority(0)]
        public async Task ListComprehensionExpression() {
            const string code = @"
x = [e > 0 for e in {1, 2, 3}]
y = x[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Bool)
                .And.NotHaveVariable("e");
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryComprehension() {
            const string code = @"
x = {str(k): e > 0 for e in {1, 2, 3}}

keys = x.keys()
k = keys[0]
values = x.values()
v = values[0]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("k").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("v").OfType(BuiltinTypeId.Bool)
                .And.NotHaveVariable("e");
        }

        // TODO handle dictionary comprehension better
        [Ignore, TestMethod, Priority(0)]
        public async Task DictionaryComprehensionTuple() {
            const string code = @"
x = {k: v for (k, v) in [(1, 1), (2, 2), (3, 3)]}

y = x[1]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Int)
                .And.NotHaveVariable("k")
                .And.NotHaveVariable("v");
        }

        [TestMethod, Priority(0)]
        public async Task ListComprehensionStatement() {
            const string code = @"[e > 0 for e in {1, 2, 3}]";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            analysis.Should().HaveVariable("e").OfType(BuiltinTypeId.Int);
        }
    }
}