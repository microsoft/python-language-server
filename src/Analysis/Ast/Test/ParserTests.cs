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
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ParserTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task RedeclareGlobal() {
            const string code = @"
def testGlobal(self):
    # 'global' NAME (',' NAME)*
    global a
    global a, b
";
            var analysis = await GetAnalysisAsync(code);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ImportStarInNestedFunction() {
            const string code = @"
def test():
    def test2():
        from random import *
        print(randint(1, 10))
    test2()
    print('hi')
test()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().HaveCount(2);
            diag[0].Message.Should().Be("import * only allowed at module level");
            diag[0].Severity.Should().Be(Severity.Error);
            diag[0].SourceSpan.Should().Be(4, 28, 4, 29);

            diag[1].Message.Should().Be("import * is not allowed in function 'test2' because it is a nested function");
            diag[1].Severity.Should().Be(Severity.Error);
            diag[1].SourceSpan.Should().Be(3, 5, 5, 30);
        }

        [TestMethod, Priority(0)]
        public async Task ImportStarInNestedFunctionWithFreeVariables() {
            const string code = @"
def test():
    from random import *
    def test2(a, b):
        print(randint(1, 10))
    test2(1, 2)
    print('hi')

test()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().HaveCount(2);
            diag[0].Message.Should().Be("import * only allowed at module level");
            diag[0].Severity.Should().Be(Severity.Error);
            diag[0].SourceSpan.Should().Be(3, 24, 3, 25);

            diag[1].Message.Should().Be("import * is not allowed in function 'test' because it contains a nested function with free variables");
            diag[1].Severity.Should().Be(Severity.Error);
            diag[1].SourceSpan.Should().Be(2, 1, 7, 16);
        }

        [TestMethod, Priority(0)]
        public async Task UnqualifiedExecContainsNestedFreeVariables() {
            const string code = @"
def func():
    exec 'print ""hi from func""'
    def subfunction():
        return True

func()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().HaveCount(1);

            diag[0].Message.Should().Be("unqualified exec is not allowed in function 'func' because it contains a nested function with free variables");
            diag[0].Severity.Should().Be(Severity.Error);
            diag[0].SourceSpan.Should().Be(2, 1, 5, 20);
        }

        [TestMethod, Priority(0)]
        public async Task DeleteVariableInNestedScope() {
            const string code = @"
def foo():
    x=5
    add=lambda a:x+a
    del x
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().HaveCount(1);

            diag[0].Message.Should().Be("cannot delete variable 'x' referenced in nested scope");
            diag[0].Severity.Should().Be(Severity.Error);
            diag[0].SourceSpan.Should().Be(4, 9, 4, 21);
        }

        [TestMethod, Priority(0)]
        public async Task NoBindingForNonlocal() {
            const string code = @"
def f():
    nonlocal x
    return 42
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().HaveCount(1);

            diag[0].Message.Should().Be("no binding for nonlocal 'x' found");
            diag[0].Severity.Should().Be(Severity.Error);
            diag[0].SourceSpan.Should().Be(3, 14, 3, 15);
        }

        [TestMethod, Priority(0)]
        public async Task RedeclareNonlocal() {
            const string code = @"
def test_nonlocal(self):
    x = 0
    y = 0
    def f():
        nonlocal x
        nonlocal x, y
";
            var analysis = await GetAnalysisAsync(code);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task DeclareNonlocalBeforeUse() {
            const string code = @"
class TestSuper(unittest.TestCase):
    def tearDown(self):
        nonlocal __class__
        __class__ = TestSuper
";
            var analysis = await GetAnalysisAsync(code);
            var diag = analysis.Document.GetParseErrors().ToArray();
            diag.Should().BeEmpty();
        }
    }
}
