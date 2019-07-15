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
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintRedefinedFunctionTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicRedefinedFuncTest() {
            const string code = @"
def hello():
    pass

def hello(test):
    print(test)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionRedefined);
            diagnostic.SourceSpan.Should().Be(5, 5, 5, 10);
            diagnostic.Message.Should().Be(Resources.FunctionRedefined.FormatInvariant(2));
        }

        [TestMethod, Priority(0)]
        public async Task RedefinedFuncInClassTest() {
            const string code = @"
class Test:
    def hello():
        pass

    def hello(test):
        print(test)
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionRedefined);
            diagnostic.SourceSpan.Should().Be(6, 9, 6, 14);
            diagnostic.Message.Should().Be(Resources.FunctionRedefined.FormatInvariant(3));
        }

        [TestMethod, Priority(0)]
        public async Task ValidRedefinitionsNoErrors() {
            const string code = @"
def foo():
    pass

bar = foo

def bar():
    pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task NestedFunctionValid() {
            const string code = @"
def foo():
    def foo():
        return 123
    return foo()

foo()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task RedefineFunctionInNestedFunction() {
            const string code = @"
def foo():
    def foo():
        return 123

    def foo():
        return 213
    
    return foo()

foo()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionRedefined);
            diagnostic.SourceSpan.Should().Be(6, 9, 6, 12);
            diagnostic.Message.Should().Be(Resources.FunctionRedefined.FormatInvariant(3));
        }

        [TestMethod, Priority(0)]
        public async Task SameFunctionInNestedClassDifferentScope() {
            const string code = @"
class tmp:
    def foo(self):
        return 123
    
    class tmp1:
        def foo(self):
            return 213
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task RedefineFunctionInNestedClass() {
            const string code = @"
class tmp:
    def foo(self):
        return 123
    
    class tmp1:
        def foo(self):
            return 213

        def foo(self):
            return 4784

";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionRedefined);
            diagnostic.SourceSpan.Should().Be(10, 13, 10, 16);
            diagnostic.Message.Should().Be(Resources.FunctionRedefined.FormatInvariant(7));
        }


        /// <summary>
        /// Finish when can handle conditional functions declarations (if ever)
        /// </summary>
        /// <returns></returns>
        [Ignore]
        [TestMethod, Priority(0)]
        public async Task ValidConditionalNoErrors() {
            const string code = @"
if 1 == 1:
    def a():
        pass
else:
     def a():
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task RedefinedFuncsOtherModules() {
            var module1Code = @"
def hello():
    pass

def hello2():
    pass
";

            var appCode = @"
from module1 import *

def hello():
    pass
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
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task RedefinedFuncsOtherModulesNamedImport() {
            var module1Code = @"
def hello():
    pass

def hello2():
    pass
";

            var appCode = @"
from module1 import hello

def hello():
    pass
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
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task RedefinedFuncsOtherModulesRenamedImport() {
            var module1Code = @"
def hello():
    pass

def hello2():
    pass
";

            var appCode = @"
from module1 import hello2 as hello

def hello():
    pass
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
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task RedefinedFuncsOneIsProperty() {
            const string code = @"
class tmp:
    def foo(self):
        return 123
   
    @property
    def foo(self):
        return 123
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionRedefined);
            diagnostic.SourceSpan.Should().Be(7, 9, 7, 12);
            diagnostic.Message.Should().Be(Resources.FunctionRedefined.FormatInvariant(3));
        }

        [TestMethod, Priority(0)]
        public async Task RedefinedFuncsBothAreProperties() {
            const string code = @"
class tmp:
    @property
    def foo(self):
        return 123
   
    @property
    def foo(self):
        return 123
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.ErrorCode.Should().Be(ErrorCodes.FunctionRedefined);
            diagnostic.SourceSpan.Should().Be(8, 9, 8, 12);
            diagnostic.Message.Should().Be(Resources.FunctionRedefined.FormatInvariant(4));
        }
    }
}
