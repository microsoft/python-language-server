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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class InheritNonClassTests : AnalysisTestBase {
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
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("str"));
            diagnostic.ErrorCode.Should().Be(ErrorCodes.InheritNonClass);

            diagnostic = analysis.Diagnostics.ElementAt(1);
            diagnostic.SourceSpan.Should().Be(10, 7, 10, 8);
            diagnostic.Message.Should().Be(Resources.InheritNonClass.FormatInvariant("5"));
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


        [TestMethod, Priority(0)]
        public async Task InheritFromOtherModule() {
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
        public async Task InheritFromRenamedOtherModule() {
            const string code = @"
import typing

tmp = typing.TypeVar
class C(tmp):
    def method(self):
        return 'test'
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
