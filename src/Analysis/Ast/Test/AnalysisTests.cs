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

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Tests;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class AnalysisTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() 
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        private readonly ServiceManager _sm = new ServiceManager();
        public AnalysisTests() {
            var platform = new OSPlatform();
            _sm
                .AddService(new TestLogger())
                .AddService(platform)
                .AddService(new FileSystem(platform));
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private AstPythonInterpreter CreateInterpreter() {
            var configuration = PythonVersions.LatestAvailable;
            configuration.AssertInstalled();
            Trace.TraceInformation("Cache Path: " + configuration.ModuleCachePath);
            configuration.ModuleCachePath = TestData.GetAstAnalysisCachePath(configuration.Version, true);
            return new AstPythonInterpreter(configuration, _sm);
        }

        #region Test cases
        [TestMethod, Priority(0)]
        public async Task SmokeTest() {
            const string code = @"
x = 'str'

class C:
    def method(self):
        return func()

def func():
    return 2.0

c = C()
y = c.method()
";
            var interpreter = CreateInterpreter();
            var doc = Document.FromContent(interpreter, code, "module");

            var ast = await doc.GetAstAsync(CancellationToken.None);
            ast.Should().NotBeNull();

            var analyzer = new PythonAnalyzer(interpreter, null);
            var analysis = await analyzer.AnalyzeDocumentAsync(doc, CancellationToken.None);

            var analysisFromDoc = await doc.GetAnalysisAsync(CancellationToken.None);
            analysisFromDoc.Should().Be(analysis);

            analysis.Members.Count.Should().Be(5);
            analysis.Members.Keys.Should().Contain("x", "C", "func", "c", "y");

            var t = analysis.Members["x"] as IPythonType;
            t.Should().NotBeNull();

            t?.TypeId.Should().Be(BuiltinTypeId.Unicode);
            analysis.Members["C"].MemberType.Should().Be(PythonMemberType.Class);
            (analysis.Members["func"] as IPythonFunction).Should().NotBeNull();

            t = analysis.Members["y"] as IPythonType;
            t?.TypeId.Should().Be(BuiltinTypeId.Float);
        }
        #endregion
    }
}
