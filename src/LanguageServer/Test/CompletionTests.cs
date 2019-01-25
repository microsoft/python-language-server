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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class CompletionTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task TopLevelVariables() {
            const string code = @"
x = 'str'
y = 1

class C:
    def method(self):
        return 1.0

";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(8, 1))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain("C", "x", "y", "while", "for", "yield");
        }

        [TestMethod, Priority(0)]
        public async Task StringMembers() {
            const string code = @"
x = 'str'
x.
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 3))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain(new[] {@"isupper", @"capitalize", @"split"});
        }

        [TestMethod, Priority(0)]
        public async Task ModuleMembers() {
            const string code = @"
import datetime
datetime.datetime.
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 19))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain(new[] {"now", @"tzinfo", @"ctime"});
        }

        [TestMethod, Priority(0)]
        public async Task MembersIncomplete() {
            const string code = @"
class ABCDE:
    def method1(self): pass

ABC
ABCDE.me
";
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(5, 4))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain(@"ABCDE");

            comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(6, 9))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain("method1");
        }

        [DataRow(PythonLanguageVersion.V36, "value")]
        [DataRow(PythonLanguageVersion.V37, "object")]
        [DataTestMethod]
        public async Task OverrideCompletions3X(PythonLanguageVersion version, string parameterName) {
            const string code = @"
class oar(list):
    def 
    pass
";
            var analysis = await GetAnalysisAsync(code, version);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var result = await cs.GetCompletionsAsync(analysis, new SourceLocation(3, 9));

            result.Should().HaveItem("append")
                .Which.Should().HaveInsertText($"append(self, {parameterName}):{Environment.NewLine}    return super().append({parameterName})")
                .And.HaveInsertTextFormat(InsertTextFormat.PlainText);
        }

        [TestMethod, Priority(0)]
        public async Task OverrideCompletionsNested() {
            // Ensure that nested classes are correctly resolved.
            const string code = @"
class oar(int):
    class fob(dict):
        def 
        pass
    def 
    pass
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X);
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);

            var completionsOar = await cs.GetCompletionsAsync(analysis, new SourceLocation(6, 9));
            completionsOar.Should().NotContainLabels("keys", "items")
                .And.HaveItem("bit_length");

            var completionsFob = await cs.GetCompletionsAsync(analysis, new SourceLocation(4, 13));
            completionsFob.Should().NotContainLabels("bit_length")
                .And.HaveLabels("keys", "items");
        }
    }
}
