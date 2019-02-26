﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class InheritanceTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BaseFunctionCall() {
            const string code = @"
class Baze:
  def foo(self, x):
    return 'base'

class Derived(Baze):
  def foo(self, x):
    return x

y = Baze().foo(42.0)
";

            var analysis = await GetAnalysisAsync(code);
            // the class, for which we know parameter type initially
            analysis.Should().HaveClass(@"Baze")
                    .Which.Should().HaveMethod("foo")
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveParameterAt(1)
                    .Which.Should().HaveName("x");

            // its derived class
            analysis.Should().HaveClass("Derived")
                .Which.Should().HaveMethod("foo")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(1)
                .Which.Should().HaveName("x");

            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public async Task DerivedFunctionCall() {
            const string code = @"
class Baze:
  def foo(self, x):
    return 'base'

class Derived(Baze):
  def foo(self, x):
    return x

y = Derived().foo(42)
";

            var analysis = await GetAnalysisAsync(code);

            // the class, for which we know parameter type initially
            analysis.Should().HaveClass("Derived").Which.Should().HaveMethod("foo")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(1)
                .Which.Should().HaveName("x");

            // its base class
            analysis.Should().HaveClass(@"Baze").Which.Should().HaveMethod("foo")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameterAt(1)
                .Which.Should().HaveName("x");

            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int);
        }


        [TestMethod, Priority(0)]
        public async Task NamedTupleSubclass() {
            const string code = @"
import collections

class A(collections.namedtuple('A', [])):
    def __new__(cls):
        return super(A, cls).__new__(cls)

a = A()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a")
                .Which.Value.Should().BeAssignableTo<IPythonInstance>()
                .Which.Type.Name.Should().Be("A");
        }
    }
}
