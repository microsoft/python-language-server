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

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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
        public async Task SimpleTopLevelVariables() {
            const string code = @"
x = 'str'
y = 1

class C:
    def method(self):
        return 1.0

";
            var comps = await GetCompletionsAsync(code, 8, 1);
            comps.Select(c => c.label).Should().Contain("C", "x", "y", "while", "for", "yield");
        }

        [TestMethod, Priority(0)]
        public async Task SimpleStringMembers() {
            const string code = @"
x = 'str'
x.
";
            var comps = await GetCompletionsAsync(code, 3, 3);
            comps.Select(c => c.label).Should().Contain(@"isupper", @"capitalize", @"split");
        }

        [TestMethod, Priority(0)]
        public async Task ModuleMembers() {
            const string code = @"
import datetime
datetime.datetime.
";
            var comps = await GetCompletionsAsync(code, 3, 19);
            comps.Select(c => c.label).Should().Contain("now", @"tzinfo", @"ctime");
        }

        [TestMethod, Priority(0)]
        public async Task MembersIncomplete() {
            const string code = @"
class ABCDE:
    def method1(self): pass

ABC
ABCDE.me
";
            var comps = await GetCompletionsAsync(code, 5, 4);
            comps.Select(c => c.label).Should().Contain(@"ABCDE");

            comps = await GetCompletionsAsync(code, 6, 9);
            comps.Select(c => c.label).Should().Contain("method1");
        }
    }
}
