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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class BasicTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private string BaselineFileName => Path.ChangeExtension(Path.Combine(BaselineFilesFolder, TestContext.TestName), "json");

        [TestMethod, Priority(0)]
        public async Task SmokeTest() {
            const string code = @"
x = 'str'

class C:
    x: int
    def __init__(self):
        self.y = 1
        
    def method(self):
        return func()

    @property
    def prop(self) -> int:
        return x

def func():
    return 2.0

c = C()
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis, Services);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);
        }

        [TestMethod, Priority(0)]
        public async Task NestedClasses() {
            const string code = @"
x = 'str'

class A:
    def methodA(self):
        return True

class B:
    x: int

    class C:
        def __init__(self):
            self.y = 1
        def methodC(self):
            return False
        
    def methodB1(self):
        return C()

    def methodB2(self):
        return C().y

c = B().methodB1()
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis, Services);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);
        }

        [TestMethod, Priority(0)]
        public async Task Builtins() {
            var analysis = await GetAnalysisAsync(string.Empty);
            var builtins = analysis.Document.Interpreter.ModuleResolution.BuiltinsModule;
            var model = ModuleModel.FromAnalysis(builtins.Analysis, Services);

            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);

            var dbModule = new PythonDbModule(model, null, Services);
            dbModule.Should().HaveSameMembersAs(builtins);
        }


        [TestMethod, Priority(0)]
        public async Task Sys() {
            var analysis = await GetAnalysisAsync("import sys");
            var sys = analysis.Document.Interpreter.ModuleResolution.GetImportedModule("sys");
            var model = ModuleModel.FromAnalysis(sys.Analysis, Services);

            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);

            using (var dbModule = new PythonDbModule(model, sys.FilePath, Services)) {
                dbModule.Should().HaveSameMembersAs(sys);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Requests() {
            const string code = @"
import requests
x = requests.get('microsoft.com')
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var v = analysis.GlobalScope.Variables["requests"];
            v.Should().NotBeNull();
            if (v.Value.GetPythonType<IPythonModule>().ModuleType == ModuleType.Unresolved) {
                Assert.Inconclusive("'requests' package is not installed.");
            }

            var rq = analysis.Document.Interpreter.ModuleResolution.GetImportedModule("requests");
            var model = ModuleModel.FromAnalysis(rq.Analysis, Services);

            var u = model.UniqueId;
            u.Should().Contain("(").And.EndWith(")");
            var open = u.IndexOf('(');
            // Verify this looks like a version.
            new Version(u.Substring(open + 1, u.IndexOf(')') - open - 1));

            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);

            using (var dbModule = new PythonDbModule(model, rq.FilePath, Services)) {
                dbModule.Should().HaveSameMembersAs(rq);
            }
        }

        [DataTestMethod, Priority(0)]
        [DataRow("", null, null, false)]
        [DataRow("str", "builtins", "str", false)]
        [DataRow("i:str", "builtins", "str", true)]
        [DataRow("i:...", "builtins", "ellipsis", true)]
        [DataRow("ellipsis", "builtins", "ellipsis", false)]
        [DataRow("i:builtins:str", "builtins", "str", true)]
        [DataRow("i:mod:x", "mod", "x", true)]
        [DataRow("typing:Union[str, tuple]", "typing", "Union[str, tuple]", false)]
        [DataRow("typing:Union[typing:Any, mod:y]", "typing", "Union[typing:Any, mod:y]", false)]
        [DataRow("typing:Union[typing:Union[str, int], mod:y]", "typing", "Union[typing:Union[str, int], mod:y]", false)]
        public void QualifiedNames(string qualifiedName, string moduleName, string typeName, bool isInstance) {
            TypeNames.DeconstructQualifiedName(qualifiedName, out var actualModuleName, out var actualMemberNames, out var actualIsInstance);
            actualModuleName.Should().Be(moduleName);
            if (string.IsNullOrEmpty(qualifiedName)) {
                actualMemberNames.Should().BeNull();
            } else {
                actualMemberNames[0].Should().Be(typeName);
            }
            actualIsInstance.Should().Be(isInstance);
        }
    }
}
