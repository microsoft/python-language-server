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
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Tests;
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

            // TODO: track assignments and type changes by position
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("l").OfType(BuiltinTypeId.List)
                .And.HaveVariable("t").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("s").OfType(BuiltinTypeId.Set)
                .And.HaveVariable("XY").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("XYZ").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("D").OfType(BuiltinTypeId.Dict);
        }

        [TestMethod, Priority(0)]
        public async Task FromImportSpecificValues() {
            var analysis = await GetAnalysisAsync("from Values import D");
            analysis.Should().HaveVariable("D").OfType(BuiltinTypeId.Dict);
        }

        [TestMethod, Priority(0)]
        public async Task FromImportReturnTypes() {
            const string code = @"from ReturnValues import *
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

        [TestMethod, Priority(0)]
        public async Task BuiltinImport() {
            var analysis = await GetAnalysisAsync(@"import sys");

            analysis.Should().HaveVariable("sys")
                 .Which.Should().HaveType(BuiltinTypeId.Module)
                 .And.HaveMember("platform");
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImportInClass() {
            const string code = @"
class C:
    import sys
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveClass("C")
                .Which.Should().HaveMember<IPythonModule>("sys")
                .Which.Should().HaveMember<IMember>("platform");
        }

        [TestMethod, Priority(0)]
        public async Task BuiltinImportInFunc() {
            const string code = @"
def f():
    import sys
    return sys.path

x = f()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.List);
        }

        [TestMethod, Priority(0)]
        public async Task ImportAs() {
            var analysis = await GetAnalysisAsync(@"import sys as s, array as a", PythonVersions.LatestAvailable3X);

            analysis.Should().HaveVariable("s")
                .Which.Should().HaveType<IPythonModule>()
                .Which.Should().HaveMember("platform");

            analysis.Should().HaveVariable("a")
                .Which.Should().HaveType<IPythonModule>()
                .Which.Should().HaveMember("ArrayType");
        }

        [TestMethod, Priority(0)]
        public async Task OsPathMembers() {
            var analysis = await GetAnalysisAsync(@"import os.path as P");
            analysis.Should().HaveVariable("P")
                .Which.Should().HaveMembers(@"abspath", @"dirname");
        }

        [TestMethod, Priority(0)]
        public async Task FromOsPathMembers() {
            var analysis = await GetAnalysisAsync(@"from os.path import join as JOIN");
            analysis.Should().HaveVariable("JOIN").Which.Should().HaveType<IPythonFunctionType>();
        }

        [TestMethod, Priority(0)]
        public async Task UnresolvedImport() {
            var analysis = await GetAnalysisAsync(@"import nonexistent");
            analysis.Should().HaveVariable("nonexistent")
                .Which.Value.GetPythonType<IPythonModule>().ModuleType.Should().Be(ModuleType.Unresolved);
            analysis.Diagnostics.Should().HaveCount(1);
            var d = analysis.Diagnostics.First();
            d.ErrorCode.Should().Be(ErrorCodes.UnresolvedImport);
            d.SourceSpan.Should().Be(1, 8, 1, 19);
            d.Message.Should().Be(Resources.ErrorUnresolvedImport.FormatInvariant("nonexistent"));
        }

        [TestMethod, Priority(0)]
        public async Task UnresolvedImportAs() {
            var analysis = await GetAnalysisAsync(@"import nonexistent as A");
            analysis.Should().HaveVariable("A")
                .Which.Value.GetPythonType<IPythonModule>().ModuleType.Should().Be(ModuleType.Unresolved);
            analysis.Diagnostics.Should().HaveCount(1);
            var d = analysis.Diagnostics.First();
            d.ErrorCode.Should().Be(ErrorCodes.UnresolvedImport);
            d.SourceSpan.Should().Be(1, 8, 1, 19);
            d.Message.Should().Be(Resources.ErrorUnresolvedImport.FormatInvariant("nonexistent"));
        }

        [TestMethod, Priority(0)]
        public async Task UnresolvedFromImport() {
            var analysis = await GetAnalysisAsync(@"from nonexistent import A");
            analysis.Diagnostics.Should().HaveCount(1);
            var d = analysis.Diagnostics.First();
            d.ErrorCode.Should().Be(ErrorCodes.UnresolvedImport);
            d.SourceSpan.Should().Be(1, 6, 1, 17);
            d.Message.Should().Be(Resources.ErrorUnresolvedImport.FormatInvariant("nonexistent"));
        }

        [TestMethod, Priority(0)]
        public async Task UnresolvedFromImportAs() {
            var analysis = await GetAnalysisAsync(@"from nonexistent import A as B");
            analysis.Diagnostics.Should().HaveCount(1);
            var d = analysis.Diagnostics.First();
            d.ErrorCode.Should().Be(ErrorCodes.UnresolvedImport);
            d.SourceSpan.Should().Be(1, 6, 1, 17);
            d.Message.Should().Be(Resources.ErrorUnresolvedImport.FormatInvariant("nonexistent"));
        }

        [TestMethod, Priority(0)]
        public async Task UnresolvedRelativeFromImportAs() {
            var analysis = await GetAnalysisAsync(@"from ..nonexistent import A as B");
            analysis.Diagnostics.Should().HaveCount(1);
            var d = analysis.Diagnostics.First();
            d.ErrorCode.Should().Be(ErrorCodes.UnresolvedImport);
            d.SourceSpan.Should().Be(1, 6, 1, 19);
            d.Message.Should().Be(Resources.ErrorRelativeImportBeyondTopLevel.FormatInvariant("nonexistent"));
        }

        [TestMethod, Priority(0)]
        public async Task FromFuture() {
            var analysis = await GetAnalysisAsync(@"from __future__ import print_function", PythonVersions.LatestAvailable2X);
            analysis.Diagnostics.Should().BeEmpty();
            analysis.Should().HaveFunction("print");
        }

        [TestMethod, Priority(0)]
        public async Task StarImportDoesNotOverwriteFunction() {
            const string code = @"
from sys import *

def exit():
    return 1234

x = exit()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task ModuleMembers() {
            var appUri = TestData.GetTestSpecificUri("app.py");
            await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "__init__.py"), "import m1");
            await TestData.CreateTestSpecificFileAsync(Path.Combine("package", "m1", "__init__.py"), string.Empty);

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            var doc = rdt.OpenDocument(appUri, "import package");

            var analysis = await doc.GetAnalysisAsync(Timeout.Infinite);
            analysis.Should().HaveVariable("package").Which.Should().HaveMember("m1");
        }
    }
}
