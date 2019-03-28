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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    using LanguageServer = Implementation.LanguageServer;

    [TestClass]
    public class LinterTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task LinterOnOff() {
            const string code = @"a = x";

            var analysis = await GetAnalysisAsync(code);
            var a = Services.GetService<IPythonAnalyzer>();
            var d = a.LintModule(analysis.Document);
            d.Should().HaveCount(1);

            var provider = Substitute.For<IAnalysisOptionsProvider>();
            Services.AddService(provider);

            var ds = Services.GetService<IDiagnosticsService>();
            var options = new AnalysisOptions();
            provider.Options.Returns(_ => options);
            options.LintingEnabled = true;

            PublishDiagnostics();
            ds.Diagnostics[analysis.Document.Uri].Should().HaveCount(1);

            LanguageServer.HandleLintingOnOff(Services, false);
            options.LintingEnabled.Should().BeFalse();

            PublishDiagnostics();
            ds.Diagnostics[analysis.Document.Uri].Should().BeEmpty();

            LanguageServer.HandleLintingOnOff(Services, true);
            options.LintingEnabled.Should().BeTrue();

            PublishDiagnostics();
            ds.Diagnostics[analysis.Document.Uri].Should().HaveCount(1);
        }

        [TestMethod, Priority(0)]
        public async Task LinterConsidersNamedExpr() {
            const string code = @"
if x := 1:
    y = x
";

            var analysis = await GetAnalysisAsync(code);
            var a = Services.GetService<IPythonAnalyzer>();
            var d = a.LintModule(analysis.Document);
            d.Should().HaveCount(0);
        }
    }
}
