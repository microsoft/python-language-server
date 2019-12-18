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

using FluentAssertions;
using Microsoft.Python.Core.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Microsoft.Python.Core.Tests {
    [TestClass]
    public class PathUtilsTests {
        private IFileSystem _fileSystem;
        [TestInitialize]
        public void TestInitialize() {
            _fileSystem = Substitute.For<IFileSystem>();
            _fileSystem.FileExists(default).Returns(true);
        }

        [TestMethod, Priority(0)]
        public void ZipFileUNCPath() {
            PathUtils.TryGetZipFilePath(_fileSystem, @"\\server\home\share\test.zip", out var zipPath, out var relativeZipPath);
            zipPath.Should().Be(@"\\server\home\share\test.zip");
            relativeZipPath.Should().BeEmpty();

            PathUtils.TryGetZipFilePath(_fileSystem, @"\\server\home\share\test.zip\test\a.py", out zipPath, out relativeZipPath);
            zipPath.Should().Be(@"\\server\home\share\test.zip");
            relativeZipPath.Should().Be("test/a.py");

            PathUtils.TryGetZipFilePath(_fileSystem, "\\path\\foo\\baz\\test.zip\\test\\a.py", out zipPath, out relativeZipPath);
            zipPath.Should().Be("\\path\\foo\\baz\\test.zip");
            relativeZipPath.Should().Be("test/a.py");
        }

        [TestMethod, Priority(0)]
        public void ZipFilePath() {
            PathUtils.TryGetZipFilePath(_fileSystem, "\\path\\foo\\baz\\test.zip", out var zipPath, out var relativeZipPath);
            zipPath.Should().Be("\\path\\foo\\baz\\test.zip");
            relativeZipPath.Should().BeEmpty();

            PathUtils.TryGetZipFilePath(_fileSystem, "\\path\\foo\\baz\\test.zip\\test\\a.py", out zipPath, out relativeZipPath);
            zipPath.Should().Be("\\path\\foo\\baz\\test.zip");
            relativeZipPath.Should().Be("test/a.py");

            PathUtils.TryGetZipFilePath(_fileSystem, "\\path\\foo\\baz\\test.zip\\test\\foo\\baz.py", out zipPath, out relativeZipPath);
            zipPath.Should().Be("\\path\\foo\\baz\\test.zip");
            relativeZipPath.Should().Be("test/foo/baz.py");
        }
    }
}
