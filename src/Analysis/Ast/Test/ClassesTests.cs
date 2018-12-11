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
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ClassesTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Classes() {
            var code = await File.ReadAllTextAsync(Path.Combine(GetAnalysisTestDataFilesPath(), "Classes.py"));
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var names = analysis.TopLevelMembers.GetMemberNames();
            var all = analysis.AllMembers.ToArray();

            names.Should().OnlyContain("C1", "C2", "C3", "C4", "C5",
                "D", "E",
                "F1",
                "f"
            );

            all.First(x => x.Name == "C1").Type.Should().BeAssignableTo<IPythonClass>();
            all.First(x => x.Name == "C2").Type.Should().BeAssignableTo<IPythonClass>();
            all.First(x => x.Name == "C3").Type.Should().BeAssignableTo<IPythonClass>();
            all.First(x => x.Name == "C4").Type.Should().BeAssignableTo<IPythonClass>();

            all.First(x => x.Name == "C5")
                .Type.Should().BeAssignableTo<IPythonClass>()
                .Which.Name.Should().Be("C1");

            all.First(x => x.Name == "D").Type.Should().BeAssignableTo<IPythonClass>();
            all.First(x => x.Name == "E").Type.Should().BeAssignableTo<IPythonClass>();
            all.First(x => x.Name == "f").Type.Should().BeAssignableTo<IPythonFunction>();

            all.First(x => x.Name == "f").Type.Should().BeAssignableTo<IPythonFunction>();

            var f1 = all.First(x => x.Name == "F1");
            var c = f1.Type.Should().BeAssignableTo<IPythonClass>().Which;

            c.GetMemberNames().Should().OnlyContain("F2", "F3", "F6", "__class__", "__bases__");
            c.GetMember("F6").Should().BeAssignableTo<IPythonClass>()
                .Which.Documentation.Should().Be("C1");

            c.GetMember("F2").Should().BeAssignableTo<IPythonClass>();
            c.GetMember("F3").Should().BeAssignableTo<IPythonClass>();
            c.GetMember("__class__").Should().BeAssignableTo<IPythonClass>();
            c.GetMember("__bases__").Should().BeAssignableTo<IPythonSequenceType>();
        }
    }
}
