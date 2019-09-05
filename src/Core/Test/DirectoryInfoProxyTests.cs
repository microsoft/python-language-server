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
using Microsoft.Python.Core.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Core.Tests {
    [TestClass]
    public class DirectoryInfoProxyTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task EnumerateFileSystemInfos() {
            var root = TestData.GetTestSpecificPath();
            await TestData.CreateTestSpecificFileAsync("a_y.py", "not important");
            await TestData.CreateTestSpecificFileAsync("b_y.py", "not important");
            await TestData.CreateTestSpecificFileAsync("c_z.py", "not important");

            var proxy = new DirectoryInfoProxy(root);
            var files = proxy.EnumerateFileSystemInfos(new[] { "*.py" }, new[] { "*z.py" }).OrderBy(x => x.FullName).ToArray();
            files.Should().HaveCount(2);

            files[0].FullName.Should().Be(Path.Combine(root, "a_y.py"));
            files[1].FullName.Should().Be(Path.Combine(root, "b_y.py"));
        }
    }
}
