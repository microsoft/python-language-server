// Python Tools for Visual Studio
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class ImportTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        [DataRow(@"from package import sub_package; import package.sub_package.module1")]
        [DataRow(@"import package.sub_package.module1; from package import sub_package")]
        [DataRow(@"from package import sub_package; from package.sub_package import module")]
        [DataRow(@"from package.sub_package import module; from package import sub_package")]
        [Ignore("Not yet implemented")]
        [ServerTestMethod(LatestAvailable3X = true, TestSpecificRootUri = true), Priority(0)]
        public async Task Completions_FromImport_ModuleAffectsPackage(Server server, string appCodeImport) {
            var appPath = "app.py";
            var appUri = TestData.GetTestSpecificUri(appPath);
            var appCode1 = appCodeImport + Environment.NewLine + "sub_package.";
            var appCode2 = appCodeImport + Environment.NewLine + "sub_package.module.";

            var modulePath = Path.Combine("package", "sub_package", "module.py");
            var moduleCode = "X = 42";
            
            await server.OpenDocumentAndGetUriAsync(modulePath, moduleCode);
            await server.OpenDocumentAndGetUriAsync(appPath, appCode1);
            var subPackageCompletions = await server.SendCompletion(appUri, 1, 12);

            await server.SendDidChangeTextDocumentAsync(appUri, appCode2);
            var packageCompletions = await server.SendCompletion(appUri, 1, 20);

            subPackageCompletions.Should().OnlyHaveLabels("module");
            packageCompletions.Should().HaveLabels("X");
        }

        [ServerTestMethod(LatestAvailable3X = true, TestSpecificRootUri = true), Priority(0)]
        public async Task Completions_PackageModuleImport(Server server) {
            var appPath = "app.py";
            var appCode = @"
import package.sub_package.module1
import package.sub_package.module2

package.
package.sub_package.
package.sub_package.module1.
package.sub_package.module2.";

            var module1Path = Path.Combine("package", "sub_package", "module1.py");
            var module1Code = "X = 42";
            var module2Path = Path.Combine("package", "sub_package", "module2.py");
            var module2Code = "Y = 6 * 9";

            await server.OpenDocumentAndGetUriAsync(module1Path, module1Code);
            await server.OpenDocumentAndGetUriAsync(module2Path, module2Code);
            var appUri = await server.OpenDocumentAndGetUriAsync(appPath, appCode);

            var completionPackage = await server.SendCompletion(appUri, 4, 8);
            var completionSubPackage = await server.SendCompletion(appUri, 5, 20);
            var completionModule1 = await server.SendCompletion(appUri, 6, 28);
            var completionModule2 = await server.SendCompletion(appUri, 7, 28);

            completionPackage.Should().OnlyHaveLabels("sub_package");
            completionSubPackage.Should().OnlyHaveLabels("module1", "module2");
            completionModule1.Should().HaveLabels("X").And.NotContainLabels("Y");
            completionModule2.Should().HaveLabels("Y").And.NotContainLabels("X");
        }

        [ServerTestMethod(LatestAvailable3X = true, TestSpecificRootUri = true), Priority(0)]
        public async Task Completions_FromPackageImportModule_UncSearchPaths(Server server) {
            server.Analyzer.SetSearchPaths(new[] { @"c:\Folder\", @"\\machine\share\" });
            var module1Path = @"c:\Folder\package\module1.py";
            var module2Path = @"\\machine\share\package\module2.py";

            var appPath = "app.py";
            var appCode1 = @"from package import ";
            var appCode2 = @"from package import module1, module2
module1.
module2.";

            await server.OpenDocumentAndGetUriAsync(module1Path, "X = 42");
            await server.OpenDocumentAndGetUriAsync(module2Path, "Y = 6 * 9");

            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            var appUri = await server.OpenDocumentAndGetUriAsync(appPath, appCode1);
            var importCompletion = await server.SendCompletion(appUri, 0, 20);

            await server.SendDidChangeTextDocumentAsync(appUri, appCode2);
            var completionModule1 = await server.SendCompletion(appUri, 1, 8);
            var completionModule2 = await server.SendCompletion(appUri, 2, 8);

            importCompletion.Should().HaveLabels("module1", "module2");
            completionModule1.Should().HaveLabels("X").And.NotContainLabels("Y");
            completionModule2.Should().HaveLabels("Y").And.NotContainLabels("X");
        }

        [ServerTestMethod(LatestAvailable3X = true, TestSpecificRootUri = true), Priority(0)]
        public async Task Completions_ExplicitImplicitPackageMix(Server server) {
            var appPath = "app.py";
            var appCode = @"
import projectA.foo
import projectA.foo.bar
import projectB.foo
import projectB.foo.baz

projectA.";

            var init1Path = Path.Combine("projectA", "foo", "bar", "__init__.py");
            var init2Path = Path.Combine("projectA", "foo", "__init__.py");
            var init3Path = Path.Combine("projectB", "foo", "bar", "__init__.py");
            var init4Path = Path.Combine("projectB", "foo", "__init__.py");

            await server.OpenDocumentAndGetUriAsync(init1Path, string.Empty);
            await server.OpenDocumentAndGetUriAsync(init2Path, string.Empty);
            await server.OpenDocumentAndGetUriAsync(init3Path, string.Empty);
            await server.OpenDocumentAndGetUriAsync(init4Path, string.Empty);
            var appUri = await server.OpenDocumentAndGetUriAsync(appPath, appCode);

            var completion = await server.SendCompletion(appUri, 6, 9);
            completion.Should().HaveLabels("foo");
        }

        [ServerTestMethod(LatestAvailable3X = true, TestSpecificRootUri = true), Priority(0)]
        public async Task Completions_SysModuleChain(Server server) {
            var uri1 = await server.OpenDocumentAndGetUriAsync("module1.py", @"import module2.mod as mod
mod.");
            var uri2 = await server.OpenDocumentAndGetUriAsync("module2.py", @"import module3 as mod");
            var uri3 = await server.OpenDocumentAndGetUriAsync("module3.py", @"import sys
sys.modules['module2.mod'] = None
VALUE = 42");

            await server.WaitForCompleteAnalysisAsync(CancellationToken.None);

            var completion = await server.SendCompletion(uri1, 1, 4);
            completion.Should().HaveLabels("VALUE");
        }
    }
}
