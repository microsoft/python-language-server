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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Interpreter;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Ast = Microsoft.Python.Parsing.Ast;

namespace AnalysisTests {
    [TestClass]
    public class AstAnalysisTests: ServerBasedTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        private string _analysisLog;
        private string _moduleCache;

        public AstAnalysisTests() {
            _moduleCache = null;
        }

        [TestCleanup]
        public void Cleanup() {
            if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed) {
                if (_analysisLog != null) {
                    Console.WriteLine("Analysis log:");
                    Console.WriteLine(_analysisLog);
                }

                if (_moduleCache != null) {
                    Console.WriteLine("Module cache:");
                    Console.WriteLine(_moduleCache);
                }
            }

            TestEnvironmentImpl.TestCleanup();
        }

        private static AstPythonInterpreterFactory CreateInterpreterFactory(InterpreterConfiguration configuration) {
            configuration.AssertInstalled();
            var opts = new InterpreterFactoryCreationOptions {
                DatabasePath = TestData.GetAstAnalysisCachePath(configuration.Version, true),
                UseExistingCache = false
            };

            Trace.TraceInformation("Cache Path: " + opts.DatabasePath);

            return new AstPythonInterpreterFactory(configuration, opts);
        }

        private static Task<Server> CreateServerAsync(InterpreterConfiguration configuration = null, string searchPath = null)
            => new Server().InitializeAsync(
                configuration ?? PythonVersions.LatestAvailable,
                searchPaths: new[] { searchPath ?? TestData.GetPath(Path.Combine("TestData", "AstAnalysis")) });

        private static AstPythonInterpreterFactory CreateInterpreterFactory() => CreateInterpreterFactory(PythonVersions.LatestAvailable);


        #region Test cases

        [TestMethod, Priority(0)]
        public void AstClasses() {
            var mod = Parse("Classes.py", PythonLanguageVersion.V35);
            mod.GetMemberNames(null).Should().OnlyContain("C1", "C2", "C3", "C4", "C5",
                "D", "E",
                "F1",
                "f"
            );

            mod.GetMember(null, "C1").Should().BeOfType<AstPythonClass>()
                .Which.Documentation.Should().Be("C1");
            mod.GetMember(null, "C2").Should().BeOfType<AstPythonClass>();
            mod.GetMember(null, "C3").Should().BeOfType<AstPythonClass>();
            mod.GetMember(null, "C4").Should().BeOfType<AstPythonClass>();
            mod.GetMember(null, "C5").Should().BeOfType<AstPythonClass>()
                .Which.Documentation.Should().Be("C1");
            mod.GetMember(null, "D").Should().BeOfType<AstPythonClass>();
            mod.GetMember(null, "E").Should().BeOfType<AstPythonClass>();
            mod.GetMember(null, "f").Should().BeOfType<AstPythonFunction>();

            var f1 = mod.GetMember(null, "F1").Should().BeOfType<AstPythonClass>().Which;
            f1.GetMemberNames(null).Should().OnlyContain("F2", "F3", "F6", "__class__", "__bases__");
            f1.GetMember(null, "F6").Should().BeOfType<AstPythonClass>()
                .Which.Documentation.Should().Be("C1");
            f1.GetMember(null, "F2").Should().BeOfType<AstPythonClass>();
            f1.GetMember(null, "F3").Should().BeOfType<AstPythonClass>();
            f1.GetMember(null, "__class__").Should().BeOfType<AstPythonClass>();
            f1.GetMember(null, "__bases__").Should().BeOfType<AstPythonSequence>();
        }

        [TestMethod, Priority(0)]
        public void AstFunctions() {
            var mod = Parse("Functions.py", PythonLanguageVersion.V35);
            mod.GetMemberNames(null).Should().OnlyContain("f", "f2", "g", "h", "C");

            mod.GetMember(null, "f").Should().BeOfType<AstPythonFunction>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember(null, "f2").Should().BeOfType<AstPythonFunction>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember(null, "g").Should().BeOfType<AstPythonFunction>();
            mod.GetMember(null, "h").Should().BeOfType<AstPythonFunction>();

            var c = mod.GetMember(null, "C").Should().BeOfType<AstPythonClass>().Which;
            c.GetMemberNames(null).Should().OnlyContain("i", "j", "C2", "__class__", "__bases__");
            c.GetMember(null, "i").Should().BeOfType<AstPythonFunction>();
            c.GetMember(null, "j").Should().BeOfType<AstPythonFunction>();
            c.GetMember(null, "__class__").Should().BeOfType<AstPythonClass>();
            c.GetMember(null, "__bases__").Should().BeOfType<AstPythonSequence>();

            var c2 = c.GetMember(null, "C2").Should().BeOfType<AstPythonClass>().Which;
            c2.GetMemberNames(null).Should().OnlyContain("k", "__class__", "__bases__");
            c2.GetMember(null, "k").Should().BeOfType<AstPythonFunction>();
            c2.GetMember(null, "__class__").Should().BeOfType<AstPythonClass>();
            c2.GetMember(null, "__bases__").Should().BeOfType<AstPythonSequence>();
        }

        [TestMethod, Priority(0)]
        public async Task AstValues() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync("from Values import *");

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("z").OfTypes(BuiltinTypeId.Bytes)
                    .And.HaveVariable("pi").OfTypes(BuiltinTypeId.Float)
                    .And.HaveVariable("l").OfTypes(BuiltinTypeId.List)
                    .And.HaveVariable("t").OfTypes(BuiltinTypeId.Tuple)
                    .And.HaveVariable("d").OfTypes(BuiltinTypeId.Dict)
                    .And.HaveVariable("s").OfTypes(BuiltinTypeId.Set)
                    .And.HaveVariable("X").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("Y").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("Z").OfTypes(BuiltinTypeId.Bytes)
                    .And.HaveVariable("PI").OfTypes(BuiltinTypeId.Float)
                    .And.HaveVariable("L").OfTypes(BuiltinTypeId.List)
                    .And.HaveVariable("T").OfTypes(BuiltinTypeId.Tuple)
                    .And.HaveVariable("D").OfTypes(BuiltinTypeId.Dict)
                    .And.HaveVariable("S").OfTypes(BuiltinTypeId.Set);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstMultiValues() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync("from MultiValues import *");

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("z").OfTypes(BuiltinTypeId.Bytes)
                    .And.HaveVariable("l").OfTypes(BuiltinTypeId.List)
                    .And.HaveVariable("t").OfTypes(BuiltinTypeId.Tuple)
                    .And.HaveVariable("s").OfTypes(BuiltinTypeId.Set)
                    .And.HaveVariable("XY").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Str)
                    .And.HaveVariable("XYZ").OfTypes(BuiltinTypeId.Int, BuiltinTypeId.Str, BuiltinTypeId.Bytes)
                    .And.HaveVariable("D").OfTypes(BuiltinTypeId.List, BuiltinTypeId.Tuple, BuiltinTypeId.Dict, BuiltinTypeId.Set);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstForwardRefProperty1() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
from ForwardRefProp1 import *
x = B().getA().methodA()
");
                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstForwardRefGlobalFunction() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
from ForwardRefGlobalFunc import *
x = func1()
");
                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstForwardRefFunction1() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
from ForwardRefFunc1 import *
x = B().getA().methodA()
");
                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Str);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstForwardRefFunction2() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
from ForwardRefFunc2 import *
x = B().getA().methodA()
");
                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Str);
            }
        }

        [ServerTestMethod(Version = PythonLanguageVersion.V35), Priority(0)]
        public void AstImports(Server server) {
            var interpreter = server.Analyzer.Interpreter;
            var path = TestData.GetPath(Path.Combine("TestData", "AstAnalysis", "Imports.py"));

            var mod = PythonModuleLoader.FromFile(interpreter, path, server.Analyzer.LanguageVersion);
            mod.GetMemberNames(null).Should().OnlyContain("version_info", "a_made_up_module");
        }

        [TestMethod, Priority(0)]
        public async Task AstReturnTypes() {
            using (var server = await CreateServerAsync()) {
                var code = @"from ReturnValues import *
R_str = r_str()
R_object = r_object()
R_A1 = A()
R_A2 = A().r_A()
R_A3 = R_A1.r_A()";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveFunctionVariables("r_a", "r_b", "r_str", "r_object")
                    .And.HaveClassVariables("A")
                    .And.HaveVariable("R_str").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("R_object").OfTypes(BuiltinTypeId.Object)
                    .And.HaveVariable("R_A1").OfTypes("A").WithDescription("A")
                    .And.HaveVariable("R_A2").OfTypes("A").WithDescription("A")
                    .And.HaveVariable("R_A3").OfTypes("A").WithDescription("A");
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstInstanceMembers() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync("from InstanceMethod import f1, f2");

                analysis.Should()
                    .HaveVariable("f1").OfType(BuiltinTypeId.Function).WithValue<BuiltinFunctionInfo>().And
                    .HaveVariable("f2").OfType(BuiltinTypeId.Method).WithValue<BoundBuiltinMethodInfo>();
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstInstanceMembers_Random() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync("from random import *");

                foreach (var fnName in new[] { "seed", "randrange", "gauss" }) {
                    analysis.Should().HaveVariable(fnName)
                        .OfType(BuiltinTypeId.Method)
                        .WithValue<BoundBuiltinMethodInfo>()
                        .Which.Should().HaveOverloadWithParametersAt(0);
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstLibraryMembers_Datetime() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync("import datetime");

                var datetimeType = analysis.Should().HavePythonModuleVariable("datetime")
                    .Which.Should().HaveClass("datetime")
                    .Which;

                datetimeType.Should().HaveReadOnlyProperty("day").And.HaveMethod("now")
                    .Which.Should().BeClassMethod().And.HaveSingleOverload()
                    .Which.Should().HaveSingleReturnType()
                    .Which.Should().BeSameAs(datetimeType);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstComparisonTypeInference() {
            var code = @"
class BankAccount(object):
    def __init__(self, initial_balance=0):
        self.balance = initial_balance
    def withdraw(self, amount):
        self.balance -= amount
    def overdrawn(self):
        return self.balance < 0
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveClass("BankAccount")
                    .Which.Should().HaveVariable("overdrawn").WithValue<IFunctionInfo>()
                    .Which.Should().HaveOverloadAt(0)
                    .Which.Should().HaveSingleReturnType("bool");
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstSearchPathsThroughAnalyzer() {
            using (var factory = CreateInterpreterFactory())
            using (var analyzer = await PythonAnalyzer.CreateAsync(factory)) {
                try {
                    var interpreter = (AstPythonInterpreter)analyzer.Interpreter;

                    var moduleNamesChanged = EventTaskSources.AstPythonInterpreter.ModuleNamesChanged.Create(interpreter, new CancellationTokenSource(1000).Token);
                    analyzer.SetSearchPaths(new[] { TestData.GetPath(Path.Combine("TestData", "AstAnalysis")) });
                    await moduleNamesChanged;

                    interpreter.GetModuleNames().Should().Contain("Values");
                    interpreter.ImportModule("Values").Should().NotBeNull("module should be available");

                    moduleNamesChanged = EventTaskSources.AstPythonInterpreter.ModuleNamesChanged.Create(interpreter, new CancellationTokenSource(1000).Token);
                    analyzer.SetSearchPaths(Enumerable.Empty<string>());
                    await moduleNamesChanged;

                    interpreter.GetModuleNames().Should().NotContain("Values");
                    interpreter.ImportModule("Values").Should().BeNull("module should be removed");
                } finally {
                    _analysisLog = factory.GetAnalysisLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstTypeStubPaths_NoStubs() {
            using (var server = await CreateServerAsync()) {
                var analysis = await GetStubBasedAnalysis(
                    server,
                    "import Package.Module\n\nc = Package.Module.Class()",
                    new AnalysisLimits { UseTypeStubPackages = false },
                    searchPaths: new[] { TestData.GetPath(Path.Combine("TestData", "AstAnalysis")) },
                    stubPaths: Enumerable.Empty<string>());

                analysis.Should().HavePythonPackageVariable("Package")
                    .Which.Should().HaveChildModule("Module");

                analysis.Should().HaveVariable("c")
                    .WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveMembers("untyped_method", "inferred_method")
                    .And.NotHaveMembers("typed_method", "typed_method_2");
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstTypeStubPaths_MergeStubs() {
            using (var server = await CreateServerAsync()) {
                var analysis = await GetStubBasedAnalysis(server,
                    "import Package.Module\n\nc = Package.Module.Class()",
                    new AnalysisLimits {
                        UseTypeStubPackages = true,
                        UseTypeStubPackagesExclusively = false
                    },
                    searchPaths: new[] { TestData.GetPath(Path.Combine("TestData", "AstAnalysis")) },
                    stubPaths: Enumerable.Empty<string>());

                analysis.Should().HavePythonPackageVariable("Package")
                    .Which.Should().HaveChildModule("Module");

                analysis.Should().HaveVariable("c")
                    .WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveMembers("untyped_method", "inferred_method", "typed_method")
                    .And.NotHaveMembers("typed_method_2");
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstTypeStubPaths_MergeStubsPath() {
            using (var server = await CreateServerAsync()) {
                var analysis = await GetStubBasedAnalysis(
                    server,
                    "import Package.Module\n\nc = Package.Module.Class()",
                    null,
                    searchPaths: new[] { TestData.GetPath(Path.Combine("TestData", "AstAnalysis")) },
                    stubPaths: new[] { TestData.GetPath(Path.Combine("TestData", "AstAnalysis", "Stubs")) });

                analysis.Should().HavePythonPackageVariable("Package")
                    .Which.Should().HaveChildModule("Module"); // member information comes from multiple sources

                analysis.Should().HaveVariable("c")
                    .WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveMembers("untyped_method", "inferred_method", "typed_method_2")
                    .And.NotHaveMembers("typed_method");
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstTypeStubPaths_ExclusiveStubs() {
            using (var server = await CreateServerAsync()) {
                var analysis = await GetStubBasedAnalysis(
                    server,
                    "import Package.Module\n\nc = Package.Module.Class()",
                    new AnalysisLimits {
                        UseTypeStubPackages = true,
                        UseTypeStubPackagesExclusively = true
                    },
                    searchPaths: new[] { TestData.GetPath(Path.Combine("TestData", "AstAnalysis")) },
                    stubPaths: new[] { TestData.GetPath(Path.Combine("TestData", "AstAnalysis", "Stubs")) });

                analysis.Should().HavePythonPackageVariable("Package")
                    .Which.Should().HaveChildModule("Module");

                analysis.Should().HaveVariable("c")
                    .WithValue<IBuiltinInstanceInfo>()
                    .Which.Should().HaveMembers("typed_method_2")
                    .And.NotHaveMembers("untyped_method", "inferred_method", "typed_method");
            }
        }

        [TestMethod, Priority(0)]
        public void AstMro() {
            var O = new AstPythonClass("O");
            var A = new AstPythonClass("A");
            var B = new AstPythonClass("B");
            var C = new AstPythonClass("C");
            var D = new AstPythonClass("D");
            var E = new AstPythonClass("E");
            var F = new AstPythonClass("F");

            F.SetBases(null, new[] { O });
            E.SetBases(null, new[] { O });
            D.SetBases(null, new[] { O });
            C.SetBases(null, new[] { D, F });
            B.SetBases(null, new[] { D, E });
            A.SetBases(null, new[] { B, C });

            AstPythonClass.CalculateMro(A).Should().Equal(new[] { "A", "B", "C", "D", "E", "F", "O" }, (p, n) => p.Name == n);
            AstPythonClass.CalculateMro(B).Should().Equal(new[] { "B", "D", "E", "O" }, (p, n) => p.Name == n);
            AstPythonClass.CalculateMro(C).Should().Equal(new[] { "C", "D", "F", "O" }, (p, n) => p.Name == n);
        }

        private static IPythonModule Parse(string path, PythonLanguageVersion version) {
            var interpreter = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion()).CreateInterpreter();
            if (!Path.IsPathRooted(path)) {
                path = TestData.GetPath(Path.Combine("TestData", "AstAnalysis", path));
            }
            return PythonModuleLoader.FromFile(interpreter, path, version);
        }

        [TestMethod, Priority(0)]
        public async Task ScrapedTypeWithWrongModule() {
            var version = PythonVersions.Versions
                .Concat(PythonVersions.AnacondaVersions)
                .LastOrDefault(v => Directory.Exists(Path.Combine(v.SitePackagesPath, "numpy")));
            version.AssertInstalled();

            Console.WriteLine("Using {0}", version.InterpreterPath);
            using (var server = await CreateServerAsync(version)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync("import numpy.core.numeric as NP; ndarray = NP.ndarray");
                                                                        //1234567890123456789012345678901234
                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var hover = await server.SendHover(uri, 0, 34);
                hover.Should().HaveTypeName("numpy.core.multiarray.ndarray");
            }
        }

        [TestMethod, Priority(0)]
        public async Task ScrapedSpecialFloats() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync("import math; inf = math.inf; nan = math.nan");

                analysis.Should().HaveVariable("math")
                    .And.HaveVariable("inf").OfType(BuiltinTypeId.Float).WithValue<NumericInstanceInfo>()
                    .And.HaveVariable("nan").OfType(BuiltinTypeId.Float).WithValue<NumericInstanceInfo>();
            }
        }

        #endregion

        #region Black-box sanity tests
        // "Do we crash?"

        [TestMethod, Priority(0)]
        public async Task AstBuiltinScrapeV37() => await AstBuiltinScrape(PythonVersions.Python37_x64 ?? PythonVersions.Python37);

        [TestMethod, Priority(0)]
        public async Task AstBuiltinScrapeV36() => await AstBuiltinScrape(PythonVersions.Python36_x64 ?? PythonVersions.Python36);

        [TestMethod, Priority(0)]
        public async Task AstBuiltinScrapeV35() => await AstBuiltinScrape(PythonVersions.Python35_x64 ?? PythonVersions.Python35);

        [TestMethod, Priority(0)]
        public async Task AstBuiltinScrapeV27() => await AstBuiltinScrape(PythonVersions.Python27_x64 ?? PythonVersions.Python27);

        [TestMethod, Priority(0)]
        public async Task AstBuiltinScrapeIPy27() => await AstBuiltinScrape(PythonVersions.IronPython27_x64 ?? PythonVersions.IronPython27);


        private async Task AstBuiltinScrape(InterpreterConfiguration configuration) {
            AstScrapedPythonModule.KeepAst = true;
            configuration.AssertInstalled();
            using (var factory = CreateInterpreterFactory(configuration))
            using (var analyzer = await PythonAnalyzer.CreateAsync(factory)) {
                try {
                    var interp = (AstPythonInterpreter)analyzer.Interpreter;
                    var ctxt = interp.CreateModuleContext();

                    var mod = await interp.ImportModuleAsync(interp.BuiltinModuleName, new CancellationTokenSource(5000).Token);
                    Assert.IsInstanceOfType(mod, typeof(AstBuiltinsPythonModule));
                    mod.Imported(ctxt);

                    var modPath = interp.ModuleCache.GetCacheFilePath(factory.Configuration.InterpreterPath);
                    if (File.Exists(modPath)) {
                        _moduleCache = File.ReadAllText(modPath);
                    }

                    var errors = ((AstScrapedPythonModule)mod).ParseErrors ?? Enumerable.Empty<string>();
                    foreach (var err in errors) {
                        Console.WriteLine(err);
                    }
                    Assert.AreEqual(0, errors.Count(), "Parse errors occurred");

                    var seen = new HashSet<string>();
                    foreach (var stmt in ((Ast.SuiteStatement)((AstScrapedPythonModule)mod).Ast.Body).Statements) {
                        if (stmt is Ast.ClassDefinition cd) {
                            Assert.IsTrue(seen.Add(cd.Name), $"Repeated use of {cd.Name} at index {cd.StartIndex} in {modPath}");
                        } else if (stmt is Ast.FunctionDefinition fd) {
                            Assert.IsTrue(seen.Add(fd.Name), $"Repeated use of {fd.Name} at index {fd.StartIndex} in {modPath}");
                        } else if (stmt is Ast.AssignmentStatement assign && assign.Left.FirstOrDefault() is Ast.NameExpression n) {
                            Assert.IsTrue(seen.Add(n.Name), $"Repeated use of {n.Name} at index {n.StartIndex} in {modPath}");
                        }
                    }

                    // Ensure we can get all the builtin types
                    foreach (BuiltinTypeId v in Enum.GetValues(typeof(BuiltinTypeId))) {
                        var type = interp.GetBuiltinType(v);
                        type.Should().NotBeNull().And.BeAssignableTo<IPythonType>($"Did not find {v}");
                        type.IsBuiltin.Should().BeTrue();
                    }

                    // Ensure we cannot see or get builtin types directly
                    mod.GetMemberNames(null).Should().NotContain(Enum.GetNames(typeof(BuiltinTypeId)).Select(n => $"__{n}"));

                    foreach (var id in Enum.GetNames(typeof(BuiltinTypeId))) {
                        mod.GetMember(null, $"__{id}").Should().BeNull(id);
                    }
                } finally {
                    AstScrapedPythonModule.KeepAst = false;
                    _analysisLog = factory.GetAnalysisLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV38x64() => await AstNativeBuiltinScrape(PythonVersions.Python38_x64);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV37x64() => await AstNativeBuiltinScrape(PythonVersions.Python37_x64);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV36x64() => await AstNativeBuiltinScrape(PythonVersions.Python36_x64);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV35x64() => await AstNativeBuiltinScrape(PythonVersions.Python35_x64);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV27x64() => await AstNativeBuiltinScrape(PythonVersions.Python27_x64);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV38x86() => await AstNativeBuiltinScrape(PythonVersions.Python38);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV37x86() => await AstNativeBuiltinScrape(PythonVersions.Python37);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV36x86() => await AstNativeBuiltinScrape(PythonVersions.Python36);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV35x86() => await AstNativeBuiltinScrape(PythonVersions.Python35);

        [TestMethod, Priority(0)]
        public async Task AstNativeBuiltinScrapeV27x86() => await AstNativeBuiltinScrape(PythonVersions.Python27);

        private async Task AstNativeBuiltinScrape(InterpreterConfiguration configuration) {
            configuration.AssertInstalled();
            AstScrapedPythonModule.KeepAst = true;
            using (var factory = CreateInterpreterFactory(configuration))
            using (var analyzer = await PythonAnalyzer.CreateAsync(factory)) {
                try {
                    var interpreter = (AstPythonInterpreter)analyzer.Interpreter;
                    var ctxt = interpreter.CreateModuleContext();
                    // TODO: this is Windows only
                    var dllsDir = PathUtils.GetAbsoluteDirectoryPath(Path.GetDirectoryName(factory.Configuration.LibraryPath), "DLLs");
                    if (!Directory.Exists(dllsDir)) {
                        Assert.Inconclusive("Configuration does not have DLLs");
                    }

                    var report = new List<string>();
                    var permittedImports = factory.GetLanguageVersion().Is2x() ?
                        new[] { interpreter.BuiltinModuleName, "exceptions" } :
                        new[] { interpreter.BuiltinModuleName };

                    foreach (var pyd in Microsoft.Python.Core.IO.PathUtils.EnumerateFiles(dllsDir, "*", recurse: false).Where(ModulePath.IsPythonFile)) {
                        var mp = ModulePath.FromFullPath(pyd);
                        if (mp.IsDebug) {
                            continue;
                        }

                        Console.WriteLine("Importing {0} from {1}", mp.ModuleName, mp.SourceFile);
                        var mod = interpreter.ImportModule(mp.ModuleName);
                        Assert.IsInstanceOfType(mod, typeof(AstScrapedPythonModule));
                        mod.Imported(ctxt);

                        var modPath = interpreter.ModuleCache.GetCacheFilePath(pyd);
                        Assert.IsTrue(File.Exists(modPath), "No cache file created");
                        _moduleCache = File.ReadAllText(modPath);

                        var errors = ((AstScrapedPythonModule)mod).ParseErrors ?? Enumerable.Empty<string>();
                        foreach (var err in errors) {
                            Console.WriteLine(err);
                        }
                        Assert.AreEqual(0, errors.Count(), "Parse errors occurred");

                        var ast = ((AstScrapedPythonModule)mod).Ast;


                        var imports = ((Ast.SuiteStatement)ast.Body).Statements
                            .OfType<Ast.ImportStatement>()
                            .SelectMany(s => s.Names)
                            .Select(n => n.MakeString())
                            .Except(permittedImports)
                            .ToArray();

                        // We expect no imports (after excluding builtins)
                        report.AddRange(imports.Select(n => $"{mp.ModuleName} imported {n}"));

                        _moduleCache = null;
                    }

                    report.Should().BeEmpty();
                } finally {
                    AstScrapedPythonModule.KeepAst = false;
                    _analysisLog = factory.GetAnalysisLogContent(CultureInfo.InvariantCulture);
                }
            }
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV38() {
            var v = PythonVersions.Python38 ?? PythonVersions.Python38_x64;
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV37() {
            var v = PythonVersions.Python37 ?? PythonVersions.Python37_x64;
            await FullStdLibTest(v);
        }


        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV36() {
            var v = PythonVersions.Python36 ?? PythonVersions.Python36_x64;
            await FullStdLibTest(v);
        }


        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV35() {
            var v = PythonVersions.Python35 ?? PythonVersions.Python35_x64;
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV27() {
            var v = PythonVersions.Python27 ?? PythonVersions.Python27_x64;
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(1)]
        [Timeout(10 * 60 * 1000)]
        public async Task FullStdLibAnaconda3() {
            var v = PythonVersions.Anaconda36_x64 ?? PythonVersions.Anaconda36;
            await FullStdLibTest(v,
                // Crashes Python on import
                "sklearn.linear_model.cd_fast",
                // Crashes Python on import
                "sklearn.cluster._k_means_elkan"
            );
        }

        [TestMethod, TestCategory("60s"), Priority(1)]
        [Timeout(10 * 60 * 1000)]
        public async Task FullStdLibAnaconda2() {
            var v = PythonVersions.Anaconda27_x64 ?? PythonVersions.Anaconda27;
            await FullStdLibTest(v,
                // Fails to import due to SxS manifest issues
                "dde",
                "win32ui"
            );
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibIPy27() {
            var v = PythonVersions.IronPython27 ?? PythonVersions.IronPython27_x64;
            await FullStdLibTest(v);
        }


        private async Task FullStdLibTest(InterpreterConfiguration configuration, params string[] skipModules) {
            configuration.AssertInstalled();
            var factory = new AstPythonInterpreterFactory(configuration, new InterpreterFactoryCreationOptions {
                DatabasePath = TestData.GetAstAnalysisCachePath(configuration.Version, true),
                UseExistingCache = false
            });

            var modules = ModulePath.GetModulesInLib(configuration.LibraryPath, configuration.SitePackagesPath).ToList();

            var skip = new HashSet<string>(skipModules);
            skip.UnionWith(new[] {
                "matplotlib.backends._backend_gdk",
                "matplotlib.backends._backend_gtkagg",
                "matplotlib.backends._gtkagg",
                "test.test_pep3131",
                "test.test_unicode_identifiers"
            });
            skip.UnionWith(modules.Select(m => m.FullName).Where(n => n.StartsWith("test.badsyntax") || n.StartsWith("test.bad_coding")));

            var anySuccess = false;
            var anyExtensionSuccess = false;
            var anyExtensionSeen = false;
            var anyParseError = false;

            using (var analyzer = await PythonAnalyzer.CreateAsync(factory)) {
                try {
                    PythonModuleLoader.KeepParseErrors = true;
                    var tasks = new List<Task<Tuple<ModulePath, IPythonModule>>>();
                    var interp = (AstPythonInterpreter)analyzer.Interpreter;
                    foreach (var m in skip) {
                        interp.AddUnimportableModule(m);
                    }

                    foreach (var r in modules
                        .Where(m => !skip.Contains(m.ModuleName))
                        .GroupBy(m => {
                            var i = m.FullName.IndexOf('.');
                            return i <= 0 ? m.FullName : m.FullName.Remove(i);
                        })
                        .AsParallel()
                        .SelectMany(g => g.Select(m => Tuple.Create(m, interp.ImportModule(m.ModuleName))))
                    ) {
                        var modName = r.Item1;
                        var mod = r.Item2;

                        anyExtensionSeen |= modName.IsNativeExtension;
                        if (mod == null) {
                            Trace.TraceWarning("failed to import {0} from {1}", modName.ModuleName, modName.SourceFile);
                        } else if (mod is AstScrapedPythonModule aspm) {
                            var errors = aspm.ParseErrors.ToArray();
                            if (errors.Any()) {
                                anyParseError = true;
                                Trace.TraceError("Parse errors in {0}", modName.SourceFile);
                                foreach (var e in errors) {
                                    Trace.TraceError(e);
                                }
                            } else {
                                anySuccess = true;
                                anyExtensionSuccess |= modName.IsNativeExtension;
                                mod.GetMemberNames(null).ToList();
                            }
                        } else if (mod is AstPythonModule apm) {
                            var filteredErrors = apm.ParseErrors.Where(e => !e.Contains("encoding problem")).ToArray();
                            if (filteredErrors.Any()) {
                                // Do not fail due to errors in installed packages
                                if (!apm.FilePath.Contains("site-packages")) {
                                    anyParseError = true;
                                }
                                Trace.TraceError("Parse errors in {0}", modName.SourceFile);
                                foreach (var e in filteredErrors) {
                                    Trace.TraceError(e);
                                }
                            } else {
                                anySuccess = true;
                                anyExtensionSuccess |= modName.IsNativeExtension;
                                mod.GetMemberNames(null).ToList();
                            }
                        } else {
                            Trace.TraceError("imported {0} as type {1}", modName.ModuleName, mod.GetType().FullName);
                        }
                    }
                } finally {
                    _analysisLog = factory.GetAnalysisLogContent(CultureInfo.InvariantCulture);
                    PythonModuleLoader.KeepParseErrors = false;
                }
            }
            Assert.IsTrue(anySuccess, "failed to import any modules at all");
            Assert.IsTrue(anyExtensionSuccess || !anyExtensionSeen, "failed to import all extension modules");
            Assert.IsFalse(anyParseError, "parse errors occurred");
        }

        #endregion
        #region Type Annotation tests
        [TestMethod, Priority(0)]
        public async Task AstTypeAnnotationConversion() {
            using (var server = await CreateServerAsync()) {
                var code = @"from ReturnAnnotations import *
x = f()
y = g()";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("x").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("f").WithValue<BuiltinFunctionInfo>()
                    .Which.Should().HaveSingleOverload()
                    .Which.Should().HaveSingleParameter()
                    .Which.Should().HaveName("p").And.HaveType("int").And.HaveNoDefaultValue();
            }
        }
        #endregion

        #region Typeshed tests

        [TestMethod, Priority(0)]
        public async Task TypeShedElementTree() {
            using (var server = await CreateServerAsync()) {
                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                var code = @"import xml.etree.ElementTree as ET

e = ET.Element()
e2 = e.makeelement()
iterfind = e.iterfind
l = iterfind()";
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
                var analysis = await server.GetAnalysisAsync(uri);
                var elementSignatures = await server.SendSignatureHelp(uri, 2, 15);
                var makeelementSignatures = await server.SendSignatureHelp(uri, 3, 19);
                var iterfindSignatures = await server.SendSignatureHelp(uri, 5, 13);

                analysis.Should().HaveVariable("l").OfTypes(BuiltinTypeId.List);
                elementSignatures.Should().HaveSingleSignature()
                    .Which.Should().OnlyHaveParameterLabels("tag", "attrib", "**extra");
                makeelementSignatures.Should().HaveSingleSignature()
                    .Which.Should().OnlyHaveParameterLabels("tag", "attrib");
                iterfindSignatures.Should().HaveSingleSignature()
                    .Which.Should().OnlyHaveParameterLabels("path", "namespaces");
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeShedChildModules() {
            string[] firstMembers;

            using (var server = await CreateServerAsync()) {
                server.Analyzer.Limits = new AnalysisLimits { UseTypeStubPackages = false };
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"import urllib");

                firstMembers = server.Analyzer.GetModuleMembers(analysis.InterpreterContext, new[] { "urllib" })
                    .Select(m => m.Name)
                    .ToArray();

                firstMembers.Should().NotBeEmpty().And.Contain(new[] { "parse", "request" });
            }

            using (var server = await CreateServerAsync()) {
                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"import urllib");

                var secondMembers = server.Analyzer.GetModuleMembers(analysis.InterpreterContext, new[] { "urllib" })
                    .Select(m => m.Name)
                    .ToArray();

                secondMembers.Should().OnlyContain(firstMembers);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeShedSysExcInfo() {
            using (var server = await CreateServerAsync()) {
                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                var code = @"import sys

e1, e2, e3 = sys.exc_info()";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                // sys.exc_info() -> (exception_type, exception_value, traceback)
                analysis.Should().HaveVariable("e1").OfTypes(BuiltinTypeId.Type)
                    .And.HaveVariable("e2").OfTypes("BaseException")
                    .And.HaveVariable("e3").OfTypes(BuiltinTypeId.Unknown)
                    .And.HaveVariable("sys").WithValue<BuiltinModule>()
                    .Which.Should().HaveMember<BuiltinFunctionInfo>("exc_info")
                    .Which.Should().HaveSingleOverload()
                    .WithSingleReturnType("tuple[type, BaseException, Unknown]");
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeShedJsonMakeScanner() {
            using (var server = await CreateServerAsync()) {
                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                var code = @"import _json

scanner = _json.make_scanner()";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                var v0 = analysis.Should().HaveVariable("scanner").WithValueAt<IBuiltinInstanceInfo>(0);

                  v0.Which.Should().HaveSingleOverload()
                    .Which.Should().HaveName("__call__")
                    .And.HaveParameters("string", "index")
                    .And.HaveParameterAt(0).WithName("string").WithType("str").WithNoDefaultValue()
                    .And.HaveParameterAt(1).WithName("index").WithType("int").WithNoDefaultValue()
                    .And.HaveSingleReturnType("tuple[object, int]");
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeShedSysInfo() {
            using (var server = await CreateServerAsync()) {
                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                server.Analyzer.Limits = new AnalysisLimits { UseTypeStubPackages = true, UseTypeStubPackagesExclusively = true };

                var code = @"import sys

l_1 = sys.argv

s_1 = sys.argv[0]
s_2 = next(iter(sys.argv))
s_3 = sys.stdout.encoding

f_1 = sys.stdout.write
f_2 = sys.__stdin__.read

i_1 = sys.flags.debug
i_2 = sys.flags.quiet
i_3 = sys.implementation.version.major
i_4 = sys.getsizeof(None)
i_5 = sys.getwindowsversion().platform_version[0]
";
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis.Should().HaveVariable("l_1").OfTypes(BuiltinTypeId.List)
                    .And.HaveVariable("s_1").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("s_2").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("s_3").OfTypes(BuiltinTypeId.Str)
                    .And.HaveVariable("f_1").OfTypes(BuiltinTypeId.Method)
                    .And.HaveVariable("f_2").OfTypes(BuiltinTypeId.Method)
                    .And.HaveVariable("i_1").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("i_2").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("i_3").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("i_4").OfTypes(BuiltinTypeId.Int)
                    .And.HaveVariable("i_5").OfTypes(BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstReturnAnnotationList() {
            using (var server = await CreateServerAsync()) {
                var code = @"
from typing import List

def ls() -> List[tuple]:
    pass

x = ls()[0]
";

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                analysis
                    .Should().HaveVariable("ls").Which
                    .Should().HaveShortDescriptions("module.ls() -> list[tuple]");

                analysis
                    .Should().HaveVariable("x").Which
                    .Should().HaveType(BuiltinTypeId.Tuple);
            }
        }

        [TestMethod, Priority(0)]
        public async Task AstNamedTupleReturnAnnotation() {
            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(@"
from ReturnAnnotation import *
nt = namedtuple('Point', ['x', 'y'])
pt = nt(1, 2)
");
                analysis.Should().HaveVariable("pt").OfTypes(BuiltinTypeId.Tuple);
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeShedNamedTuple() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                server.Analyzer.SetTypeStubPaths(new[] { GetTypeshedPath() });
                server.Analyzer.Limits = new AnalysisLimits { UseTypeStubPackages = true, UseTypeStubPackagesExclusively = true };
                var code = "from collections import namedtuple\n";

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(code);
                analysis
                    .Should().HaveBuiltInFunctionInfo("namedtuple").Which
                    .Should().HaveSingleOverload().Which
                    .Should().HaveSingleReturnType("tuple");
            }
        }

        [TestMethod, Priority(0)]
        public void TypeStubConditionalDefine() {
            var seen = new HashSet<Version>();

            var code = @"import sys

if sys.version_info < (2, 7):
    LT_2_7 : bool = ...
if sys.version_info <= (2, 7):
    LE_2_7 : bool = ...
if sys.version_info > (2, 7):
    GT_2_7 : bool = ...
if sys.version_info >= (2, 7):
    GE_2_7 : bool = ...

";

            var fullSet = new[] { "LT_2_7", "LE_2_7", "GT_2_7", "GE_2_7" };

            foreach (var ver in PythonVersions.Versions) {
                if (!seen.Add(ver.Version)) {
                    continue;
                }

                Console.WriteLine("Testing with {0}", ver.InterpreterPath);

                var interpreter = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(ver.Version).CreateInterpreter();
                var entry = PythonModuleLoader.FromStream(interpreter, new MemoryStream(Encoding.ASCII.GetBytes(code)), TestData.GetTestSpecificPath("testmodule.pyi"), ver.Version.ToLanguageVersion(), null);

                var expected = new List<string>();
                var pythonVersion = ver.Version.ToLanguageVersion();
                if (pythonVersion.Is3x()) {
                    expected.Add("GT_2_7");
                    expected.Add("GE_2_7");
                } else if (pythonVersion == PythonLanguageVersion.V27) {
                    expected.Add("GE_2_7");
                    expected.Add("LE_2_7");
                } else {
                    expected.Add("LT_2_7");
                    expected.Add("LE_2_7");
                }

                entry.GetMemberNames(null).Where(n => n.EndsWithOrdinal("2_7"))
                    .Should().Contain(expected)
                    .And.NotContain(fullSet.Except(expected));
            }
        }
        #endregion
    }
}
