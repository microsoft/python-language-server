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
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class WithTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task WithTuple() {
            const string code = @"
from typing import Tuple

class Test:
    def __enter__(self) -> Tuple[int, int]:
        return (1, 2)
    
    def __exit__(x, y, z, w):
        pass


with Test() as (hi, hello):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("hi").Which.Should().HaveType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("hello").Which.Should().HaveType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task WithName() {
            const string code = @"
from typing import Tuple

class Test:
    def __enter__(self) -> str:
        return (1, 2)
    
    def __exit__(x, y, z, w):
        pass


with Test() as test:
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("test").Which.Should().HaveType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task WithNameEnterNoReturnType() {
            const string code = @"
from typing import Tuple

class Test:
    def __enter__(self):
        pass
    
    def __exit__(x, y, z, w):
        pass


with Test() as (a, b, c, d):
    pass
";
            var analysis = await GetAnalysisAsync(code);
            // Uses context manager type when return type of __enter__ is unknown
            analysis.Should().HaveVariable("a").Which.Should().HaveType("Test");
            analysis.Should().HaveVariable("b").Which.Should().HaveType("Test");
            analysis.Should().HaveVariable("c").Which.Should().HaveType("Test");
            analysis.Should().HaveVariable("d").Which.Should().HaveType("Test");
        }

    }
}
