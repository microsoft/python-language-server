// Python Tools for Visual Studio
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
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class FunctionBodyAnalysisTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ParameterInfoSet() {
            var code = @"def f(a, b):
    pass

f(1, 3.14)
a = 7.28
b = 3.14
";
            using (var server = await new Server().InitializeAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunction("f")
                    .Which.Should()
                        .HaveParameter("a").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("a").WithValue<ParameterInfo>()
                    .And.HaveParameter("b").OfTypes(BuiltinTypeId.Float)
                    .And.HaveVariable("b").WithValue<ParameterInfo>();
            }
        }

        [TestMethod, Priority(0)]
        public async Task ParameterInfoReturnValue() {
            var code = @"def f(a, b):
    return a

r_a = f(1, 3.14)
r_b = f(b=1, a=3.14)
r_a = f(1, 3.14)
";
            using (var server = await new Server().InitializeAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should()
                        .HaveVariable("r_a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("r_b").OfType(BuiltinTypeId.Float)
                    .And.HaveFunction("f")
                    .Which.Should()
                        .HaveParameter("a").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveParameter("b").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveResolvedReturnTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveReturnValue().WithValue<ParameterInfo>()
                    .Which.Should().HaveName("a");

                // Unevaluated calls return all types
                analysis.GetValues("f()", SourceLocation.MinValue).Select(av => av.TypeId)
                    .Should().OnlyContain(BuiltinTypeId.Int, BuiltinTypeId.Float);
                analysis.GetValues("f(1)", SourceLocation.MinValue).Select(av => av.TypeId)
                    .Should().OnlyContain(BuiltinTypeId.Int, BuiltinTypeId.Float);

            }
        }

        [TestMethod, Priority(0)]
        public async Task ChainedParameterInfoReturnValue() {
            var code = @"def f(a, b):
    return a

def g(x, y):
    return f(x, y)

r_a = g(1, 3.14)
r_b = g(y=1, x=3.14)
r_a = g(1, 3.14)
";
            using (var server = await new Server().InitializeAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should()
                        .HaveVariable("r_a").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("r_b").OfType(BuiltinTypeId.Float)
                    .And.HaveFunction("f")
                    .Which.Should()
                        .HaveParameter("a").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveParameter("b").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveResolvedReturnTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveReturnValue().WithValue<ParameterInfo>()
                    .Which.Should().HaveName("a");

                analysis.Should().HaveFunction("g")
                    .Which.Should()
                        .HaveParameter("x").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveParameter("y").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveResolvedReturnTypes(BuiltinTypeId.Int, BuiltinTypeId.Float)
                    .And.HaveReturnValue().WithValue<ParameterInfo>()
                    .Which.Should().HaveName("x");

                // Unevaluated calls return all types
                analysis.GetValues("f()", SourceLocation.MinValue).Select(av => av.TypeId)
                    .Should().OnlyContain(BuiltinTypeId.Int, BuiltinTypeId.Float);
                analysis.GetValues("g()", SourceLocation.MinValue).Select(av => av.TypeId)
                    .Should().OnlyContain(BuiltinTypeId.Int, BuiltinTypeId.Float);
                analysis.GetValues("g(1)", SourceLocation.MinValue).Select(av => av.TypeId)
                    .Should().OnlyContain(BuiltinTypeId.Int, BuiltinTypeId.Float);
            }
        }

        [TestMethod, Priority(0)]
        public async Task LazyMemberOnParameter() {
            var code = @"class C:
    x = 123
class D:
    x = 3.14

def f(v):
    return v.x

c = f(C())
d = f(D())";

            using (var server = await new Server().InitializeAsync(PythonVersions.LatestAvailable3X)) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("c").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("d").OfType(BuiltinTypeId.Float)
                    .And.HaveFunction("f")
                    .Which.Should().HaveParameter("v").OfTypes("C", "D");
            }
        }
    }
}
