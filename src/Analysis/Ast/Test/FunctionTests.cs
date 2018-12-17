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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class FunctionTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Functions() {
            var code = await File.ReadAllTextAsync(Path.Combine(GetAnalysisTestDataFilesPath(), "Functions.py"));
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var mod = analysis.Document;

            mod.GetMemberNames().Should().OnlyContain("f", "f2", "g", "h", "C");
            mod.GetMember("f").Should().BeAssignableTo<IPythonFunction>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember("f2").Should().BeAssignableTo<IPythonFunction>()
                .Which.Documentation.Should().Be("f");

            mod.GetMember("g").Should().BeAssignableTo<IPythonFunction>();
            mod.GetMember("h").Should().BeAssignableTo<IPythonFunction>();

            var c = mod.GetMember("C").Should().BeAssignableTo<IPythonClass>().Which;
            c.GetMemberNames().Should().OnlyContain("i", "j", "C2", "__class__", "__bases__");
            c.GetMember("i").Should().BeAssignableTo<IPythonFunction>();
            c.GetMember("j").Should().BeAssignableTo<IPythonFunction>();
            c.GetMember("__class__").Should().BeAssignableTo<IPythonClass>();
            c.GetMember("__bases__").Should().BeAssignableTo<IPythonSequence>();

            var c2 = c.GetMember("C2").Should().BeAssignableTo<IPythonClass>().Which;
            c2.GetMemberNames().Should().OnlyContain("k", "__class__", "__bases__");
            c2.GetMember("k").Should().BeAssignableTo<IPythonFunction>();
            c2.GetMember("__class__").Should().BeAssignableTo<IPythonClass>();
            c2.GetMember("__bases__").Should().BeAssignableTo<IPythonSequence>();
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/microsoft/python-language-server/issues/406")]
        public async Task NamedTupleReturnAnnotation() {
            var code = @"
from ReturnAnnotation import *
nt = namedtuple('Point', ['x', 'y'])
pt = nt(1, 2)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            analysis.Should().HaveVariable("pt").OfTypes(BuiltinTypeId.Tuple);
        }
    }
}
