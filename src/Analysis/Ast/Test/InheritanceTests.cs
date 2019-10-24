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

        [TestMethod, Priority(0)]
        public async Task SuperShouldReturnBaseClassFunctions() {
            const string code = @"
class Baze:
    def base_func(self):
        return 1234

class Derived(Baze):
    def foo(self):
        x = super()
";

            var analysis = await GetAnalysisAsync(code);

            // the class, for which we know parameter type initially
            analysis.Should().HaveClass("Derived").Which.Should().HaveMethod("foo")
                .Which.Should().HaveVariable("x")
                .Which.Value.Should().HaveMemberName("base_func");
        }


        [TestMethod, Priority(0)]
        public async Task SuperShouldReturnBaseClassFunctionsWhenCalledWithSelf() {
            const string code = @"
class Baze:
    def base_func(self):
        return 1234

class Derived(Baze):
    def foo(self):
        x = super(Derived, self)
";

            var analysis = await GetAnalysisAsync(code);

            // the class, for which we know parameter type initially
            analysis.Should().HaveClass("Derived").Which.Should().HaveMethod("foo")
                .Which.Should().HaveVariable("x")
                .Which.Value.Should().HaveMemberName("base_func");
        }


        [TestMethod, Priority(0)]
        public async Task SuperWithSecondParamDerivedClassShouldOnlyHaveBaseMembers() {
            const string code = @"
class Baze:
    def baze_foo(self):
        pass

class Derived(Baze):
    def foo(self):
        pass

d = Derived()

x = super(Derived, d)
";

            var analysis = await GetAnalysisAsync(code);
            
            analysis.Should().HaveVariable("x")
                .Which.Value.Should().HaveMemberName("baze_foo");

            analysis.Should().HaveVariable("x")
                .Which.Value.Should().NotHaveMembers("foo");
        }

        [TestMethod, Priority(0)]
        public async Task SuperWithSecondParamParentShouldOnlyReturnGrandparentMembers() {
            const string code = @"
class A:
    def a_foo(self):
        pass

class B(A):
    def b_foo(self):
        pass

class C(B):
    def c_foo(self):
        pass

c = C()

x = super(B, c) # super starts its search after 'B' in the mro
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").Which.Value
                .Should().HaveMembers("a_foo")
                .And.NotHaveMembers("b_foo")
                .And.NotHaveMembers("c_foo");
        }


        [TestMethod, Priority(0)]
        public async Task SuperWithSecondParamInvalidShouldBeUnknown() {
            const string code = @"
class A:
    def a_foo(self):
        pass

class B(A):
    def b_foo(self):
        pass

x = super(B, DummyClass) # super starts its search after 'b'
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Unknown);
        }

        [TestMethod, Priority(0)]
        public async Task SuperWithFirstParamInvalid() {
            const string code = @"
class A:
    def a_foo(self):
        pass

class B(A):
    def b_foo(self):
        pass

x = super(DummyClass, B) # super starts its search after 'b'
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Unknown);
        }

        [TestMethod, Priority(0)]
        public async Task SuperUnbounded() {
            const string code = @"
class A:
    def a_foo(self):
        pass

class B(A):
    def b_foo(self):
        pass

x = super(B) # super starts its search after 'b'
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").Which.Value
                .Should().HaveMembers("a_foo")
                .And.NotHaveMembers("b_foo");
        }

        [TestMethod, Priority(0)]
        public async Task SuperWithNoBaseShouldReturnObject() {
            const string code = @"
class A():
    def foo(self):
        x = super()
";
            var analysis = await GetAnalysisAsync(code);

            // the class, for which we know parameter type initially
            analysis.Should().HaveClass("A").Which.Should().HaveMethod("foo")
                .Which.Should().HaveVariable("x").OfType(BuiltinTypeId.Type);
        }

        [TestMethod, Priority(0)]
        public async Task FunctionWithNoClassCallingSuperShouldFail() {
            const string code = @"
def foo(self):
    x = super()
";

            var analysis = await GetAnalysisAsync(code);
          
            analysis.Should().HaveFunction("foo")
                .Which.Should().HaveVariable("x")
                .Which.Name.Should().Be("x");
        }

        [TestMethod, Priority(0)]
        public async Task FunctionAssigningIntToSuperShouldBeInt() {
            const string code = @"
def foo(self):
    super = 1
";

            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveFunction("foo")
                .Which.Should().HaveVariable("super")
                .Which.Value.IsOfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task SuperShouldReturnAllBaseClassMembers() {
            const string code = @"
class GrandParent:
    def grand_func(self):
        return 1

class Parent(GrandParent):
    def parent_func(self):
        return 2

class Child(Parent):
    def child_func(self):
        x = super()
";

            var analysis = await GetAnalysisAsync(code);

            var x = analysis.Should().HaveClass("Child").Which.Should().HaveMethod("child_func")
                .Which.Should().HaveVariable("x").Which;

            x.Value.Should().HaveMemberName("grand_func");
            x.Value.Should().HaveMemberName("parent_func");
        }


        [TestMethod, Priority(0)]
        public async Task SuperShouldReturnAllBaseClassMembersGenerics() {
            const string code = @"
from typing import Generic, TypeVar
T = TypeVar('T')
class A(Generic[T]):
    def af(self, x: T) -> T:
        return x
class B(A[int]):  # leave signature as is
    def bf(self, x: T) -> T:
        y = super()
        return x
";

            var analysis = await GetAnalysisAsync(code);

            var y = analysis.Should().HaveClass("B")
                .Which.Should().HaveMethod("bf")
                .Which.Should().HaveVariable("y").Which;

            y.Value.Should().HaveMemberName("af");
        }

    


        //        [TestMethod, Priority(0)]
        //        public async Task MultipleInheritanceSuperShould() {
        //            const string code = @"
        //class GrandParent:
        //    def dowork(self):
        //        return 1

        //class Dad(GrandParent):
        //    def dowork(self):
        //        return super().dowork()

        //class Mom():
        //    def dowork(self):
        //        return 2

        //class Child(Dad, Mom):
        //    def child_func(self):
        //        x = super()

        //";
        //            var analysis = await GetAnalysisAsync(code);

        //            analysis.Should().HaveClass("Child")
        //                .Which.Should().HaveMethod("child_func")
        //                .Which.Should().HaveVariable("x")
        //                .Which.Value.Should().BeAssignableTo<IPythonInstance>()
        //                .Which.Type.Name.Should().Be("Mom");
        //        }
    }
}
