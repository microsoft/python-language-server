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
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class DocumentHighlightTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();


        [TestMethod, Priority(0)]
        public async Task HighlightBasic() {
            const string code = @"
x = 1

def func(x):
    return x

y = func(x)
x = 2
";
            var analysis = await GetAnalysisAsync(code);
            var dhs = new DocumentHighlightSource(Services);

            // Test global scope
            var highlights1 = await dhs.DocumentHighlightAsync(analysis.Document.Uri, new SourceLocation(8, 1));

            highlights1.Should().HaveCount(3);
            highlights1[0].range.Should().Be(1, 0, 1, 1);
            highlights1[0].kind.Should().Be(DocumentHighlightKind.Write);
            highlights1[1].range.Should().Be(6, 9, 6, 10);
            highlights1[1].kind.Should().Be(DocumentHighlightKind.Read);
            highlights1[2].range.Should().Be(7, 0, 7, 1);

            // Test local scope in func()
            var highlights2 = await dhs.DocumentHighlightAsync(analysis.Document.Uri, new SourceLocation(4, 10));

            highlights2.Should().HaveCount(2);
            highlights2[0].range.Should().Be(3, 9, 3, 10);
            highlights2[0].kind.Should().Be(DocumentHighlightKind.Write);
            highlights2[1].range.Should().Be(4, 11, 4, 12);
            highlights2[1].kind.Should().Be(DocumentHighlightKind.Read);
        }

        [TestMethod, Priority(0)]
        public async Task HighlightEmptyDocument() {
            await GetAnalysisAsync(string.Empty);
            var dhs = new DocumentHighlightSource(Services);
            var references = await dhs.DocumentHighlightAsync(null, new SourceLocation(1, 1));
            references.Should().BeEmpty();
        }
    }
}
