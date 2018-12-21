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
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ImportTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task FromImportValues() {
            var analysis = await GetAnalysisAsync("from Values import *");

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("pi").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("l").OfType(BuiltinTypeId.List)
                .And.HaveVariable("t").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("s").OfType(BuiltinTypeId.Set)
                .And.HaveVariable("X").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("Y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("Z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("PI").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("L").OfType(BuiltinTypeId.List)
                .And.HaveVariable("T").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("D").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("S").OfType(BuiltinTypeId.Set);
        }

        [TestMethod, Priority(0)]
        public async Task FromImportMultiValues() {
            var analysis = await GetAnalysisAsync("from MultiValues import *");

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("l").OfType(BuiltinTypeId.List)
                .And.HaveVariable("t").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("s").OfType(BuiltinTypeId.Set)
                .And.HaveVariable("XY").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("XYZ").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("D").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task FromImportSpecificValues() {
            var analysis = await GetAnalysisAsync("from Values import D");
            analysis.Should().HaveVariable("D").OfType(BuiltinTypeId.Dict);
        }

        [TestMethod, Priority(0)]
        public async Task ImportNonExistingModule() {
            var code = await File.ReadAllTextAsync(Path.Combine(GetAnalysisTestDataFilesPath(), "Imports.py"));
            var analysis = await GetAnalysisAsync(code);

            analysis.TopLevelMembers.GetMemberNames().Should().OnlyContain("version_info", "a_made_up_module");
        }

        [TestMethod, Priority(0)]
        public async Task FromImportReturnTypes() {
            var code = @"from ReturnValues import *
R_str = r_str()
R_object = r_object()
R_A1 = A()
R_A2 = A().r_A()
R_A3 = R_A1.r_A()";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveFunctionVariables("r_a", "r_b", "r_str", "r_object")
                .And.HaveClassVariables("A")
                .And.HaveVariable("R_str").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("R_object").OfType(BuiltinTypeId.Object)
                .And.HaveVariable("R_A1").OfType("A")
                .And.HaveVariable("R_A2").OfType("A")
                .And.HaveVariable("R_A3").OfType("A");
        }
    }
}
