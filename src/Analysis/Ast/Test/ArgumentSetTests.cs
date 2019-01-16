﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Parsing.Ast;
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
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.ListArgument.Should().BeNull();
            argSet.DictionaryArgument.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task KeywordArgs() {
            const string code = @"
def f(a, b): ...
f(b=1, a=2)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(2);
            argSet.Arguments[0].Name.Should().Be("a");
            argSet.Arguments[0].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
            argSet.Arguments[1].Name.Should().Be("b");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.ListArgument.Should().BeNull();
            argSet.DictionaryArgument.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task DefaultArgs() {
            const string code = @"
def f(a, b, c='str'): ...
f(b=1, a=2)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(3);
            argSet.Arguments[0].Name.Should().Be("a");
            argSet.Arguments[0].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
            argSet.Arguments[1].Name.Should().Be("b");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.Arguments[2].Name.Should().Be("c");
            argSet.Arguments[2].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be("str");
            argSet.ListArgument.Should().BeNull();
            argSet.DictionaryArgument.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task StarArg() {
            const string code = @"
def f(a, b, *, c='str', d=True): ...
f(1, 2, d=False)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(4);
            argSet.Arguments[0].Name.Should().Be("a");
            argSet.Arguments[0].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.Arguments[1].Name.Should().Be("b");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
            argSet.Arguments[2].Name.Should().Be("c");
            argSet.Arguments[2].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be("str");
            argSet.Arguments[3].Name.Should().Be("d");
            argSet.Arguments[3].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(false);
            argSet.ListArgument.Should().BeNull();
            argSet.DictionaryArgument.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task StarArgExtraPositionals() {
            const string code = @"
def f(a, b, *, c='str'): ...
f(1, 2, 3, 4, c=6)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.Errors.Count.Should().Be(1);
            argSet.Errors[0].ErrorCode.Should().Be(ErrorCodes.TooManyPositionalArgumentsBeforeStar);
        }

        [TestMethod, Priority(0)]
        public async Task TwoStarArg() {
            const string code = @"
def f(a, b, *, *, c='str'): ...
f(1, 2, 3, 4, 5, c=6)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
        }

        [TestMethod, Priority(0)]
        public async Task NamedStarArg() {
            const string code = @"
def f(a, b, *list, c='str', d=True): ...
f(1, 2, 3, 4, 5, c='a')
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(4);
            argSet.Arguments[0].Name.Should().Be("a");
            argSet.Arguments[0].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.Arguments[1].Name.Should().Be("b");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
            argSet.Arguments[2].Name.Should().Be("c");
            argSet.Arguments[2].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be("a");
            argSet.Arguments[3].Name.Should().Be("d");
            argSet.Arguments[3].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(true);
            argSet.ListArgument.Should().NotBeNull();
            argSet.ListArgument.Name.Should().Be("list");
            argSet.ListArgument.Expressions.OfType<ConstantExpression>().Select(c => c.Value).Should().ContainInOrder(3, 4, 5);
            argSet.DictionaryArgument.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task NamedDictArg() {
            const string code = @"
def f(a, b, **dict): ...
f(b=1, a=2, c=3, d=4, e='str')
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(2);
            argSet.Arguments[0].Name.Should().Be("a");
            argSet.Arguments[0].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
            argSet.Arguments[1].Name.Should().Be("b");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.ListArgument.Should().BeNull();
            argSet.DictionaryArgument.Should().NotBeNull();
            argSet.DictionaryArgument.Name.Should().Be("dict");
            argSet.DictionaryArgument.Expressions["c"].Should().BeAssignableTo<ConstantExpression>().Which.Value.Should().Be(3);
            argSet.DictionaryArgument.Expressions["d"].Should().BeAssignableTo<ConstantExpression>().Which.Value.Should().Be(4);
            argSet.DictionaryArgument.Expressions["e"].Should().BeAssignableTo<ConstantExpression>().Which.Value.Should().Be("str");
        }

        [TestMethod, Priority(0)]
        public async Task NamedDictExtraPositionals() {
            const string code = @"
def f(a, b, **dict): ...
f(1, 2, 3, 4, 5, c='a')
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.Errors.Count.Should().Be(1);
            argSet.Errors[0].ErrorCode.Should().Be(ErrorCodes.TooManyPositionalArgumentsBeforeStar);
        }

        [TestMethod, Priority(0)]
        public async Task DoubleKeywordArg() {
            const string code = @"
def f(a, b): ...
f(a=1, a=2)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.Errors.Count.Should().Be(1);
            argSet.Errors[0].ErrorCode.Should().Be(ErrorCodes.ParameterAlreadySpecified);
        }

        [TestMethod, Priority(0)]
        public async Task UnknownKeywordArg() {
            const string code = @"
def f(a, b): ...
f(a=1, c=2)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.Errors.Count.Should().Be(1);
            argSet.Errors[0].ErrorCode.Should().Be(ErrorCodes.UnknownParameterName);
        }

        [TestMethod, Priority(0)]
        public async Task TooManyKeywordArgs() {
            const string code = @"
def f(a, b): ...
f(a=1, b=2, a=1)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.Errors.Count.Should().Be(1);
            argSet.Errors[0].ErrorCode.Should().Be(ErrorCodes.ParameterAlreadySpecified);
        }

        [TestMethod, Priority(0)]
        public async Task TooManyPositionalArgs() {
            const string code = @"
def f(a, b): ...
f(1, 2, 1)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.Errors.Count.Should().Be(1);
            argSet.Errors[0].ErrorCode.Should().Be(ErrorCodes.TooManyFunctionArguments);
        }

        [TestMethod, Priority(0)]
        public async Task TooFewArgs() {
            const string code = @"
def f(a, b): ...
f(1)
";
            var argSet = await GetArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(0);
            argSet.Errors.Count.Should().Be(1);
            argSet.Errors[0].ErrorCode.Should().Be(ErrorCodes.ParameterMissing);
        }

        [TestMethod, Priority(0)]
        public async Task Method() {
            const string code = @"
class A:
    def f(self, a, b): ...

a = A()
a.f(1, 2)
";
            var argSet = await GetClassArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(3);
            argSet.Errors.Count.Should().Be(0);
            argSet.Arguments[0].Name.Should().Be("self");
            argSet.Arguments[0].Expression.Should().BeNull();
            argSet.Arguments[0].Value.Should().BeAssignableTo<IPythonClassType>();
            argSet.Arguments[1].Name.Should().Be("a");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.Arguments[2].Name.Should().Be("b");
            argSet.Arguments[2].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
        }

        [TestMethod, Priority(0)]
        public async Task StaticMethod() {
            const string code = @"
class A:
    @staticmethod
    def f(a, b): ...

A.f(1, 2)
";
            var argSet = await GetClassArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(2);
            argSet.Errors.Count.Should().Be(0);
            argSet.Arguments[0].Name.Should().Be("a");
            argSet.Arguments[0].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.Arguments[1].Name.Should().Be("b");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethod() {
            const string code = @"
class A:
    @classmethod
    def f(cls, a, b): ...

a = A()
a.f(b=1, a=2)
";
            var argSet = await GetClassArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(3);
            argSet.Errors.Count.Should().Be(0);
            argSet.Arguments[0].Name.Should().Be("cls");
            argSet.Arguments[0].Expression.Should().BeNull();
            argSet.Arguments[0].Value.Should().BeAssignableTo<IPythonClassType>();
            argSet.Arguments[1].Name.Should().Be("a");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
            argSet.Arguments[2].Name.Should().Be("b");
            argSet.Arguments[2].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
        }

        [TestMethod, Priority(0)]
        public async Task UnboundMethod() {
            const string code = @"
class A:
    def f(self, a, b): ...

a = A()
f = A.f
f(a, 1, 2)
";
            var argSet = await GetUnboundArgSetAsync(code);
            argSet.Arguments.Count.Should().Be(3);
            argSet.Errors.Count.Should().Be(0);
            argSet.Arguments[0].Name.Should().Be("self");
            argSet.Arguments[0].Expression.Should().BeOfType<NameExpression>().Which.Name.Should().Be("a");
            argSet.Arguments[1].Name.Should().Be("a");
            argSet.Arguments[1].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(1);
            argSet.Arguments[2].Name.Should().Be("b");
            argSet.Arguments[2].Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(2);
        }

        private async Task<ArgumentSet> GetArgSetAsync(string code, string funcName = "f") {
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveFunction(funcName).Which;
            var call = GetCall(analysis.Ast);
            return new ArgumentSet(f, call, analysis.Document, null);
        }

        private async Task<ArgumentSet> GetUnboundArgSetAsync(string code, string funcName = "f") {
            var analysis = await GetAnalysisAsync(code);
            var f = analysis.Should().HaveVariable(funcName).Which;
            var call = GetCall(analysis.Ast);
            return new ArgumentSet(f.Value.GetPythonType<IPythonFunctionType>(), call, analysis.Document, null);
        }

        private async Task<ArgumentSet> GetClassArgSetAsync(string code, string className = "A", string funcName = "f") {
            var analysis = await GetAnalysisAsync(code);
            var cls = analysis.Should().HaveClass(className).Which;
            var f = cls.Should().HaveMethod(funcName).Which;
            var call = GetCall(analysis.Ast);
            return new ArgumentSet(f, call, analysis.Document, null);
        }

        private CallExpression GetCall(PythonAst ast) {
            var statements = (ast.Body as SuiteStatement)?.Statements;
            return statements?.OfType<ExpressionStatement>().FirstOrDefault(e => e.Expression is CallExpression)?.Expression as CallExpression;
        }
    }
}
