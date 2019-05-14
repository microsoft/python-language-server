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

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests;
using Microsoft.Python.LanguageServer.Services;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class PythonInspectorServiceTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task MemberNamesSys(bool is3x) {
            using (var s = await CreateServicesAsync(null, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X, null))
            using (var inspector = new PythonInspectorService(s)) {
                for (var i = 0; i < 2; i++) {
                    var response = await inspector.GetModuleMemberNames("sys");
                    response.Should().NotBeNull();
                    response.Members.Should().Contain("stdout").And.NotContain("__all__");
                    response.All.Should().BeNull();
                }
            }
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task MemberNamesOsPath(bool is3x) {
            using (var s = await CreateServicesAsync(null, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X, null))
            using (var inspector = new PythonInspectorService(s)) {
                for (var i = 0; i < 2; i++) {
                    var response = await inspector.GetModuleMemberNames("os.path");
                    response.Should().NotBeNull();
                    response.Members.Should().Contain("join").And.Contain("__all__");
                    response.All.Should().Contain("join").And.NotContain("__all__");
                }
            }
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod, Priority(0)]
        public async Task MemberNamesNotFound(bool is3x) {
            using (var s = await CreateServicesAsync(null, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X, null))
            using (var inspector = new PythonInspectorService(s)) {
                for (var i = 0; i < 2; i++) {
                    var response = await inspector.GetModuleMemberNames("thismoduledoesnotexist");
                    response.Should().BeNull();
                }
            }
        }
    }
}

