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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class SuperTypeTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private string BaselineFileName => GetBaselineFileName(TestContext.TestName);

        [TestMethod, Priority(0)]
        public async Task ClassesWithSuper() {
            const string code = @"
class A:
    def methodA(self):
        return True

class B(A):
    def methodB(self):
        return super()
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);

            using (var dbModule = CreateDbModule(model, analysis.Document.FilePath)) {
                dbModule.Should().HaveSameMembersAs(analysis.Document);
            }
        }

        [TestMethod, Priority(0)]
        public async Task GlobalSuper() {
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
            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);

            using (var dbModule = CreateDbModule(model, analysis.Document.FilePath)) {
                dbModule.Should().HaveSameMembersAs(analysis.Document);
            }
        }
    }
}
