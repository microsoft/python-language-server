using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class InheritanceTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod]
        public async Task AbstractMethodReturnTypeIgnored() {
            var code = @"import abc
class A:
    @abc.abstractmethod
    def virt():
        pass
class B(A):
    def virt():
        return 42
a = A()
b = a.virt()";

            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("b").OfTypes(BuiltinTypeId.Int);
            }
        }

        [TestMethod]
        public async Task AbstractPropertyReturnTypeIgnored() {
            var code = @"
import abc

class A:
    @abc.abstractproperty
    def virt():
        pass

class B(A):
    @property
    def virt():
        return 42

a = A()
b = a.virt";

            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("b").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Function);
            }
        }

        [TestMethod]
        public async Task ParameterTypesPropagateDownAndBackUp() {
            var code = @"
class Baze:
  def foo(self, x):
    pass

class Derived1(Baze):
  def foo(self, x):
    pass

class Derived2(Baze):
  def foo(self, x):
    pass

Derived2().foo(42)
";

            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                // the class, for which we know parameter type initially
                analysis.Should().HaveClass("Derived2").WithFunction("foo")
                    .WithParameter("x").OfType(BuiltinTypeId.Int);
                // its most base class with the same function
                analysis.Should().HaveClass("Baze").WithFunction("foo")
                    .WithParameter("x").OfType(BuiltinTypeId.Int);
                // all the classes, derived from the base
                analysis.Should().HaveClass("Derived1").WithFunction("foo")
                    .WithParameter("x").OfType(BuiltinTypeId.Int);
            }
        }
    }
}
