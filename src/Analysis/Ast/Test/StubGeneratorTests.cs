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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Generators;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class StubGeneratorTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BuiltInModule() {
            await TestModuleStub(a => a.Document.Interpreter.ModuleResolution.BuiltinsModule, args: Array.Empty<string>(), TestStrings.BuiltInModuleStubContent);
        }

        [TestMethod, Priority(0)]
        public async Task CompiledBuiltInModule() {
            await TestModuleStub(a => a.Document.Interpreter.ModuleResolution.GetOrLoadModule("sys"), args: new[] { "-u8", "sys" }, TestStrings.SysStubContent, s => s.StartsWith("dllhandle"));
        }

        [TestMethod, Priority(0)]
        public async Task Module() {
            await TestModuleStub(a => a.Document.Interpreter.ModuleResolution.GetOrLoadModule("msilib"), args: new[] { "-u8", "msilib" }, TestStrings.MsiLibStubContent);
        }

        private async Task TestModuleStub(Func<IDocumentAnalysis, IPythonModule> moduleGetter, string[] args, string expected, Func<string, bool> filter = null) {
            var notUsed = @"";
            var analysis = await GetAnalysisAsync(notUsed);
            var logger = analysis.ExpressionEvaluator.Services.GetService<ILogger>();

            var module = moduleGetter(analysis);
            var stubCode = StubGenerator.Scrape(analysis.Document.Interpreter, logger, module, args, CancellationToken.None);

            var actualLines = stubCode.Trim().Split('\n');
            var expectedLines = expected.Split('\n');

            actualLines.Length.Should().Be(expectedLines.Length);

            for (var i = 0; i < actualLines.Length; i++) {
                if (filter != null && filter(actualLines[i]) && filter(expectedLines[i])) {
                    continue;
                }

                actualLines[i].Should().Be(expectedLines[i]);
            }
        }
    }
}
