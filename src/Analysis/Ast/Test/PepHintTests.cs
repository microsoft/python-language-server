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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class PepHintTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        // https://www.python.org/dev/peps/pep-0526/
        [TestMethod, Priority(0)]
        public async Task BasicHints() {
            const string code = @"
from typing import List, Dict
import datetime

a = ...  # type: str
x = ...  # type: int
primes = []  # type: List[int]
stats = {}  # type: Dict[str, int]

class Response: # truncated
    encoding = ...  # type: str
    cookies = ...  # type: int
    elapsed = ...  # type: datetime.timedelta
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            analysis.Should().HaveVariable("primes").OfType("List[int]");
            analysis.Should().HaveVariable("stats").OfType("Dict[str, int]");

            var cls = analysis.Should().HaveVariable("Response").Which;
            cls.Value.Should().BeAssignableTo<IPythonClassType>();

            var c = cls.Value.GetPythonType();
            c.Should().HaveMember<IPythonInstance>("encoding")
                .Which.Should().HaveType(BuiltinTypeId.Str);
            c.Should().HaveMember<IPythonInstance>("cookies")
                .Which.Should().HaveType(BuiltinTypeId.Int);

            var dt = analysis.GlobalScope.Variables["datetime"];
            dt.Should().NotBeNull();
            var timedelta = dt.Value.GetPythonType().GetMember(@"timedelta");
            timedelta.IsUnknown().Should().BeFalse();

            c.Should().HaveMember<IPythonInstance>("elapsed")
                .Which.Should().HaveSameMembersAs(timedelta);
        }
    }
}
