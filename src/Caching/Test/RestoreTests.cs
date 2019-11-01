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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class RestoreTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ReturnType() {
            const string code = @"
from module2 import func2
x = func2()
";
            const string mod2Code = @"
class C2:
    def M1C2(self):
        return 0

def func2() -> C2: ...
";
            await TestData.CreateTestSpecificFileAsync("module2.py", mod2Code);
            var analysis = await GetAnalysisAsync(code);

            var func2 = analysis.Should().HaveVariable("func2").Which;
            var analysis2 = ((IPythonFunctionType)func2.Value).DeclaringModule.Analysis;

            var dbs = new ModuleDatabase(Services, Path.GetDirectoryName(TestData.GetDefaultModulePath()));
            Services.AddService(dbs);
            await dbs.StoreModuleAnalysisAsync(analysis2, CancellationToken.None);

            await Services.GetService<IPythonAnalyzer>().ResetAnalyzer();
            var doc = Services.GetService<IRunningDocumentTable>().GetDocument(analysis.Document.Uri);
            analysis = await doc.GetAnalysisAsync(Timeout.Infinite);

            analysis.Should().HaveFunction("func2")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnType("C2")
                .Which.Should().HaveMember<IPythonFunctionType>("M1C2")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveReturnType(BuiltinTypeId.Int);
        }
    }
}
