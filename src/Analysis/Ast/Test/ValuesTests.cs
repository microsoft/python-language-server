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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ValuesTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Values() {
            var code = await File.ReadAllTextAsync(Path.Combine(GetAnalysisTestDataFilesPath(), "Values.py"));
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("pi").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("l").OfType(BuiltinTypeId.List)
                .And.HaveVariable("t").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("s").OfType(BuiltinTypeId.Set)
                .And.HaveVariable("X").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("Y").OfType(BuiltinTypeId.Unicode)
                .And.HaveVariable("Z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("PI").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("L").OfType(BuiltinTypeId.List)
                .And.HaveVariable("T").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("D").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("S").OfType(BuiltinTypeId.Set);
        }
    }
}
