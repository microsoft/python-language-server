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

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;
using Microsoft.Python.Analysis.Modules;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class ReferencesTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private string BaselineFileName => GetBaselineFileName(TestContext.TestName);

        [TestMethod, Priority(0)]
        public async Task MemberLocations() {
            const string code = @"
x = 'str'

def sum(a, b):
    return a + b

class B:
    x: int

    class C:
        def __init__(self):
            pass
        def methodC(self):
            pass

    @property
    def propertyB(self):
        return 1

    def methodB2(self):
        return 2
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis, Services);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);

            using (var dbModule = new PythonDbModule(model, analysis.Document.FilePath, Services)) {
                var sum = dbModule.GetMember("sum") as IPythonFunctionType;
                sum.Should().NotBeNull();
                sum.Definition.Span.Should().Be(4, 5, 4, 8);

                var b = dbModule.GetMember("B") as IPythonClassType;
                b.Should().NotBeNull();
                b.Definition.Span.Should().Be(7, 7, 7, 8);

                var c = b.GetMember("C") as IPythonClassType;
                c.Should().NotBeNull();
                c.Definition.Span.Should().Be(10, 11, 10, 12);

                var methodC = c.GetMember("methodC") as IPythonFunctionType;
                methodC.Should().NotBeNull();
                methodC.Definition.Span.Should().Be(13, 13, 13, 20);

                var propertyB = b.GetMember("propertyB") as IPythonPropertyType;
                propertyB.Should().NotBeNull();
                propertyB.Definition.Span.Should().Be(17, 9, 17, 18);

                var methodB2 = b.GetMember("methodB2") as IPythonFunctionType;
                methodB2.Should().NotBeNull();
                methodB2.Definition.Span.Should().Be(20, 9, 20, 17);
            }
        }

        [TestMethod, Priority(0)]
        public async Task Logging() {
            const string code = @"
import logging
logging.critical()
";
            var analysis = await GetAnalysisAsync(code);
            var logging = analysis.Document.Interpreter.ModuleResolution.GetImportedModule("logging");
            var model = ModuleModel.FromAnalysis(logging.Analysis, Services);

            var dbModule = new PythonDbModule(model, logging.FilePath, Services);
            analysis.Document.Interpreter.ModuleResolution.SpecializeModule("logging", x => dbModule, replaceExisting: true);
            
            var moduleName = $"{analysis.Document.Name}_db.py";
            var modulePath = TestData.GetTestSpecificPath(moduleName);
            analysis = await GetAnalysisAsync(code, Services, moduleName, modulePath);

            var v = analysis.Should().HaveVariable("logging").Which;
            var vm = v.Value.Should().BeOfType<PythonVariableModule>().Which;
            var m = vm.Module.Should().BeOfType<PythonDbModule>().Which;

            var critical = m.GetMember("critical") as IPythonFunctionType;
            critical.Should().NotBeNull();

            var span = critical.Definition.Span;
            span.Start.Line.Should().BeGreaterThan(1000);
            (span.End.Column - span.Start.Column).Should().Be("critical".Length);
        }
    }
}
