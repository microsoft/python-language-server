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
    public class LintInheritNonClassTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task InheritFromRenamedBuiltin() {
            const string code = @"
tmp = str

class C(tmp):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task InheritFromBuiltin() {
            const string code = @"
class C(str):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task InheritFromUserClass() {
            const string code = @"
class D:
    def hello(self):
        pass

class C(D):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task InheritFromConstant() {
            const string code = @"
class C(5):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(2, 7, 2, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("5"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }


        [TestMethod, Priority(0)]
        public async Task InheritFromConstantVar() {
            const string code = @"
x = 'str'

class C(x):
    def method(self):
        return 'test'

x = 5

class D(x):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(2);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(4, 7, 4, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("x"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.SourceSpan.Should().Be(10, 7, 10, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("x"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }

        [Ignore]
        [TestMethod, Priority(0)]
        public async Task InheritFromBinaryOp() {
            const string code = @"
x = 5

class C(x + 2):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(4, 7, 4, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("x + 2"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }


        /// <summary>
        /// Because typing module is specialized with functions instead of classes,
        /// we think that we are extending a function instead of a class so we would erroneously
        /// give a diagnostic message
        /// </summary>
        /// <returns></returns>
        [Ignore]
        [TestMethod, Priority(0)]
        public async Task InheritFromTypingModule() {
            const string code = @"
import typing

class C(typing.TypeVar):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromOtherModule() {
            var module1Code = @"
class B:
    def hello(self):
        pass
";

            var appCode = @"
from module1 import B

class C(B):
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
            var analysis = await app.GetAnalysisAsync();
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromPropertyReturnsFunction() {
            const string code = @"
def func(self):
    pass

class B:
    @property
    def test(self):
        return func

b = B()

class C(b.test):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(12, 7, 12, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("b.test"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }


        [TestMethod, Priority(0)]
        public async Task InheritFromPropertyReturnsMethod() {
            const string code = @"
class B:
    @property
    def test(self):
        return self.func

    def func(self):
        pass

b = B()

class C(b.test):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(12, 7, 12, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("b.test"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromPropertyReturnsConstant() {
            const string code = @"
class B:
    @property
    def test(self):
        return 5

b = B()

class C(b.test):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(9, 7, 9, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("b.test"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromEmptyPropertyNoDiagnostic() {
            const string code = @"
class B:
    @property
    def test(self):
        pass

b = B()

class C(b.test):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromMethod() {
            const string code = @"
class B:
    def test(self):
        pass

b = B()

class C(b.test):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(8, 7, 8, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("b.test"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromObjectInstance() {
            const string code = @"
class B:
    def test(self):
        pass

b = B()

class C(b):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(8, 7, 8, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("b"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromFunction() {
            const string code = @"
def test(self):
    pass

class C(test):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            diagnostic.SourceSpan.Should().Be(5, 7, 5, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("test"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);
        }

        [TestMethod, Priority(0)]
        public async Task InheritFromVarThatHoldsType() {
            const string code = @"
class A: pass

a = A

class B(a):
    pass
b = B()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
