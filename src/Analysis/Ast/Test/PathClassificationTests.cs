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

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class PathClassificationTests {
        private readonly FileSystem _fs = new FileSystem(new OSPlatform());

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod]
        public void Plain() {
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            var venv = Path.Combine(root, "venv");
            var venvLib = Path.Combine(venv, "Lib");
            var venvSitePackages = Path.Combine(venvLib, "site-packages");

            var fromInterpreter = new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venvSitePackages, PythonLibraryPathType.Site),
            };

            var (interpreterPaths, userPaths, zipPaths) = PythonLibraryPath.ClassifyPaths(root, _fs, fromInterpreter, Array.Empty<string>());

            interpreterPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venvSitePackages, PythonLibraryPathType.Site),
            });

            userPaths.Should().BeEmpty();
        }

        [TestMethod]
        public void WithSrcDir() {
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            var venv = Path.Combine(root, "venv");
            var venvLib = Path.Combine(venv, "Lib");
            var venvSitePackages = Path.Combine(venvLib, "site-packages");

            var src = Path.Combine(root, "src");

            var fromInterpreter = new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venvSitePackages, PythonLibraryPathType.Site),
            };

            var fromUser = new[] {
                "./src",
            };

            var (interpreterPaths, userPaths, zipPaths) = PythonLibraryPath.ClassifyPaths(root, _fs, fromInterpreter, fromUser);

            interpreterPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venvSitePackages, PythonLibraryPathType.Site),
            });

            userPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(src, PythonLibraryPathType.Unspecified),
            });
        }

        [TestMethod]
        public void NormalizeUser() {
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            var src = Path.Combine(root, "src");

            var fromUser = new[] {
                "./src/",
            };

            var (interpreterPaths, userPaths, zipPaths) = PythonLibraryPath.ClassifyPaths(root, _fs, Array.Empty<PythonLibraryPath>(), fromUser);

            interpreterPaths.Should().BeEmpty();

            userPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(src, PythonLibraryPathType.Unspecified),
            });
        }

        [TestMethod]
        public void NestedUser() {
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            var src = Path.Combine(root, "src");
            var srcSomething = Path.Combine(src, "something");
            var srcFoo = Path.Combine(src, "foo");
            var srcFooBar = Path.Combine(srcFoo, "bar");

            var fromUser = new[] {
                "./src",
                "./src/something",
                "./src/foo/bar",
                "./src/foo",
            };

            var (interpreterPaths, userPaths, zipPaths) = PythonLibraryPath.ClassifyPaths(root, _fs, Array.Empty<PythonLibraryPath>(), fromUser);

            interpreterPaths.Should().BeEmpty();

            userPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(src, PythonLibraryPathType.Unspecified),
                new PythonLibraryPath(srcSomething, PythonLibraryPathType.Unspecified),
                new PythonLibraryPath(srcFooBar, PythonLibraryPathType.Unspecified),
                new PythonLibraryPath(srcFoo, PythonLibraryPathType.Unspecified),
            });
        }

        [TestMethod]
        public void NestedUserOrdering() {
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            var src = Path.Combine(root, "src");
            var srcSomething = Path.Combine(src, "something");
            var srcFoo = Path.Combine(src, "foo");
            var srcFooBar = Path.Combine(srcFoo, "bar");

            var fromUser = new[] {
                "./src/foo",
                "./src/foo/bar",
                "./src",
                "./src/something",
            };

            var (interpreterPaths, userPaths, zipPaths) = PythonLibraryPath.ClassifyPaths(root, _fs, Array.Empty<PythonLibraryPath>(), fromUser);

            interpreterPaths.Should().BeEmpty();

            userPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(srcFoo, PythonLibraryPathType.Unspecified),
                new PythonLibraryPath(srcFooBar, PythonLibraryPathType.Unspecified),
                new PythonLibraryPath(src, PythonLibraryPathType.Unspecified),
                new PythonLibraryPath(srcSomething, PythonLibraryPathType.Unspecified),
            });
        }

        [TestMethod]
        public void InsideStdLib() {
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            var venv = Path.Combine(root, "venv");
            var venvLib = Path.Combine(venv, "Lib");
            var venvSitePackages = Path.Combine(venvLib, "site-packages");
            var inside = Path.Combine(venvSitePackages, "inside");
            var what = Path.Combine(venvSitePackages, "what");

            var src = Path.Combine(root, "src");

            var fromInterpreter = new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venvSitePackages, PythonLibraryPathType.Site),
                new PythonLibraryPath(inside, PythonLibraryPathType.Pth),
            };

            var fromUser = new[] {
                "./src",
                "./venv/Lib/site-packages/what",
            };

            var (interpreterPaths, userPaths, zipPaths) = PythonLibraryPath.ClassifyPaths(root, _fs, fromInterpreter, fromUser);

            interpreterPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(what, PythonLibraryPathType.Unspecified),
                new PythonLibraryPath(venvSitePackages, PythonLibraryPathType.Site),
                new PythonLibraryPath(inside, PythonLibraryPathType.Pth),
            });

            userPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(src, PythonLibraryPathType.Unspecified),
            });
        }

        [TestMethod]
        public void SiteOutsideStdlib() {
            var appPath = TestData.GetTestSpecificPath("app.py");
            var root = Path.GetDirectoryName(appPath);

            var venv = Path.Combine(root, "venv");
            var venvLib = Path.Combine(venv, "Lib");
            var sitePackages = Path.Combine(root, "site-packages");

            var src = Path.Combine(root, "src");

            var fromInterpreter = new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(sitePackages, PythonLibraryPathType.Site),
            };

            var fromUser = new[] {
                "./src",
            };

            var (interpreterPaths, userPaths, zipPaths) = PythonLibraryPath.ClassifyPaths(root, _fs, fromInterpreter, fromUser);

            interpreterPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(venvLib, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(venv, PythonLibraryPathType.StdLib),
                new PythonLibraryPath(sitePackages, PythonLibraryPathType.Site),
            });

            userPaths.Should().BeEquivalentToWithStrictOrdering(new[] {
                new PythonLibraryPath(src, PythonLibraryPathType.Unspecified),
            });
        }
    }
}
