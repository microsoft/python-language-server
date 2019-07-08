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
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class AbstractClassTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ClassInheritABC() {
            const string code = @"
from abc import ABC, abstractmethod

class C(ABC):
    @abstractmethod
    def method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var cls = analysis.Should().HaveClass("C").Which;
            cls.IsAbstract.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public async Task ClassInheritMultiple() {
            const string code = @"
from abc import ABC, abstractmethod

class B:
    def test(self):
        return 5

class C(ABC, B):
    @abstractmethod
    def method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var cls = analysis.Should().HaveClass("C").Which;
            cls.IsAbstract.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public async Task MultipleBasesOneHasAbstractMethods() {
            const string code = @"
from abc import ABC, abstractmethod

class B:
    @abstractmethod
    def test(self):
        return 5

class C(ABC, B):
    def method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var cls = analysis.Should().HaveClass("C").Which;
            cls.IsAbstract.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public async Task ClassInheritABCNoMethods() {
            const string code = @"
from abc import ABC

class C(ABC):
    def method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var cls = analysis.Should().HaveClass("C").Which;
            // because there are no methods on the abstract class, treat it as a normal class
            cls.IsAbstract.Should().BeFalse();
        }

        [TestMethod, Priority(0)]
        public async Task ClassWithAbstractMethodsNoABC() {
            const string code = @"
from abc import abstractmethod

class C():
    @abstractmethod
    def method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var cls = analysis.Should().HaveClass("C").Which;
            // because there are no methods on the abstract class, treat it as a normal class
            cls.IsAbstract.Should().BeFalse();
        }


        [TestMethod, Priority(0)]
        public async Task ClassInheritsAbstract() {
            const string code = @"
from abc import ABC, abstractmethod

class A(ABC):
    @abstractmethod
    def method(self):
        return 4

class B(A):
    def not_impl(self):
        return 2
";
            // note to Cameron - when implementing a method in the base class, it overwrites the abstract
            // method in the superclass, so when implementing, all you need to do is check the immediate parents 
            // to see if they have any abstract methods
            var analysis = await GetAnalysisAsync(code);

            var clsBase = analysis.Should().HaveClass("A").Which;
            var absFunc = clsBase.Should().HaveMethod("method").Which;
            absFunc.IsAbstract.Should().BeTrue();

            var cls = analysis.Should().HaveClass("B").Which;
            clsBase.IsAbstract.Should().BeTrue();
            cls.IsAbstract.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public async Task ClassInheritsAbstractImplementsAllMethods() {
            const string code = @"
from abc import ABC, abstractmethod

class A(ABC):
    @abstractmethod
    def method(self):
        return 4

class B(A):
    def method(self):
        return 10

    def not_impl(self):
        return 2
";
            var analysis = await GetAnalysisAsync(code);

            var clsBase = analysis.Should().HaveClass("A").Which;
            var absFunc = clsBase.Should().HaveMethod("method").Which;
            absFunc.IsAbstract.Should().BeTrue();

            var cls = analysis.Should().HaveClass("B").Which;
            var clsFunc = cls.Should().HaveMethod("method").Which;
            clsFunc.IsAbstract.Should().BeFalse();

            clsBase.IsAbstract.Should().BeTrue();
            cls.IsAbstract.Should().BeFalse();
        }

        [TestMethod, Priority(0)]
        public async Task ClassInheritsAbstractAddMoreAbstractMethods() {
            const string code = @"
from abc import ABC, abstractmethod

class A(ABC):
    @abstractmethod
    def method(self):
        return 1

class B(A):
    @abstractmethod
    def not_impl(self):
        return 3

class C(B):
    def new_method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveClass("A").Which.Should().HaveMethod("method").Which
                .IsAbstract.Should().BeTrue();

            var gParent = analysis.Should().HaveClass("B").Which;
            gParent.IsAbstract.Should().BeTrue();
            gParent.Should().HaveMethod("method").Which.IsAbstract.Should().BeTrue();

            var parent = analysis.Should().HaveClass("C").Which;
            parent.IsAbstract.Should().BeTrue();
            parent.Should().HaveMethod("method").Which.IsAbstract.Should().BeTrue();
            parent.Should().HaveMethod("not_impl").Which.IsAbstract.Should().BeTrue();

            var cls = analysis.Should().HaveClass("C").Which;
            cls.IsAbstract.Should().BeTrue();
            cls.Should().HaveMethod("method").Which.IsAbstract.Should().BeTrue();
            cls.Should().HaveMethod("not_impl").Which.IsAbstract.Should().BeTrue();
            cls.Should().HaveMethod("new_method").Which.IsAbstract.Should().BeFalse();
        }

        [TestMethod, Priority(0)]
        public async Task ClassDerivedFromAbstract() {
            const string code = @"
from abc import ABC, abstractmethod

class A(ABC):
    @abstractmethod
    def method(self):
        return 1

class B(A):
    def method(self):
        return 3

class C(B):
    def new_method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var gParent = analysis.Should().HaveClass("A").Which;
            gParent.IsAbstract.Should().BeTrue();
            gParent.Should().HaveMethod("method").Which.IsAbstract.Should().BeTrue();

            var parent = analysis.Should().HaveClass("B").Which;
            parent.IsAbstract.Should().BeFalse();
            parent.Should().HaveMethod("method").Which.IsAbstract.Should().BeFalse();

            var cls = analysis.Should().HaveClass("C").Which;
            cls.IsAbstract.Should().BeFalse();

            var tmp = cls.GetMembers<PythonFunctionType>();
            cls.Should().HaveMethod("method").Which.IsAbstract.Should().BeFalse();
            cls.Should().HaveMethod("new_method").Which.IsAbstract.Should().BeFalse();
        }

        [TestMethod, Priority(0)]
        public async Task ClassDerivedFromAbstractAddMoreAbstractMethods() {
            const string code = @"
from abc import ABC, abstractmethod

class A(ABC):
    @abstractmethod
    def method(self):
        return 1

class B(A):
    def method(self):
        return 3

class C(B):
    @abstractmethod
    def new_method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var gParent = analysis.Should().HaveClass("A").Which;
            gParent.IsAbstract.Should().BeTrue();
            gParent.Should().HaveMethod("method").Which.IsAbstract.Should().BeTrue();

            var parent = analysis.Should().HaveClass("B").Which;
            parent.IsAbstract.Should().BeFalse();
            parent.Should().HaveMethod("method").Which.IsAbstract.Should().BeFalse();

            var cls = analysis.Should().HaveClass("C").Which;
            cls.IsAbstract.Should().BeTrue();

            var tmp = cls.GetMembers<PythonFunctionType>();
            cls.Should().HaveMethod("method").Which.IsAbstract.Should().BeFalse();
            cls.Should().HaveMethod("new_method").Which.IsAbstract.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public async Task ClassInheritsPassesAbstractMethods() {
            const string code = @"
from abc import ABC, abstractmethod

class A(ABC):
    @abstractmethod
    def method(self):
        return 1

class B(A):
    def not_impl(self):
        return 3

class C(B):
    def method(self):
        return 4
";
            var analysis = await GetAnalysisAsync(code);

            var gParent = analysis.Should().HaveClass("A").Which;
            gParent.IsAbstract.Should().BeTrue();
            gParent.Should().HaveMethod("method").Which.IsAbstract.Should().BeTrue();

            var parent = analysis.Should().HaveClass("B").Which;
            parent.IsAbstract.Should().BeTrue();
            parent.Should().HaveMethod("method").Which.IsAbstract.Should().BeTrue();
            parent.Should().HaveMethod("not_impl").Which.IsAbstract.Should().BeFalse();

            var cls = analysis.Should().HaveClass("C").Which;
            cls.IsAbstract.Should().BeFalse();
            cls.Should().HaveMethod("method").Which.IsAbstract.Should().BeFalse();
        }
    }
}
