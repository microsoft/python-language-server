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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Core.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class DependencyResolverTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

// ReSharper disable StringLiteralTypo
        [DataRow("A:BC|B:C|C", "CBA")]
        [DataRow("C|A:BC|B:C", "CBA")]
        [DataRow("C|B:AC|A:BC", "CBABA")]
        [DataRow("A:CE|B:A|C:B|D:B|E", "[BE]CAB[DC]A")]
        [DataRow("A:D|B:DA|C:BA|D:AE|E", "[AE]DADBC")]
        [DataRow("A:C|C:B|B:A|D:AF|F:CE|E:BD", "ABCA[DB][EC]FDEF")]
        [DataRow("A:BC|B:AC|C:BA|D:BC", "ACBACBD")]
        [DataRow("A|B|C|D:AB|E:BC", "[ABC][DE]")]
        [DataRow("A:CE|B:A|C:B|D:BC|E|F:C", "[BE]CABC[FDA]")]
// ReSharper restore StringLiteralTypo
        [DataTestMethod]
        public void NotifyChanges(string input, string output) {
            var resolver = new DependencyResolver<string, string>();
            var splitInput = input.Split("|");

            var walker = default(IDependencyChainWalker<string, string>);
            foreach (var value in splitInput) {
                var kv = value.Split(":");
                var dependencies = kv.Length == 1 ? ImmutableArray<string>.Empty : ImmutableArray<string>.Create(kv[1].Select(c => c.ToString()).ToList());
                walker = resolver.NotifyChanges(value.Split(":")[0], value, dependencies);
            }

            var result = new StringBuilder();
            var tasks = new List<Task<IDependencyChainNode<string>>>();
            while (walker.Remaining > 0) {
                var nodeTask = walker.GetNextAsync(default);
                if (!nodeTask.IsCompleted) {
                    if (tasks.Count > 1) {
                        result.Append('[');
                    }

                    foreach (var task in tasks) {
                        result.Append(task.Result.Value[0]);
                        task.Result.Commit();
                    }

                    if (tasks.Count > 1) {
                        result.Append(']');
                    }

                    tasks.Clear();
                }
                tasks.Add(nodeTask);
            }

            result.ToString().Should().Be(output);
        }
        
        [TestMethod]
        public async Task NotifyChanges_RepeatedChange() {
            var resolver = new DependencyResolver<string, string>();
            resolver.NotifyChanges("A", "A:B", "B");
            resolver.NotifyChanges("B", "B:C", "C");
            var walker = resolver.NotifyChanges("C", "C");

            var result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.Commit();
            }

            result.ToString().Should().Be("CBA");

            walker = resolver.NotifyChanges("B", "B:C", "C");
            result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.Commit();
            }

            result.ToString().Should().Be("BA");
        }

        [TestMethod]
        public async Task NotifyChanges_RepeatedChange2() {
            var resolver = new DependencyResolver<string, string>();
            resolver.NotifyChanges("A", "A:B", "B");
            resolver.NotifyChanges("B", "B");
            resolver.NotifyChanges("C", "C:D", "D");
            var walker = resolver.NotifyChanges("D", "D");

            var result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.Commit();
            }

            result.ToString().Should().Be("BDAC");


            resolver.NotifyChanges("D", "D");
            walker = resolver.NotifyChanges("B", "B:C", "C");
            result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.Commit();
            }

            result.ToString().Should().Be("DCBA");
        }

        [TestMethod]
        public async Task NotifyChanges_MissingKeys() {
            var resolver = new DependencyResolver<string, string>();
            resolver.NotifyChanges("A", "A:B", "B");
            resolver.NotifyChanges("B", "B");
            var walker = resolver.NotifyChanges("C", "C:D", "D");
            
            var result = new StringBuilder();
            var node = await walker.GetNextAsync(default);
            result.Append(node.Value[0]);
            node.Commit();
            
            node = await walker.GetNextAsync(default);
            result.Append(node.Value[0]);
            node.Commit();
            
            walker.MissingKeys.Should().Equal("D");
            result.ToString().Should().Be("BC");

            walker = resolver.NotifyChanges("D", "D");
            result = new StringBuilder();
            result.Append((await walker.GetNextAsync(default)).Value[0]);
            result.Append((await walker.GetNextAsync(default)).Value[0]);
            
            walker.MissingKeys.Should().BeEmpty();
            result.ToString().Should().Be("AD");
        }

        [TestMethod]
        public async Task NotifyChanges_Skip() {
            var resolver = new DependencyResolver<string, string>();
            resolver.NotifyChanges("A", "A:B", "B");
            resolver.NotifyChanges("B", "B");
            resolver.NotifyChanges("D", "D");
            var walker = resolver.NotifyChanges("C", "C:D", "D");
            
            var result = new StringBuilder();
            var node = await walker.GetNextAsync(default);
            result.Append(node.Value[0]);
            node.Skip();
            
            node = await walker.GetNextAsync(default);
            result.Append(node.Value[0]);
            node.Skip();
            
            result.ToString().Should().Be("BD");

            walker = resolver.NotifyChanges("D", "D");
            result = new StringBuilder();
            result.Append((await walker.GetNextAsync(default)).Value[0]);
            result.Append((await walker.GetNextAsync(default)).Value[0]);

            result.ToString().Should().Be("BD");
        }
    }
}
