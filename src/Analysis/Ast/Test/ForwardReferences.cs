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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ForwardReferenceTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ForwardRefGlobalFunction() {
            const string code = @"
def func1():
    return func2()

def func2():
    return func3()

def func3():
    return 's'

x = func1()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRefFunction1() {
            const string code = @"
class A(object):
    def methodA(self):
        return 's'

class B(object):
    def getA(self):
        return self.funcA()

    def funcA(self):
        return A()

x = B().getA().methodA()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRefFunction2() {
            const string code = @"
class A(object):
    def methodA(self):
        return 's'

class B(object):
    def getA(self):
        return self.func1()

    def func1(self):
        return self.func2()

    def func2(self):
        return self.func3()

    def func3(self):
        return A()

x = B().getA().methodA()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRefClass() {
            const string code = @"
class A:
    def funcA(self):
        return B().methodB()

class B:
    def methodB(self):
        return 's'

x = A().funcA()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task ForwardRefClassMember() {
            const string code = @"
class A:
    def __init__(self):
        self.b = B().methodB()

class B:
    def methodB(self):
        return 's'

x = A().b
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
        }
    }
}
