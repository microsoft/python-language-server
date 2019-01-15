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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ArgumentSetTests: AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task EmptyArgSet() {
            const string code = @"
def f(): ...
f()
";
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction("f").Which;
            var call = GetCall(analysis.Ast);
            var result = ArgumentSet.FromArgs(f.FunctionDefinition, call, analysis.Document, new DiagSink(), out var argSet);
            result.Should().BeTrue();
            argSet.Arguments.Count.Should().Be(0);
            argSet.SequenceArgument.Should().BeNull();
            argSet.DictArgument.Should().BeNull();
        }

        private CallExpression GetCall(PythonAst ast) {
            var statements = (ast.Body as SuiteStatement)?.Statements;
            return statements?.OfType<ExpressionStatement>().FirstOrDefault(e => e.Expression is CallExpression)?.Expression as CallExpression;
        }
    }
}
