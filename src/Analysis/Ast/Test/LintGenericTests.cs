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
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LintGenericTests : LinterTestBase {
        private const string GenericSetup = @"
from typing import Generic, TypeVar
T = TypeVar('T', int, str)
T1 = TypeVar('T1', int, str)

_X = TypeVar('_X', str, int)
_T = _X
";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow("x = Generic[T, str]")]
        [DataRow("x = Generic[T, T1, int]")]
        [DataRow("x = Generic[T, str, int, T1]")]
        [DataRow("x = Generic[str, int]")]
        [DataRow("x = Generic[str]")]
        [DataTestMethod, Priority(0)]
        public async Task GenericNotAllTypeParameters(string decl) {
            var code = GenericSetup + decl;

            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            var start = decl.IndexOfOrdinal("Generic") + 1;
            // adding 1 because SourceSpan.End is exclusive and another 1 because SourceSpan is 1-indexed
            var end = decl.IndexOfOrdinal("]", start) + 2;

            diagnostic.SourceSpan.Should().Be(8, start, 8, end);
            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingGenericArguments);
            diagnostic.Message.Should().Be(Resources.GenericNotAllTypeParameters);
        }

        [DataRow("x = Generic[_T, _X]")]
        [DataRow("x = Generic[_T, T, T1, _X]")]
        [DataRow("x = Generic[_T,_T, T]")]
        [DataRow("x = Generic[T,T]")]
        [DataTestMethod, Priority(0)]
        public async Task GenericDuplicateArguments(string decl) {
            var code = GenericSetup + decl;
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().HaveCount(1);

            var diagnostic = analysis.Diagnostics.ElementAt(0);
            var start = decl.IndexOfOrdinal("Generic") + 1;
            // adding 1 because SourceSpan.End is exclusive and another 1 because SourceSpan is 1-indexed
            var end = decl.IndexOfOrdinal("]", start) + 2;
            diagnostic.SourceSpan.Should().Be(8, start, 8, end);

            diagnostic.ErrorCode.Should().Be(Diagnostics.ErrorCodes.TypingGenericArguments);
            diagnostic.Message.Should().Be(Resources.GenericNotAllUnique);
        }

        [DataRow("x = Generic[_X, T]")]
        [DataRow("x = Generic[T1, T]")]
        [DataRow("x = Generic[T]")]
        [DataRow("x = Generic[T,T1, _X]")]
        [DataTestMethod, Priority(0)]
        public async Task GenericArgumentsNoDiagnosticOnValid(string decl) {
            var code = GenericSetup + decl;
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task GenericNoArgumentsNoDiagnostic() {
            const string code = GenericSetup + @"
x = Generic[]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }


        [TestMethod, Priority(0)]
        public async Task GenericArgumentSpaceNoDiagnostic() {
            const string code = GenericSetup + @"
x = Generic[  ]
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Diagnostics.Should().BeEmpty();
        }
    }
}
