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
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class ImportsTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Completions_ExplicitImplicitPackageMix() {
            const string appCode = @"
import projectA.foo
import projectA.foo.bar
import projectB.foo
import projectB.foo.baz

projectA.";

            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);
            var init1Path = Path.Combine(root, "projectA", "foo", "bar", "__init__.py");
            var init2Path = Path.Combine(root, "projectA", "foo", "__init__.py");
            var init3Path = Path.Combine(root, "projectB", "foo", "bar", "__init__.py");
            var init4Path = Path.Combine(root, "projectB", "foo", "__init__.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X, appPath);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(new Uri(init1Path), string.Empty, init1Path);
            rdt.OpenDocument(new Uri(init2Path), string.Empty, init2Path);
            rdt.OpenDocument(new Uri(init3Path), string.Empty, init3Path);
            rdt.OpenDocument(new Uri(init4Path), string.Empty, init4Path);

            var doc = rdt.OpenDocument(new Uri(appPath), appCode, appPath);
            var analysis = await doc.GetAnalysisAsync();

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(7, 10))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain("foo");
        }

        [TestMethod, Priority(0)]
        public async Task Completions_SysModuleChain() {
            const string content1 = @"import module2.mod as mod
mod.";
            const string content2 = @"import module3 as mod";
            const string content3 = @"import sys
sys.modules['module2.mod'] = None
VALUE = 42";

            var uri1 = await TestData.CreateTestSpecificFileAsync("module1.py", content1);
            var uri2 = await TestData.CreateTestSpecificFileAsync("module2.py", content2);
            var uri3 = await TestData.CreateTestSpecificFileAsync("module3.py", content3);

            var root = TestData.GetTestSpecificRootUri().AbsolutePath;
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var doc1 = rdt.OpenDocument(uri1, content1);
            rdt.OpenDocument(uri2, content2);
            rdt.OpenDocument(uri3, content3);

            var analysis = await doc1.GetAnalysisAsync();

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = (await cs.GetCompletionsAsync(analysis, new SourceLocation(2, 5))).Completions.ToArray();
            comps.Select(c => c.label).Should().Contain("VALUE");
        }
    }
}
