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

using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Microsoft.Python.LanguageServer.CodeActions;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class SettingTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }
        public CancellationToken CancellationToken => TestContext.CancellationTokenSource.Token;

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void BasicInteger() {
            TestSettgins("{ a: 1}", "a", 1);
        }

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void BasicString() {
            TestSettgins("{ a: \"test\" }", "a", "test");
        }

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void BasicBool() {
            TestSettgins("{ a: true }", "a", true);
        }

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void BasicFloat() {
            TestSettgins("{ a: 1.0 }", "a", 1.0);
        }

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void NestedNames() {
            TestSettgins("{ a: { b: { c : true } } }", "a.b.c", true);
        }

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void Map() {
            var refactoring = JToken.Parse("{ a: [{n1: true}, {n2: false}] }");
            var codeActionSetting = CodeActionSettings.Parse(refactoring, quickFix: null, CancellationToken);

            var map = codeActionSetting.GetRefactoringOption<Dictionary<string, object>>("a", defaultValue: null);

            map["n1"].Should().Be(true);
            map["n2"].Should().Be(false);
        }

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void MapDottedName() {
            var refactoring = JToken.Parse("{ a: [{ \"n.n1\" : true}, { \"n.n2\" : false}] }");
            var codeActionSetting = CodeActionSettings.Parse(refactoring, quickFix: null, CancellationToken);

            var map = codeActionSetting.GetRefactoringOption<Dictionary<string, object>>("a", defaultValue: null);

            map["n.n1"].Should().Be(true);
            map["n.n2"].Should().Be(false);
        }

        [TestMethod, Priority(0), Timeout(AnalysisTimeoutInMS)]
        public void PrefixNestedNames() {
            var main = JToken.Parse("{ a: { b: { c : true } } }");
            var codeActionSetting = CodeActionSettings.Parse(main["a"], quickFix: null, CancellationToken);

            codeActionSetting.GetRefactoringOption("b.c", false).Should().Be(true);
        }

        private void TestSettgins<T>(string json, string key, T expected) {
            var refactoring = JToken.Parse(json);
            var codeActionSetting = CodeActionSettings.Parse(refactoring, quickFix: null, CancellationToken);

            codeActionSetting.GetRefactoringOption(key, default(T)).Should().Be(expected);
        }
    }
}
