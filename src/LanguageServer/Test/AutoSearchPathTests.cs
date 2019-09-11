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
using FluentAssertions;
using Microsoft.Python.Core.IO;
using Microsoft.Python.LanguageServer.SearchPaths;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class AutoSearchPathTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public void SrcNotDir() {
            var root = TestData.GetTestSpecificPath();
            var rootSrc = Path.Combine(root, "src");
            var fs = Substitute.For<IFileSystem>();
            fs.DirectoryExists(rootSrc).Returns(false);
            AutoSearchPathFinder.Find(fs, root).Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public void SrcDir() {
            var root = TestData.GetTestSpecificPath();
            var rootSrc = Path.Combine(root, "src");
            var fs = Substitute.For<IFileSystem>();
            fs.DirectoryExists(rootSrc).Returns(true);
            fs.FileExists(Path.Combine(rootSrc, "__init__.py")).Returns(false);

            AutoSearchPathFinder.Find(fs, root).Should().BeEquivalentTo(new[] { rootSrc });
        }

        [TestMethod, Priority(0)]
        public void SrcDirWithInitPy() {
            var root = TestData.GetTestSpecificPath();
            var rootSrc = Path.Combine(root, "src");
            var fs = Substitute.For<IFileSystem>();
            fs.DirectoryExists(rootSrc).Returns(true);
            fs.FileExists(Path.Combine(rootSrc, "__init__.py")).Returns(true);

            AutoSearchPathFinder.Find(fs, root).Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public void NullRoot() {
            var fs = Substitute.For<IFileSystem>();
            AutoSearchPathFinder.Find(fs, null).Should().BeEmpty();
        }
    }
}
