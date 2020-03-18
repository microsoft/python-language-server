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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class CoreTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private string BaselineFileName => GetBaselineFileName(TestContext.TestName);
        private string GetBaselineFileNameWithSuffix(string suffix) => GetBaselineFileName(TestContext.TestName, suffix);

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
            var model = await GetModelAsync(code);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);
        }

        [DataTestMethod, Priority(0)]
        [DataRow("t:str", "builtins", "str", ObjectType.Type)]
        [DataRow("i:str", "builtins", "str", ObjectType.Instance)]
        [DataRow("i:...", "builtins", "ellipsis", ObjectType.Instance)]
        [DataRow("t:ellipsis", "builtins", "ellipsis", ObjectType.Type)]
        [DataRow("i:builtins:str", "builtins", "str", ObjectType.Instance)]
        [DataRow("i:mod:x", "mod", "x", ObjectType.Instance)]
        [DataRow("t:typing:Union[str, tuple]", "typing", "Union[str, tuple]", ObjectType.Type)]
        [DataRow("t:typing:Union[typing:Any, mod:y]", "typing", "Union[typing:Any, mod:y]", ObjectType.Type)]
        [DataRow("t:typing:Union[typing:Union[str, int], mod:y]", "typing", "Union[typing:Union[str, int], mod:y]", ObjectType.Type)]
        [DataRow("m:typing", "typing", "", ObjectType.Module)]
        [DataRow("p:A", "A", "", ObjectType.VariableModule)]
        public void QualifiedNames(string qualifiedName, string moduleName, string typeName, ObjectType objectType) {
            TypeNames.DeconstructQualifiedName(qualifiedName, out var parts);
            parts.ModuleName.Should().Be(moduleName);
            switch (objectType) {
                case ObjectType.Instance:
                case ObjectType.Type:
                    parts.MemberNames[0].Should().Be(typeName);
                    break;
                default:
                    parts.MemberNames.Should().BeEmpty();
                    break;
            }
            parts.ObjectType.Should().Be(objectType);
        }

        [DataTestMethod, Priority(0)]
        [DataRow(true)]
        [DataRow(false)]
        public async Task VersionHandling(bool is3x) {
            const string code = @"
if sys.version_info >= (3, 0):
    def func(a, b, c): ...
else:
    def func(a): ...
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            analysis.Should().HaveFunction("func")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameters(is3x ? new[] { "a", "b", "c" } : new[] { "a" });

            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);
            model.FilePath = null;
            var json = ToJson(model);
            Baseline.CompareToFile(GetBaselineFileNameWithSuffix(is3x ? "3" : "2"), json);
        }

        [TestMethod, Priority(0)]
        public async Task PositionalOnly() {
            const string code = @"
x = 'str'

class C:
    x: int
    def __init__(self):
        self.y = 1

    def method(self, x, /, y=True):
        return func()

    @property
    def prop(self) -> int:
        return x

def func():
    return 2.0

c = C()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.Required_Python38X);
            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);

            using (var dbModule = CreateDbModule(model, analysis.Document.FilePath)) {
                dbModule.Should().HaveSameMembersAs(analysis.Document);
            }
        }
    }
}
