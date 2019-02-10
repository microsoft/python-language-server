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

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis;
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

            var sig = await src.GetSignatureAsync(analysis, new SourceLocation(6, 12));
            sig.activeSignature.Should().Be(0);
            sig.activeParameter.Should().Be(0);
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("method(a: int, b) -> float");
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

            var sig = await src.GetSignatureAsync(analysis, new SourceLocation(11, 18));
            sig.signatures.Should().NotBeNull();
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("get() -> int");

            sig = await src.GetSignatureAsync(analysis, new SourceLocation(14, 18));
            sig.signatures.Should().NotBeNull();
            sig.signatures.Length.Should().Be(1);
            sig.signatures[0].label.Should().Be("get() -> str");

        }
    }
}
