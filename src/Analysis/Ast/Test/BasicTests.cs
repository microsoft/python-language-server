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

using System;
using System.IO;
using System.Linq;
using System.Threading;
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
    public class BasicTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task SmokeTest() {
            const string code = @"
x = 'str'

class C:
    def method(self):
        return func()

def func():
    return 2.0

c = C()
y = c.method()
";
            var analysis = await GetAnalysisAsync(code);

            var names = analysis.GlobalScope.Variables.Names;
            names.Should().OnlyContain("x", "C", "func", "c", "y");

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Str);
            analysis.Should().HaveVariable("C").Which.Value.Should().BeAssignableTo<IPythonClassType>();

            analysis.Should().HaveVariable("func")
                .Which.Value.Should().BeAssignableTo<IPythonFunctionType>();

            var v = analysis.Should().HaveVariable("c").Which;
            var instance = v.Value.Should().BeAssignableTo<IPythonInstance>().Which;
            instance.MemberType.Should().Be(PythonMemberType.Instance);
            instance.Type.Should().BeAssignableTo<IPythonClassType>();

            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task ImportTest() {
            const string code = @"
import sys
x = sys.path
";
            var analysis = await GetAnalysisAsync(code);
            analysis.GlobalScope.Variables.Count.Should().Be(2);

            analysis.Should()
                .HaveVariable("sys").OfType(BuiltinTypeId.Module)
                .And.HaveVariable("x").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinsTest() {
            const string code = @"
x = 1
";
            var analysis = await GetAnalysisAsync(code);

            var v = analysis.Should().HaveVariable("x").Which;
            var t = v.Value.GetPythonType();
            t.Should().BeAssignableTo<IMemberContainer>();

            var mc = (IMemberContainer)t;
            var names = mc.GetMemberNames().ToArray();
            names.Length.Should().BeGreaterThan(50);
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinsTrueFalse() {
            const string code = @"
booltypetrue = True
booltypefalse = False
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable(@"booltypetrue").OfType(BuiltinTypeId.Bool)
                .And.HaveVariable(@"booltypefalse").OfType(BuiltinTypeId.Bool);
        }
    }
}
