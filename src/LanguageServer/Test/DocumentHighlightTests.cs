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
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing;
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
            var analysis = await GetAnalysisAsync(string.Empty);
            var dhs = new DocumentHighlightSource(Services);
            var highlights = await dhs.DocumentHighlightAsync(analysis.Document.Uri, new SourceLocation(1, 1));
            highlights.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task HighlightNonReference() {
            const string code = @"
x = y = 0
assert x == 1
assert y != 3
";
            var analysis = await GetAnalysisAsync(code);
            var dhs = new DocumentHighlightSource(Services);
            var highlights = await dhs.DocumentHighlightAsync(analysis.Document.Uri, new SourceLocation(3, 5));

            highlights.Should().HaveCount(2);
            highlights[0].range.Should().Be(2, 0, 2, 6);
            highlights[0].kind.Should().Be(DocumentHighlightKind.Text);
            highlights[1].range.Should().Be(3, 0, 3, 6);
            highlights[1].kind.Should().Be(DocumentHighlightKind.Text);
        }

        [TestMethod, Priority(0)]
        public async Task HighlightUndefined() {
            const string code = @"
assert x == 1
assert x != 3
";
            var analysis = await GetAnalysisAsync(code);
            var dhs = new DocumentHighlightSource(Services);
            var highlights = await dhs.DocumentHighlightAsync(analysis.Document.Uri, new SourceLocation(2, 8));

            highlights.Should().HaveCount(2);
            highlights[0].range.Should().Be(1, 7, 1, 8);
            highlights[0].kind.Should().Be(DocumentHighlightKind.Text);
            highlights[1].range.Should().Be(2, 7, 2, 8);
            highlights[1].kind.Should().Be(DocumentHighlightKind.Text);
        }

        [TestMethod, Priority(0)]
        public void TokenCacheEntriesLimit() {
            var c = new TokenCache(3, TimeSpan.FromMinutes(1));
            c.GetTokens("1", PythonLanguageVersion.V38);
            c.GetTokens("12", PythonLanguageVersion.V38);
            c.GetTokens("123", PythonLanguageVersion.V38);
            c.GetTokens("1234", PythonLanguageVersion.V38);

            (DateTime AccessTime, IReadOnlyList<TokenInfo> Tokens)[] e = c.Entries.ToArray();

            e.Should().HaveCount(3);
            var byTime = e.OrderBy(x => x.AccessTime).ToArray();
            (byTime[0].Tokens[0].SourceSpan.End.Column - byTime[0].Tokens[0].SourceSpan.Start.Column).Should().Be(2);
            (byTime[1].Tokens[0].SourceSpan.End.Column - byTime[1].Tokens[0].SourceSpan.Start.Column).Should().Be(3);
            (byTime[2].Tokens[0].SourceSpan.End.Column - byTime[2].Tokens[0].SourceSpan.Start.Column).Should().Be(4);
        }

        [TestMethod, Priority(0)]
        public async Task TokenCacheExpiration() {
            var c = new TokenCache(5, TimeSpan.FromMilliseconds(50));
            c.GetTokens("1", PythonLanguageVersion.V38);
            await Task.Delay(10);
            c.GetTokens("12", PythonLanguageVersion.V38);
            await Task.Delay(10);
            c.GetTokens("123", PythonLanguageVersion.V38);
            await Task.Delay(40);
            c.GetTokens("1234", PythonLanguageVersion.V38);

            (DateTime AccessTime, IReadOnlyList<TokenInfo> Tokens)[] e = c.Entries.ToArray();
            e.Should().HaveCount(2);
            var byTime = e.OrderBy(x => x.AccessTime).ToArray();
            (byTime[0].Tokens[0].SourceSpan.End.Column - byTime[1].Tokens[0].SourceSpan.Start.Column).Should().Be(3);
            (byTime[1].Tokens[0].SourceSpan.End.Column - byTime[1].Tokens[0].SourceSpan.Start.Column).Should().Be(4);
        }
    }
}
