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

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class TypingTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ListContent() {
            const string code = @"
from typing import List

lstr: List[str]
x = lstr[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"lstr").OfType("List[str]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ListOfLists() {
            const string code = @"
from typing import List

lst: List[List[int]]
x = lst[0]
y = x[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"lst").OfType("List[List[int]]")
                .And.HaveVariable("x").OfType("List[int]")
                .And.HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionAnnotatedToList() {
            const string code = @"
from typing import List

def f() -> List[str]: ...
x = f()[0]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnDocumentation("List[str]");

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionWithListArgument() {
            const string code = @"
from typing import List

def f(a: List[str]):
    return a

x = f(1)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionFetchingFromList() {
            const string code = @"
from typing import List

def f(a: List[str], index: int):
    return a[index]

x = f(1)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task TypeVarSimple() {
            const string code = @"
from typing import TypeVar

T = TypeVar('T', str, bytes)

def longest(x: T, y: T):
    return x if len(x) >= len(y) else y

x = longest('a', 'bc')
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("T").OfType(typeof(IPythonTypeDeclaration))
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task TypeAlias() {
            const string code = @"
Url = str

def f(a: Url) -> Url: ...
def f(a: int) -> float: ...

u: Url
x = f('s')
y = f(u)
z = f(1)
";

            var analysis = await GetAnalysisAsync(code);
            // TODO: should it be Url? Should we rename type copy on assignment? How to match types then?
            analysis.Should().HaveVariable("u").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("x").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task TupleContent() {
            const string code = @"
from typing import Tuple

t: Tuple[int, str]
x = t[0]
y = t[1]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("t").OfType("Tuple[int, str]")
                .And.HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task TupleOfTuple() {
            const string code = @"
from typing import Tuple

t: Tuple[Tuple[int, str], bool]
x = t[0]
y = t[1]
z0 = x[0]
z1 = x[1]
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"lst").OfType("Tuple[Tuple[int, str], bool]")
                .And.HaveVariable("x").OfType("Tuple[int, str]")
                .And.HaveVariable("y").OfType(BuiltinTypeId.Bool)
                .And.HaveVariable("z1").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("z2").OfType(BuiltinTypeId.Str);
        }
    }
}
