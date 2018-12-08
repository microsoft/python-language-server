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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Tests;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Ast = Microsoft.Python.Parsing.Ast;

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
        public async Task AstBasic() {
            var code = @"
x = 'str'
def func(a):
    return 1
";
            var interpreter = CreateInterpreter();
            var doc = Document.FromContent(interpreter, code, "module");

            var ast = await doc.GetAstAsync(CancellationToken.None);
            ast.Should().NotBeNull();

            var analyzer = new PythonAnalyzer(interpreter, null);
            var analysis = await analyzer.AnalyzeDocumentAsync(doc, CancellationToken.None);

            var analysisFromDoc = await doc.GetAnalysisAsync(CancellationToken.None);
            analysisFromDoc.Should().Be(analysis);

            var member = analysis.Members;
        }
        #endregion
    }
}
