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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class SignatureTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task MethodSignature() {
            const string code = @"
class C:
    def method(self, a:int, b) -> float:
        return 1.0

C().method()
";
            var analysis = await GetAnalysisAsync(code);
            var src = new SignatureSource(new PlainTextDocumentationSource());

            var sig = src.GetSignature(analysis, new SourceLocation(6, 12));
            sig.activeSignature.Should().Be(0);
            sig.activeParameter.Should().Be(0);
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("method(a: int, b) -> float");

            var parameterSpans = sig.signatures[0].parameters.Select(p => p.label).OfType<int[]>().SelectMany(x => x).ToArray();
            parameterSpans.Should().ContainInOrder(7, 13, 15, 16);
        }

        [TestMethod, Priority(0)]
        public async Task ClassInitializer() {
            const string code = @"
class C:
    def __init__(self, a:int, b):
        pass

C()
";
            var analysis = await GetAnalysisAsync(code);
            var src = new SignatureSource(new PlainTextDocumentationSource());

            var sig = src.GetSignature(analysis, new SourceLocation(6, 3));
            sig.activeSignature.Should().Be(0);
            sig.activeParameter.Should().Be(0);
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("C(a: int, b)");
        }

        [TestMethod, Priority(0)]
        public async Task ImportedClassInitializer() {
            const string module1Code = @"
class C:
    def __init__(self, a:int, b):
        pass
";

            const string appCode = @"
import module1

module1.C()
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

            var src = new SignatureSource(new PlainTextDocumentationSource());

            var sig = src.GetSignature(analysis, new SourceLocation(4, 11));
            sig.activeSignature.Should().Be(0);
            sig.activeParameter.Should().Be(0);
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("C(a: int, b)");
        }

        [TestMethod, Priority(0)]
        public async Task GenericClassMethod() {
            const string code = @"
from typing import TypeVar, Generic

_T = TypeVar('_T')

class Box(Generic[_T]):
    def get(self) -> _T:
        return self.v

boxedint = Box(1234)
x = boxedint.get()

boxedstr = Box('str')
y = boxedstr.get()
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var src = new SignatureSource(new PlainTextDocumentationSource());

            var sig = src.GetSignature(analysis, new SourceLocation(11, 18));
            sig.signatures.Should().NotBeNull();
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("get() -> int");

            sig = src.GetSignature(analysis, new SourceLocation(14, 18));
            sig.signatures.Should().NotBeNull();
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("get() -> str");

        }
    }
}
