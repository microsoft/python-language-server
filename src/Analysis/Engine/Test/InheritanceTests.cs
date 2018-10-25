using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
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

        [ServerTestMethod(Version = PythonLanguageVersion.V36), Priority(0)]
        public async Task AbstractMethodReturnTypeIgnored(Server server) {
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

            var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
            analysis.Should().HaveVariable("b").OfTypes(BuiltinTypeId.Int);
        }

        [ServerTestMethod(Version = PythonLanguageVersion.V36), Priority(0)]
        public async Task AbstractPropertyReturnTypeIgnored(Server server) {
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

            var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
            analysis.Should().HaveVariable("b").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Function);
        }

        [ServerTestMethod(Version = PythonLanguageVersion.V36), Priority(0)]
        public async Task ParameterTypesPropagateToDerivedFunctions(Server server) {
            var code = @"
class Baze:
  def foo(self, x):
    pass

class Derived(Baze):
  def foo(self, x):
    pass

Baze().foo(42)
";

            var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

            // the class, for which we know parameter type initially
            analysis.Should().HaveClass("Baze").WithFunction("foo")
                .WithParameter("x").OfType(BuiltinTypeId.Int);
            // its derived class
            analysis.Should().HaveClass("Derived").WithFunction("foo")
                .WithParameter("x").OfType(BuiltinTypeId.Int);
        }

        [TestMethod]
        public async Task ParameterTypesPropagateToBaseFunctions() {
            var code = @"
class Baze:
  def foo(self, x):
    pass

class Derived(Baze):
  def foo(self, x):
    pass

Derived().foo(42)
";

            using (var server = await new Server().InitializeAsync(PythonVersions.Required_Python36X)) {
                server.Analyzer.Limits.PropagateParameterTypeToBaseMethods = true;

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                // the class, for which we know parameter type initially
                analysis.Should().HaveClass("Derived").WithFunction("foo")
                    .WithParameter("x").OfType(BuiltinTypeId.Int);
                // its base class
                analysis.Should().HaveClass("Baze").WithFunction("foo")
                    .WithParameter("x").OfType(BuiltinTypeId.Int);
            }
        }
    }
}
