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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class StubMergeTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Datetime() {
            var analysis = await GetAnalysisAsync("import datetime");

            var module = analysis.Should().HaveVariable("datetime")
                .Which.Should().HaveType<IPythonModule>().Which.Value as IPythonModule;
            module.Should().NotBeNull();
            
            var stub = module.Stub;
            stub.Should().NotBeNull();

            var dt = stub.Should().HaveClass("datetime").Which;
            dt.DeclaringModule.Name.Should().Be("datetime");
        }

        [TestMethod, Priority(0)]
        public async Task Os() {
            var analysis = await GetAnalysisAsync("import os");

            var module = analysis.Should().HaveVariable("os")
                .Which.Should().HaveType<IPythonModule>().Which.Value as IPythonModule;
            module.Should().NotBeNull();

            var environ = module.Should().HaveMember<IPythonInstance>("environ").Which;
            var environType = environ.GetPythonType();
            environType.Documentation.Should().NotBeNullOrEmpty();
        }
    }
}
