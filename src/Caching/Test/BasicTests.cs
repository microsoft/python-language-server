﻿// Copyright(c) Microsoft Corporation
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

using System.IO;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
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

def func():
    return 2.0

c = C()
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);
        }

        [TestMethod, Priority(0)]
        public async Task Builtins() {
            var analysis = await GetAnalysisAsync(string.Empty);
            var builtins = analysis.Document.Interpreter.ModuleResolution.BuiltinsModule;
            var model = ModuleModel.FromAnalysis(builtins.Analysis);
            
            // Compare data to the baseline.
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);
            
            // Read persistent data back
            var dbModule = new PythonDbModule(model, Services);
            // Compare to the original members, should be identical.
            dbModule.Should().HaveSameMembersAs(builtins);
        }


        [TestMethod, Priority(0)]
        public async Task Sys() {
            var analysis = await GetAnalysisAsync("import sys");
            var sys = analysis.Document.Interpreter.ModuleResolution.GetImportedModule("sys");
            var model = ModuleModel.FromAnalysis(sys.Analysis);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);
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
            var model = ModuleModel.FromAnalysis(rq.Analysis);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);

             for var dbModule = new PythonDbModule(model, Services);
            dbModule.Should().HaveSameMembersAs(rq);
        }
    }
}
