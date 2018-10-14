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
        public void PathEquality() {
            foreach (var path in new[] {
                "/normalized/path/",
                "/normalized/./path",
                "/normalized/path/.",
                "/normalized/path/./",
                "/normalized/here/../path/"
           }) {
                Assert.IsTrue(PathEqualityComparer.Instance.Equals("/normalized/path", path),
                    $"Path: {path}, Normalized: {PathEqualityComparer.Normalize(path)}");
            }
        }

        [TestMethod]
        public void PathEqualityStartsWith() {
            Assert.IsTrue(PathEqualityComparer.Instance.StartsWith("/root/a/b", "/root"));
            Assert.IsTrue(PathEqualityComparer.Instance.StartsWith("/root/", "/root"));
            Assert.IsTrue(PathEqualityComparer.Instance.StartsWith("/notroot/../root", "/root"));
        }
    }
}
