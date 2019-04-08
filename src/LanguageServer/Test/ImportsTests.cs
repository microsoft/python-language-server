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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
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
        public async Task ExplicitImplicitPackageMix() {
            const string appCode = @"
import projectA.foo
import projectA.foo.bar
import projectB.foo
import projectB.foo.baz

projectA.
projectA.foo.
projectB.
projectB.foo.";

            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);
            var init1Path = Path.Combine(root, "projectA", "foo", "__init__.py");
            var init2Path = Path.Combine(root, "projectA", "foo", "bar", "__init__.py");
            var init3Path = Path.Combine(root, "projectB", "foo", "__init__.py");
            var init4Path = Path.Combine(root, "projectB", "foo", "baz", "__init__.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X, appPath);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(new Uri(init1Path), string.Empty);
            rdt.OpenDocument(new Uri(init2Path), string.Empty);
            rdt.OpenDocument(new Uri(init3Path), string.Empty);
            rdt.OpenDocument(new Uri(init4Path), string.Empty);

            var doc = rdt.OpenDocument(new Uri(appPath), appCode, appPath);
            var analysis = await doc.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(7, 10));
            comps.Should().HaveLabels("foo");

            comps = cs.GetCompletions(analysis, new SourceLocation(8, 14));
            comps.Should().HaveLabels("bar");

            comps = cs.GetCompletions(analysis, new SourceLocation(9, 10));
            comps.Should().HaveLabels("foo");

            comps = cs.GetCompletions(analysis, new SourceLocation(10, 14));
            comps.Should().HaveLabels("baz");
        }

        [TestMethod, Priority(0)]
        public async Task ExplicitImplicitPackageMix2() {
            const string appCode = @"
import projectA.foo
import projectB.foo

from projectA.foo import bar
from projectB.foo import baz

projectA.
projectA.foo.
projectB.
projectB.foo.";

            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);
            var init1Path = Path.Combine(root, "projectA", "foo", "__init__.py");
            var init2Path = Path.Combine(root, "projectA", "foo", "bar", "__init__.py");
            var init3Path = Path.Combine(root, "projectB", "foo", "__init__.py");
            var init4Path = Path.Combine(root, "projectB", "foo", "baz", "__init__.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X, appPath);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(new Uri(init1Path), string.Empty);
            rdt.OpenDocument(new Uri(init2Path), string.Empty);
            rdt.OpenDocument(new Uri(init3Path), string.Empty);
            rdt.OpenDocument(new Uri(init4Path), string.Empty);

            var doc = rdt.OpenDocument(new Uri(appPath), appCode, appPath);
            var analysis = await doc.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(8, 10));
            comps.Should().HaveLabels("foo");

            comps = cs.GetCompletions(analysis, new SourceLocation(9, 14));
            comps.Should().HaveLabels("bar");

            comps = cs.GetCompletions(analysis, new SourceLocation(10, 10));
            comps.Should().HaveLabels("foo");

            comps = cs.GetCompletions(analysis, new SourceLocation(11, 14));
            comps.Should().HaveLabels("baz");
        }

        [TestMethod, Priority(0)]
        public async Task SysModuleChain() {
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
            var analyzer = Services.GetService<IPythonAnalyzer>();

            var doc1 = rdt.OpenDocument(uri1, content1);
            rdt.OpenDocument(uri2, content2);
            rdt.OpenDocument(uri3, content3);
            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await doc1.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(2, 5));
            comps.Should().HaveLabels("VALUE");
        }

        [TestMethod, Priority(0)]
        public async Task SysModuleChain_SingleOpen() {
            const string content = @"import module1.mod as mod
mod.";
            await TestData.CreateTestSpecificFileAsync("module1.py", @"import module2 as mod");
            await TestData.CreateTestSpecificFileAsync("module2.py", @"import sys
sys.modules['module1.mod'] = None
VALUE = 42");

            var root = TestData.GetTestSpecificRootUri().AbsolutePath;
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var doc = rdt.OpenDocument(TestData.GetDefaultModuleUri(), content);
            await doc.GetAstAsync();
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            var analysis = await doc.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(2, 5));
            comps.Should().HaveLabels("VALUE");
        }

        [TestMethod, Priority(0)]
        public async Task UncSearchPaths() {
            const string module1Path = @"q:\Folder\package\module1.py";
            const string module2Path = @"\\machine\share\package\module2.py";

            const string appCode1 = @"from package import ";
            const string appCode2 = @"from package import module1, module2
module1.
module2.";
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var interpreter = Services.GetService<IPythonInterpreter>();
            interpreter.ModuleResolution.SetUserSearchPaths(new[] { @"q:\Folder\", @"\\machine\share\" });

            rdt.OpenDocument(new Uri(module1Path), "X = 42");
            rdt.OpenDocument(new Uri(module2Path), "Y = 6 * 9");

            var doc = rdt.OpenDocument(new Uri(appPath), appCode1);
            var analysis = await doc.GetAnalysisAsync(-1);


            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(1, 21));
            comps.Should().HaveLabels("module1", "module2");

            doc.Update(new[] {
                new DocumentChange {
                    InsertedText = appCode2,
                    ReplacedSpan = new SourceSpan(1, 1, 1, 21)
                }
            });

            await doc.GetAstAsync();
            analysis = await doc.GetAnalysisAsync(-1);

            comps = cs.GetCompletions(analysis, new SourceLocation(2, 9));
            comps.Should().HaveLabels("X").And.NotContainLabels("Y");

            comps = cs.GetCompletions(analysis, new SourceLocation(3, 9));
            comps.Should().HaveLabels("Y").And.NotContainLabels("X");
        }

        [TestMethod, Priority(0)]
        public async Task UserSearchPathsInsideWorkspace() {
            var folder1 = TestData.GetTestSpecificPath("folder1");
            var folder2 = TestData.GetTestSpecificPath("folder2");
            var packageInFolder1 = Path.Combine(folder1, "package");
            var packageInFolder2 = Path.Combine(folder2, "package");
            var module1Path = Path.Combine(packageInFolder1, "module1.py");
            var module2Path = Path.Combine(packageInFolder2, "module2.py");
            const string module1Content = @"class A():
    @staticmethod
    def method1():
        pass";
            const string module2Content = @"class B():
    @staticmethod
    def method2():
        pass";
            const string mainContent = @"from package import module1 as mod1, module2 as mod2
mod1.
mod2.
mod1.A.
mod2.B.";
            var root = Path.GetDirectoryName(folder1);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);

            var rdt = Services.GetService<IRunningDocumentTable>();
            var analyzer = Services.GetService<IPythonAnalyzer>();
            var interpreter = Services.GetService<IPythonInterpreter>();
            interpreter.ModuleResolution.SetUserSearchPaths(new[] { folder1, folder2 });

            rdt.OpenDocument(new Uri(module1Path), module1Content);
            rdt.OpenDocument(new Uri(module2Path), module2Content);

            var mainPath = Path.Combine(root, "main.py");
            var doc = rdt.OpenDocument(new Uri(mainPath), mainContent);
            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await doc.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(2, 6));
            comps.Should().HaveLabels("A").And.NotContainLabels("B");

            comps = cs.GetCompletions(analysis, new SourceLocation(3, 6));
            comps.Should().HaveLabels("B").And.NotContainLabels("A");

            comps = cs.GetCompletions(analysis, new SourceLocation(4, 8));
            comps.Should().HaveLabels("method1");

            comps = cs.GetCompletions(analysis, new SourceLocation(5, 8));
            comps.Should().HaveLabels("method2");
        }

        [TestMethod, Priority(0)]
        public async Task PackageModuleImport() {
            const string appCode = @"
import package.sub_package.module1
import package.sub_package.module2

package.
package.sub_package.
package.sub_package.module1.
package.sub_package.module2.";

            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var module1Path = Path.Combine(root, "package", "sub_package", "module1.py");
            var module2Path = Path.Combine(root, "package", "sub_package", "module2.py");

            rdt.OpenDocument(new Uri(module1Path), "X = 42");
            rdt.OpenDocument(new Uri(module2Path), "Y = 6 * 9");

            var doc = rdt.OpenDocument(new Uri(appPath), appCode);
            var analysis = await doc.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(5, 9));
            comps.Should().OnlyHaveLabels("sub_package");

            comps = cs.GetCompletions(analysis, new SourceLocation(6, 21));
            comps.Should().OnlyHaveLabels("module1", "module2");

            comps = cs.GetCompletions(analysis, new SourceLocation(7, 29));
            comps.Should().HaveLabels("X").And.NotContainLabels("Y");

            comps = cs.GetCompletions(analysis, new SourceLocation(8, 29));
            comps.Should().HaveLabels("Y").And.NotContainLabels("X");
        }

        [TestMethod, Priority(0)]
        public async Task LoopImports() {
            var module1Code = @"
class B1:
    def M1(self):
        pass
    pass

from module2 import B2
class A1(B2):
    pass";
            var module2Code = @"
class B2:
    def M2(self):
        pass
    pass

from module3 import B3
class A2(B3):
    pass";
            var module3Code = @"
class B3:
    def M3(self):
        pass
    pass

from module1 import B1
class A3(B1):
    pass";

            var appCode = @"
from module1 import A1
from module2 import A2
from module3 import A3

a1 = A1()
a2 = A2()
a3 = A3()

a1.
a2.
a3.";

            var module1Uri = TestData.GetTestSpecificUri("module1.py");
            var module2Uri = TestData.GetTestSpecificUri("module2.py");
            var module3Uri = TestData.GetTestSpecificUri("module3.py");
            var appUri = TestData.GetTestSpecificUri("app.py");

            var root = Path.GetDirectoryName(appUri.AbsolutePath);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var analyzer = Services.GetService<IPythonAnalyzer>();

            rdt.OpenDocument(module1Uri, module1Code);
            rdt.OpenDocument(module2Uri, module2Code);
            rdt.OpenDocument(module3Uri, module3Code);

            var app = rdt.OpenDocument(appUri, appCode);
            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await app.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(10, 4));
            comps.Should().HaveLabels("M2");

            comps = cs.GetCompletions(analysis, new SourceLocation(11, 4));
            comps.Should().HaveLabels("M3");

            comps = cs.GetCompletions(analysis, new SourceLocation(12, 4));
            comps.Should().HaveLabels("M1");
        }

        [TestMethod, Priority(0)]
        public async Task TypingModule() {
            var analysis = await GetAnalysisAsync(@"from typing import ");
            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(1, 20));
            comps.Should().HaveLabels("TypeVar", "List", "Dict", "Union");
        }

        [TestMethod, Priority(0)]
        public async Task RelativeImportsFromParent() {
            const string module2Code = @"from ...package import module1
module1.";

            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var analyzer = Services.GetService<IPythonAnalyzer>();

            var module1Path = Path.Combine(root, "package", "module1.py");
            var module2Path = Path.Combine(root, "package", "sub_package", "module2.py");

            rdt.OpenDocument(new Uri(module1Path), "X = 42");
            var module2 = rdt.OpenDocument(new Uri(module2Path), module2Code);

            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await module2.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(2, 9));
            comps.Should().HaveLabels("X");
        }

        [DataRow(@"from package import sub_package; import package.sub_package.module")]
        [DataRow(@"import package.sub_package.module; from package import sub_package")]
        [DataRow(@"from package import sub_package; from package.sub_package import module")]
        [DataRow(@"from package.sub_package import module; from package import sub_package")]
        [TestMethod, Priority(0)]
        public async Task FromImport_ModuleAffectsPackage(string appCodeImport) {
            var appCode1 = appCodeImport + Environment.NewLine + "sub_package.";
            var appCode2 = appCodeImport + Environment.NewLine + "sub_package.module.";

            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var modulePath = Path.Combine(root, "package", "sub_package", "module.py");

            rdt.OpenDocument(new Uri(modulePath), "X = 42");
            var doc = rdt.OpenDocument(new Uri(appPath), appCode1);
            var analysis = await doc.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(2, 13));
            comps.Should().OnlyHaveLabels("module");

            doc.Update(new[] {
                    new DocumentChange {
                    InsertedText = appCode2,
                    ReplacedSpan = new SourceSpan(1, 1, 2, 13)
                }
            });

            analysis = await doc.GetAnalysisAsync(-1);
            comps = cs.GetCompletions(analysis, new SourceLocation(2, 21));
            comps.Should().HaveLabels("X");
        }

        [TestMethod, Priority(0)]
        public async Task AllSimple() {
            var module1Code = @"
class A:
    def foo(self):
        pass
    pass

class B:
    def bar(self):
        pass
    pass

__all__ = ['A']
";

            var appCode = @"
from module1 import *

A().
B().
";

            var module1Uri = TestData.GetTestSpecificUri("module1.py");
            var appUri = TestData.GetTestSpecificUri("app.py");

            var root = Path.GetDirectoryName(appUri.AbsolutePath);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var analyzer = Services.GetService<IPythonAnalyzer>();

            rdt.OpenDocument(module1Uri, module1Code);

            var app = rdt.OpenDocument(appUri, appCode);
            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await app.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(4, 5));
            comps.Should().HaveLabels("foo");

            comps = cs.GetCompletions(analysis, new SourceLocation(5, 5));
            comps.Should().NotContainLabels("bar");
        }

//        [DataRow(@"
//other = ['B']
//__all__ = ['A'] + other")]
//        [DataRow(@"
//other = ['B']
//__all__ = ['A']
//__all__ += other")]
        [DataRow(@"
other = ['B']
__all__ = ['A']
__all__.extend(other)")]
        [DataRow(@"
__all__ = ['A']
__all__.append('B')")]
        [DataTestMethod, Priority(0)]
        public async Task AllComplex(string allCode) {
            var module1Code = @"
class A:
    def foo(self):
        pass
    pass

class B:
    def bar(self):
        pass
    pass

class C:
    def baz(self):
        pass
    pass
" + allCode;

            const string appCode = @"
from module1 import *

A().
B().
C().
";

            var module1Uri = TestData.GetTestSpecificUri("module1.py");
            var appUri = TestData.GetTestSpecificUri("app.py");

            var root = Path.GetDirectoryName(appUri.AbsolutePath);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var analyzer = Services.GetService<IPythonAnalyzer>();

            rdt.OpenDocument(module1Uri, module1Code);

            var app = rdt.OpenDocument(appUri, appCode);
            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await app.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(4, 5));
            comps.Should().HaveLabels("foo");

            comps = cs.GetCompletions(analysis, new SourceLocation(5, 5));
            comps.Should().HaveLabels("bar");

            comps = cs.GetCompletions(analysis, new SourceLocation(6, 5));
            comps.Should().NotContainLabels("baz");
        }

        [DataRow(@"
__all__ = ['A']
__all__.something(A)")]
        [DataRow(@"
__all__ = ['A']
__all__ *= ['B']")]
        [DataRow(@"
__all__ = ['A']
__all__ += 1234")]
        [DataRow(@"
__all__ = ['A']
__all__.extend(123)")]
        [DataRow(@"
__all__ = ['A']
__all__.extend(nothing)")]
        [DataRow(@"
__all__ = [chr(x + 65) for x in range(1, 2)]")]
        [DataTestMethod, Priority(0)]
        public async Task AllUnsupported(string allCode) {
            var module1Code = @"
class A:
    def foo(self):
        pass
    pass

class B:
    def bar(self):
        pass
    pass
" + allCode;

            var appCode = @"
from module1 import *

A().
B().
";

            var module1Uri = TestData.GetTestSpecificUri("module1.py");
            var appUri = TestData.GetTestSpecificUri("app.py");

            var root = Path.GetDirectoryName(appUri.AbsolutePath);
            await CreateServicesAsync(root, PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var analyzer = Services.GetService<IPythonAnalyzer>();

            rdt.OpenDocument(module1Uri, module1Code);

            var app = rdt.OpenDocument(appUri, appCode);
            await analyzer.WaitForCompleteAnalysisAsync();
            var analysis = await app.GetAnalysisAsync(-1);

            var cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
            var comps = cs.GetCompletions(analysis, new SourceLocation(4, 5));
            comps.Should().HaveLabels("foo");

            comps = cs.GetCompletions(analysis, new SourceLocation(5, 5));
            comps.Should().HaveLabels("bar");
        }
    }
}
