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
using Microsoft.Python.Analysis.Analyzer.Caching;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class CachingTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Basic() {
            const string code = @"
def f(a):
    return 1

class A:
    def __init__(self): ...
    def a(self) -> str: ...

    def b(self):
        return False

    def c(self, a):
        return a
    
    @property
    def d(self):
        return 1
";
            var md = await GetSerializedModuleData(code);
            md.Functions.Should().HaveCount(5);

            md.Functions.Should().ContainKey("f");
            md.Functions["f"].Should().Be("int");

            md.Functions.Should().ContainKey("A.a");
            md.Functions["A.a"].Should().Be("str");

            md.Functions.Should().ContainKey("A.b");
            md.Functions["A.b"].Should().Be("bool");

            md.Functions.Should().ContainKey("A.c");
            md.Functions["A.c"].Should().BeNull();

            md.Functions.Should().ContainKey("A.d");
            md.Functions["A.d"].Should().Be("int");

            md.Functions.Should().NotContainKey("A.__init__");
        }

        [TestMethod, Priority(0)]
        public async Task Nested() {
            const string code = @"
class A: ...

def f(a):
    def e():
        return 1.0
    return A()

class B:
    class C:
        def a(self) -> str: ...

        @property
        def d(self):
            return 1

    def g(self):
        def h():
            return 1
        return B()
";
            var md = await GetSerializedModuleData(code);
            md.Functions.Should().HaveCount(6);

            md.Functions.Should().ContainKey("f");
            md.Functions["f"].Should().Be("A");

            md.Functions.Should().ContainKey("f.e");
            md.Functions["f.e"].Should().Be("float");

            md.Functions.Should().ContainKey("B.C.a");
            md.Functions["B.C.a"].Should().Be("str");

            md.Functions.Should().ContainKey("B.C.d");
            md.Functions["B.C.d"].Should().Be("int");

            md.Functions.Should().ContainKey("B.g");
            md.Functions["B.g"].Should().Be("B");

            md.Functions.Should().ContainKey("B.g.h");
            md.Functions["B.g.h"].Should().Be("int");
        }

        private async Task<ModuleData> GetSerializedModuleData(string code) {
            var fs = Substitute.For<IFileSystem>();
            fs.FileExists(Arg.Any<string>()).Returns(false);

            string path = null;
            string content = null;
            fs.When(x => x.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
                .Do(x => {
                    path = x.Args()[0] as string;
                    content = x.Args()[1] as string;
                });

            var analysis = await GetAnalysisAsync(code, () => Services.ReplaceService(typeof(IFileSystem), fs));

            var cache = new AnalysisCache(Services, TestData.GetTestSpecificPath(".cache"));
            await cache.WriteAnalysisAsync(analysis.Document, analysis.Document.GlobalScope);

            path.Should().NotBeNullOrEmpty();
            content.Should().NotBeNullOrEmpty();

            return JsonSerializer.CreateDefault().Deserialize<ModuleData>(new JsonTextReader(new StringReader(content)));
        }
    }
}
