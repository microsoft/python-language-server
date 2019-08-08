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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class LibraryModulesTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private string BaselineFileName => GetBaselineFileName(TestContext.TestName);

        [TestMethod, Priority(0)]
        [Ignore("Builtins module have custom member handling. We do not persist it yet.")]
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
        public Task Sys() => TestModule("sys");

        [TestMethod, Priority(0)]
        public Task Io() => TestModule("io");

        [TestMethod, Priority(0)]
        public Task Re() => TestModule("re");

        [TestMethod, Priority(0)]
        public Task Os() => TestModule("os");

        [TestMethod, Priority(0)]
        public Task Logging() => TestModule("logging");

        [TestMethod, Priority(0)]
        public Task Types() => TestModule("types");

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

            CompareBaselineAndRestore(model, rq);
        }

        private async Task TestModule(string name) {
            var analysis = await GetAnalysisAsync($"import {name}");
            var m = analysis.Document.Interpreter.ModuleResolution.GetImportedModule(name);
            var model = ModuleModel.FromAnalysis(m.Analysis, Services);

            CompareBaselineAndRestore(model, m);
        }

        private void CompareBaselineAndRestore(ModuleModel model, IPythonModule m) {
            //var json = ToJson(model);
            //Baseline.CompareToFile(BaselineFileName, json);

            using (var dbModule = new PythonDbModule(model, m.FilePath, Services)) {
                dbModule.Should().HaveSameMembersAs(m);
            }
        }
    }
}
