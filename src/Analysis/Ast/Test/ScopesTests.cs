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

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Core.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ScopesTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task FindScope() {
            const string code = @"
a
class A:
    x: int
    def method(self):
        
        b = 1
        
    
    aa

def func(x, y):
    a
    def inner(c):
        z
        

    

";
            var analysis = await GetAnalysisAsync(code);
            var gs = analysis.GlobalScope;

            var locations = new[] {
                (new SourceLocation(2, 1),   "<module>"),
                (new SourceLocation(2, 2),   "<module>"),
                (new SourceLocation(3, 1),   "<module>"),
                (new SourceLocation(3, 3),   "<module>"),
                (new SourceLocation(4, 11),  "A"),
                (new SourceLocation(5, 1),   "A"),
                (new SourceLocation(5, 5),   "A"),
                (new SourceLocation(5, 6),   "A"),
                (new SourceLocation(5, 17),  "method"),
                (new SourceLocation(6, 9),   "method"),
                (new SourceLocation(7, 9),   "method"),
                (new SourceLocation(7, 14),  "method"),
                (new SourceLocation(8, 9),   "method"),
                (new SourceLocation(9, 5),   "A"),
                (new SourceLocation(10, 5),  "A"),
                (new SourceLocation(10, 7),  "A"),
                (new SourceLocation(11, 1),  "<module>"),
                (new SourceLocation(12, 1),  "<module>"),
                (new SourceLocation(12, 11), "func"),
                (new SourceLocation(13, 5),  "func"),
                (new SourceLocation(13, 6),  "func"),
                (new SourceLocation(14, 5),  "func"),
                (new SourceLocation(14, 15), "inner"),
                (new SourceLocation(15, 9),  "inner"),
                (new SourceLocation(15, 10), "inner"),
                (new SourceLocation(16, 9),  "inner"),
                (new SourceLocation(17, 5),  "func"),
                (new SourceLocation(18, 1),  "<module>")
            };

            foreach (var loc in locations) {
                var scope = gs.FindScope(analysis.Document, loc.Item1);
                scope.Name.Should().Be(loc.Item2, $"location {loc.Item1.Line}, {loc.Item1.Column}");
            }
        }

        [TestMethod, Priority(0)]
        public async Task EmptyLines() {
            const string code = @"
class A:
    x: int
    def method(self):

";
            var analysis = await GetAnalysisAsync(code);
            var gs = analysis.GlobalScope;

            var locations = new[] {
                (new SourceLocation(5, 1), "<module>"),
                (new SourceLocation(5, 5), "A"),
                (new SourceLocation(5, 9), "method")
            };

            foreach (var loc in locations) {
                var scope = gs.FindScope(analysis.Document, loc.Item1);
                scope.Name.Should().Be(loc.Item2, $"location {loc.Item1.Line}, {loc.Item1.Column}");
            }
        }
    }
}
