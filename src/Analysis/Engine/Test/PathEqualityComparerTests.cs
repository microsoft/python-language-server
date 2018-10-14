// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class PathEqualityComparerTests {
        [TestMethod]
        public void GetPathCompareKey_Windows() {
            var cmp = new PathEqualityComparer(isCaseSensitivePath: false, directorySeparator: '\\', altDirectorySeparator: '/');
            foreach (var path in new[] {
                "C:/normalized//path/",
                "C:/normalized/.\\path",
                "C:\\NORMalized\\\\path",
                "C:/normalized/path\\",
                "C:/normalized/path/",
                "C:/normalized/path/p1/p2/p3/../../..",
                "C:/normalized/path/p1/../p3/../",
                "C:/normalized/path/",
                "C:/normalized/here////..////path/"
            }) {
                Assert.AreEqual(@"C:\NORMALIZED\PATH", cmp.GetCompareKeyUncached(path), path);
            }
            foreach (var path in new[] {
                @"\\computer/normalized//path/",
                @"//computer/normalized/.\\path",
                @"\\computer\NORMalized\\path",
                @"\\computer/normalized/path\\",
                @"\/computer/normalized/path/",
                @"\\computer/normalized/here////..////path/"
            }) {
                Assert.AreEqual(@"\\COMPUTER\NORMALIZED\PATH", cmp.GetCompareKeyUncached(path), path);
            }

            Assert.AreEqual(@"C:\PATH", cmp.GetCompareKeyUncached(@"C:\..\..\PATH/"));
            Assert.AreEqual(@"C:\PATH", cmp.GetCompareKeyUncached("C:/.././.././path/"));
            Assert.AreEqual(@"\\COMPUTER\SHARE\PATH", cmp.GetCompareKeyUncached("//computer/share/.././.././path/"));
        }

        [TestMethod]
        public void GetPathCompareKey_OSX() {
            var cmp = new PathEqualityComparer(isCaseSensitivePath: false, directorySeparator: '/');
            foreach (var path in new[] {
                "/normalized//path/",
                "/normalized/./path",
                "/NORMalized//path",
                "/normalized/path/",
                "/normalized/path//",
                "/normalized/here//..//path/"
            }) {
                Assert.AreEqual("/NORMALIZED/PATH", cmp.GetCompareKeyUncached(path), path);
            }
            foreach (var path in new[] {
                "smb://computer/normalized//path/",
                "smb://computer/normalized/./path",
                "smb://computer/NORMalized//path",
                "smb://computer/normalized/path/",
                "smb://computer/normalized/path/",
                "smb://computer/normalized/here//..//path/"
            }) {
                Assert.AreEqual("smb://COMPUTER/NORMALIZED/PATH", cmp.GetCompareKeyUncached(path), path);
            }
        }

        [TestMethod]
        public void GetPathCompareKey_Linux() {
            var cmp = new PathEqualityComparer(isCaseSensitivePath: true, directorySeparator: '/');
            foreach (var path in new[] {
                "/normalized//path/",
                "/normalized/./path",
                "/normalized/path/",
                "/normalized/path//",
                "/normalized/here//..//path/"
            }) {
                Assert.AreEqual("/normalized/path", cmp.GetCompareKeyUncached(path), path);
            }
            foreach (var path in new[] {
                "smb://computer/normalized//path/",
                "smb://computer/normalized/./path",
                "smb://computer/normalized/path/",
                "smb://computer/normalized/path/",
                "smb://computer/normalized/here//..//path/"
            }) {
                Assert.AreEqual("smb://computer/normalized/path", cmp.GetCompareKeyUncached(path), path);
            }
        }

        [TestMethod]
        public void PathEqualityStartsWith_Windows() {
            var cmp = new PathEqualityComparer(isCaseSensitivePath: false, directorySeparator: '\\', altDirectorySeparator: '/');
            foreach (var path in new[] {
                new { p ="C:/root/a/b", isFull = false },
                new { p = @"C:\ROOT\", isFull = true },
                new { p = @"C:\Root", isFull = true },
                new { p = @"C:\notroot\..\root", isFull=true },
                new { p = @"C:\.\root\", isFull = true }
            }) {
                Assert.IsTrue(cmp.StartsWith(path.p, @"C:\Root"), path.p);
                if (path.isFull) {
                    Assert.IsFalse(cmp.StartsWith(path.p, @"C:\Root", allowFullMatch: false));
                } else {
                    Assert.IsTrue(cmp.StartsWith(path.p, @"C:\Root", allowFullMatch: false));
                }
            }
        }

        [TestMethod]
        public void PathEqualityStartsWith_OSX() {
            var cmp = new PathEqualityComparer(isCaseSensitivePath: false, directorySeparator: '/');
            foreach (var path in new[] {
                new { p = "/root/a/b", isFull = false },
                new { p ="/ROOT/", isFull = true },
                new { p = "/Root", isFull = true },
                new { p = "/notroot/../root", isFull = true },
                new { p = "/./root/", isFull = true }
            }) {
                Assert.IsTrue(cmp.StartsWith(path.p, "/Root"), path.p);
                if (path.isFull) {
                    Assert.IsFalse(cmp.StartsWith(path.p, "/Root", allowFullMatch: false));
                } else {
                    Assert.IsTrue(cmp.StartsWith(path.p, "/Root", allowFullMatch: false));
                }
            }
        }

        [TestMethod]
        public void PathEqualityStartsWith_Linux() {
            var cmp = new PathEqualityComparer(isCaseSensitivePath: true, directorySeparator: '/');
            foreach (var path in new[] {
                new { p = "/root/a/b", isFull = false },
                new { p = "/notroot/../root", isFull = true },
                new { p = "/./root/", isFull = true }
            }) {
                Assert.IsTrue(cmp.StartsWith(path.p, "/root"), path.p);
                if (path.isFull) {
                    Assert.IsFalse(cmp.StartsWith(path.p, "/root", allowFullMatch: false));
                } else {
                    Assert.IsTrue(cmp.StartsWith(path.p, "/root", allowFullMatch: false));
                }
            }
            foreach (var path in new[] {
                new { p ="/ROOT/", isFull = true },
            }) {
                Assert.IsFalse(cmp.StartsWith(path.p, "/root"), path.p);
                if (path.isFull) {
                    Assert.IsFalse(cmp.StartsWith(path.p, "/Root", allowFullMatch: false));
                } else {
                    Assert.IsTrue(cmp.StartsWith(path.p, "/ROOT", allowFullMatch: false));
                }
            }
        }

        [TestMethod]
        public void PathEqualityCache_Windows() {
            var cmp = new PathEqualityComparer(isCaseSensitivePath: false, directorySeparator: '\\', altDirectorySeparator: '/');

            var path1 = "C:\\normalized\\path\\";
            var path2 = "c:/normalized/here/../path";

            Assert.AreEqual(0, cmp._compareKeyCache.Count);
            cmp.GetHashCode(path1);
            Assert.AreEqual(1, cmp._compareKeyCache[path1].Accessed);

            cmp.GetHashCode(path2);
            Assert.AreEqual(1, cmp._compareKeyCache[path1].Accessed);
            Assert.AreEqual(2, cmp._compareKeyCache[path2].Accessed);

            cmp.GetHashCode(path1);
            Assert.AreEqual(3, cmp._compareKeyCache[path1].Accessed);
            Assert.AreEqual(2, cmp._compareKeyCache[path2].Accessed);

            foreach (var path_i in Enumerable.Range(0, 100).Select(i => $"C:\\path\\{i}\\here")) {
                cmp.GetHashCode(path_i);
                cmp.GetHashCode(path1);
            }

            cmp._compareKeyCache
                .Should().ContainKey(path1)
                .And.NotContainKey(path2);
        }
    }
}
