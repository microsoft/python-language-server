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
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintUndefinedVarsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicVariables() {
            const string code = @"
y = x

class A:
    x1: int = 0
    y1: int
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 5, 2, 6);
        }

        [TestMethod, Priority(0)]
        public async Task ClassVariables() {
            const string code = @"
class A:
    x1: int = 0
    y1: int
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task Conditionals() {
            const string code = @"
z = 3
if x > 2 and y == 3 or z < 2:
    pass
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(2);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(3, 4, 3, 5);
            d[1].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[1].SourceSpan.Should().Be(3, 14, 3, 15);
        }

        [TestMethod, Priority(0)]
        public async Task Calls() {
            const string code = @"
z = 3
func(x, 1, y+1, z)
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(3);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(3, 1, 3, 5);
            d[1].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[1].SourceSpan.Should().Be(3, 6, 3, 7);
            d[2].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[2].SourceSpan.Should().Be(3, 12, 3, 13);
        }

        [TestMethod, Priority(0)]
        public async Task TupleAssignment() {
            const string code = @"
a, *b, c = range(5)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task TupleUsage() {
            const string code = @"
a, b = 1
x = a
y = b
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FunctionNoneArgument() {
            const string code = @"
def func(a=None, b=True):
    x = a
    y = b
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task FunctionReference() {
            const string code = @"
def func1():
    func2()
    return

def func2(a=None, b=True): ...
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ClassReference() {
            const string code = @"
class A(B):
    def __init__(self):
        x = B()
        return

class B(): ...
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForVariables() {
            const string code = @"
c = {}
for i in c:
    x = i
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForVariablesCondition() {
            const string code = @"
c = {}
for a, b in c if a < 0:
    x = b
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForVariablesList() {
            const string code = @"
for i, (j, k) in {}:
    x = j
    y = k
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ForExpression() {
            const string code = @"
def func1(a):
    return a

def func2(a, b):
    return a + b

func1(func2(a) for a, b in {} if a < 0)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task ListComprehension() {
            const string code = @"
NAME = ' '.join(str(x) for x in {z, 2, 3})

class C:
    EVENTS = ['x']
    x = [(e, e) for e in EVENTS]
    y = EVENTS
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 34, 2, 35);
        }

        [TestMethod, Priority(0)]
        public async Task SelfAssignment() {
            const string code = @"
def foo(m):
    m = m
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task AssignmentBefore() {
            const string code = @"
x = 1
y = x
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task AssignmentBeforeAndAfter() {
            const string code = @"
x = 1
y = x
x = 's'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task AssignmentAfter() {
            const string code = @"
y = x
x = 1
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(2, 5, 2, 6);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionArguments() {
            const string code = @"
def z(x):
    return x

def func(a, b, c):
    a = b
    x = c
    z(c * 3)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task NonLocal() {
            const string code = @"
class A:
    x: int
    def func():
        nonlocal x, y
        y = 2
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(5, 21, 5, 22);
        }

        [TestMethod, Priority(0)]
        public async Task Global() {
            const string code = @"
x = 1

class A:
    def func():
        global x, y
        y = 2
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(6, 19, 6, 20);
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task OptionsSwitch(bool enabled) {
            const string code = @"x = y";

            var sm = CreateServiceManager();
            var op = new AnalysisOptionsProvider();
            sm.AddService(op);

            op.Options.LintingEnabled = enabled;
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X, sm);
            analysis.Diagnostics.Should().HaveCount(enabled ? 1 : 0);
        }

        [TestMethod, Priority(0)]
        public async Task Builtins() {
            const string code = @"
print(1)
abs(3)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task Enumeration() {
            const string code = @"
x = {}
for a, b in enumerate(x):
    if a:
        pass
    if b:
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ReassignInLoop() {
            const string code = @"
x = {}
for a, b in enumerate(x):
    y = a
    a = b
    b = 1
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task ListComprehensionStatement() {
            const string code = @"
[a == 1 for a in {}]
x = a
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task DictionaryComprehension() {
            const string code = @"
b = {str(a): a == 1 for a in {}}
x = a
";
            var analysis = await GetAnalysisAsync(code);
            var d = analysis.Diagnostics.ToArray();
            d.Should().HaveCount(1);
            d[0].ErrorCode.Should().Be(ErrorCodes.UndefinedVariable);
            d[0].SourceSpan.Should().Be(3, 5, 3, 6);
        }

        [TestMethod, Priority(0)]
        public async Task Lambda() {
            const string code = @"
x = lambda a: a
x(1)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task LambdaComrehension() {
            const string code = @"
y = lambda x: [e for e in x if e == 1]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        private class AnalysisOptionsProvider : IAnalysisOptionsProvider {
            public AnalysisOptions Options { get; } = new AnalysisOptions();
        }
    }
}
