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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
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
        // Square brackets mean that nodes can be walked in parallel. Parentheses mean that nodes are in loop.
        [DataRow("A:BC|B:C|C", "CBA", "A")]
        [DataRow("C|A:BC|B:C", "CBA", "A")]
        [DataRow("C|B:AC|A:BC", "C(BA)", "A")]
        [DataRow("A:CE|B:A|C:B|D:B|E", "E(ACB)D", "D")]
        [DataRow("A:D|B:DA|C:BA|D:AE|E", "E(AD)BC", "C")]
        [DataRow("A:C|C:B|B:A|D:AF|F:CE|E:BD", "(ACB)(DFE)", "F")]
        [DataRow("A:BC|B:AC|C:BA|D:BC", "(ABC)D", "D")]
        [DataRow("A|B|C|D:AB|E:BC", "[ABC][DE]", "D|E")]
        [DataRow("A:CE|B:A|C:B|D:BC|E|F:C", "E(ACB)[FD]", "D|F")]
        [DataRow("A:D|B:E|C:F|D:E|E:F|F:D", "(DEF)[ABC]", "A|B|C")]
        // ReSharper restore StringLiteralTypo
        [DataTestMethod]
        public void ChangeValue(string input, string output, string root) {
            var resolver = new DependencyResolver<string, string>();
            var splitInput = input.Split("|");
            var splitRoots = root.Split("|");

            foreach (var value in splitInput) {
                var kv = value.Split(":");
                var dependencies = kv.Length == 1 ? ImmutableArray<string>.Empty : ImmutableArray<string>.Create(kv[1].Select(c => c.ToString()).ToList());
                resolver.ChangeValue(kv[0], value, splitRoots.Contains(kv[0]), dependencies);
            }

            var walker = resolver.CreateWalker();
            var result = new StringBuilder();
            var tasks = new List<Task<IDependencyChainNode>>();
            while (walker.Remaining > 0) {
                var nodeTask = walker.GetNextAsync(default);
                if (!nodeTask.IsCompleted) {
                    if (tasks.Count > 1) {
                        result.Append('[');
                    }

                    foreach (var task in tasks) {
                        AppendFirstChar(result, task.Result);
                        task.Result.MarkWalked();
                        task.Result.MoveNext();
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
        public async Task ChangeValue_ChangeToIdentical() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:B", true, "B");
            resolver.ChangeValue("B", "B:C", false, "C");
            resolver.ChangeValue("C", "C", false);
            var walker = resolver.CreateWalker();

            var result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                AppendFirstChar(result, node);
                node.Should().HaveOnlyWalkedDependencies();
                node.MarkWalked();
                node.MoveNext();
            }

            result.ToString().Should().Be("CBA");

            resolver.ChangeValue("B", "B:C", false, "C");
            walker = resolver.CreateWalker();

            result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                AppendFirstChar(result, node);
                node.Should().HaveOnlyWalkedDependencies();
                node.MarkWalked();
                node.MoveNext();
            }

            result.ToString().Should().Be("BA");
        }

        [TestMethod]
        public async Task ChangeValue_TwoChanges() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:B", true, "B");
            resolver.ChangeValue("B", "B", true);
            resolver.ChangeValue("C", "C:D", true, "D");
            resolver.ChangeValue("D", "D", false);
            var walker = resolver.CreateWalker();

            var result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                AppendFirstChar(result, node);
                node.Should().HaveOnlyWalkedDependencies();
                node.MarkWalked();
                node.MoveNext();
            }

            result.ToString().Should().Be("BDAC");

            resolver.ChangeValue("D", "D", false);
            resolver.ChangeValue("B", "B:C", true, "C");

            walker = resolver.CreateWalker();
            result = new StringBuilder();
            while (walker.Remaining > 0) {
                var node = await walker.GetNextAsync(default);
                AppendFirstChar(result, node);
                node.Should().HaveOnlyWalkedDependencies();
                node.MarkWalked();
                node.MoveNext();
            }

            result.ToString().Should().Be("DCBA");
        }

        [TestMethod]
        public async Task ChangeValue_MissingKeys() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:B", true, "B");
            resolver.ChangeValue("B", "B", false);
            resolver.ChangeValue("C", "C:D", true, "D");
            var walker = resolver.CreateWalker();
            
            var node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("B")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("C:D")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.MissingKeys.Should().Equal("D");

            resolver.ChangeValue("D", "D", false);
            walker = resolver.CreateWalker();
            walker.MissingKeys.Should().BeEmpty();
            
            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:B");
            
            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("D");
        }

        [TestMethod]
        public async Task ChangeValue_Add() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:BD", true, "B", "D");
            resolver.ChangeValue("C", "C", false);

            var walker = resolver.CreateWalker();
            walker.MissingKeys.Should().Equal("B", "D");
            var node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BD")
                .And.HaveMissingDependencies()
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);

            // Add B
            resolver.ChangeValue("B", "B", false);
            walker = resolver.CreateWalker();       
            walker.MissingKeys.Should().Equal("D");
                
            node = await walker.GetNextAsync(default); 
            node.Should().HaveSingleValue("B")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BD")
                .And.HaveMissingDependencies()
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0); 
                
            // Add D
            resolver.ChangeValue("D", "D:C", false, "C");
            walker = resolver.CreateWalker();       
            walker.MissingKeys.Should().BeEmpty();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("C")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("D:C")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default); 
            node.Should().HaveSingleValue("A:BD")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);
        }

        [TestMethod]
        public async Task ChangeValue_Add_ParallelWalkers() { 
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:BD", true, "B", "D");
            resolver.ChangeValue("B", "B:C", false, "C");
            resolver.ChangeValue("C", "C", false);

            var walker = resolver.CreateWalker();
            walker.MissingKeys.Should().Equal("D");

            var node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("C")
                .And.HaveNoMissingDependencies()
                .And.HaveValidVersion();

            // Add D
            resolver.ChangeValue("D", "D:C", false, "C");
            var newWalker = resolver.CreateWalker();
            newWalker.MissingKeys.Should().BeEmpty();

            // MarkWalked node from old walker
            node.Should().HaveOnlyWalkedDependencies()
                .And.HaveInvalidVersion();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("B:C")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies()
                .And.HaveInvalidVersion();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BD")
                .And.HaveMissingDependencies()
                .And.HaveOnlyWalkedDependencies()
                .And.HaveInvalidVersion();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);

            // Walk new walker
            node = await newWalker.GetNextAsync(default);
            node.Should().HaveSingleValue("C")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies()
                .And.HaveValidVersion();
            node.MarkWalked();
            node.MoveNext();

            node = await newWalker.GetNextAsync(default);
            node.Should().HaveSingleValue("B:C")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies()
                .And.HaveValidVersion();
            node.MarkWalked();
            node.MoveNext();

            node = await newWalker.GetNextAsync(default);
            node.Should().HaveSingleValue("D:C")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies()
                .And.HaveValidVersion();
            node.MarkWalked();
            node.MoveNext();

            node = await newWalker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BD")
                .And.HaveNoMissingDependencies()
                .And.HaveOnlyWalkedDependencies()
                .And.HaveValidVersion();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);
        }

        [TestMethod]
        public async Task ChangeValue_PartiallyWalkLoop() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:B", true, "B");
            resolver.ChangeValue("B", "B:CE", false, "C", "E");
            resolver.ChangeValue("C", "C:DE", false, "D", "E");
            resolver.ChangeValue("D", "D:BE", false, "B", "E");
            resolver.ChangeValue("E", "E", false);

            var walker = resolver.CreateWalker();
            walker.MissingKeys.Should().BeEmpty();

            var node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("E")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveLoopValues("B:CE", "C:DE", "D:BE")
                .And.HaveNonWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            // Create new walker
            var newWalker = resolver.CreateWalker();

            // Mark vertex walked as it would've been in parallel
            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:B")
                .And.HaveNonWalkedDependencies()
                .And.NotBeWalkedWithDependencies();
            node.MarkWalked();
            node.MoveNext();

            // Now iterate with new walker
            node = await newWalker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:B")
                .And.HaveOnlyWalkedDependencies()
                .And.BeWalkedWithDependencies();
            node.MarkWalked();
            node.MoveNext();

            newWalker.Remaining.Should().Be(0);
        }

        [TestMethod]
        public async Task ChangeValue_Remove() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:BC", true, "B", "C");
            resolver.ChangeValue("B", "B:C", false, "C");
            resolver.ChangeValue("C", "C", false);

            var walker = resolver.CreateWalker();
            walker.MissingKeys.Should().BeEmpty();
            var node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("C")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("B:C")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BC")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            resolver.Remove("B");
            walker = resolver.CreateWalker();
            walker.MissingKeys.Should().Equal("B");

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BC")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);
        }

        [TestMethod]
        public async Task ChangeValue_ChangeChangeRemove() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:B", true, "B");
            resolver.ChangeValue("B", "B:C", true, "C");
            resolver.ChangeValue("C", "C:AD", true, "A", "D");

            var walker = resolver.CreateWalker();
            walker.MissingKeys.Should().Equal("D");
            walker.AffectedValues.Should().Equal("A:B", "B:C", "C:AD");
            walker.Remaining.Should().Be(3);

            //resolver.ChangeValue("D", "D:B", true, "B");
            resolver.ChangeValue("A", "A", true);
            resolver.ChangeValue("B", "B", true);
            resolver.Remove("B");

            walker = resolver.CreateWalker();
            walker.MissingKeys.Should().Equal("D");
            walker.Remaining.Should().Be(2);

            var node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("C:AD")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);
        }

        [TestMethod]
        public async Task ChangeValue_RemoveFromLoop() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:B", true, "B");
            resolver.ChangeValue("B", "B:C", false, "C");
            resolver.ChangeValue("C", "C:A", false, "A");

            var walker = resolver.CreateWalker();
            walker.MissingKeys.Should().BeEmpty();

            var node = await walker.GetNextAsync(default);
            node.Should().HaveLoopValues("A:B", "B:C", "C:A")
                .And.HaveNonWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();
            
            walker.Remaining.Should().Be(0);

            resolver.Remove("B");
            walker = resolver.CreateWalker();
            walker.MissingKeys.Should().Equal("B");

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:B")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);
        }

        [TestMethod]
        public async Task ChangeValue_RemoveKeys() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:BC", true, "B", "C");
            resolver.ChangeValue("B", "B:C", false, "C");
            resolver.ChangeValue("C", "C:D", false, "D");
            resolver.ChangeValue("D", "D", false);

            var walker = resolver.CreateWalker();
            walker.MissingKeys.Should().BeEmpty();
            var node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("D")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("C:D")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("B:C")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BC")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            resolver.RemoveKeys("B", "D");
            walker = resolver.CreateWalker();
            walker.MissingKeys.Should().Equal("B", "D");

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("C:D")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            node = await walker.GetNextAsync(default);
            node.Should().HaveSingleValue("A:BC")
                .And.HaveOnlyWalkedDependencies();
            node.MarkWalked();
            node.MoveNext();

            walker.Remaining.Should().Be(0);
        }

        [TestMethod]
        public async Task ChangeValue_Skip() {
            var resolver = new DependencyResolver<string, string>();
            resolver.ChangeValue("A", "A:B", true, "B");
            resolver.ChangeValue("B", "B", false);
            resolver.ChangeValue("D", "D", false);
            resolver.ChangeValue("C", "C:D", true, "D");

            var walker = resolver.CreateWalker();
            var result = new StringBuilder();
            var node = await walker.GetNextAsync(default);
            AppendFirstChar(result, node);
            node.MoveNext();
                
            node = await walker.GetNextAsync(default);
            AppendFirstChar(result, node);
            node.MoveNext();
                
            result.ToString().Should().Be("BD");

            resolver.ChangeValue("D", "D", false);
            walker = resolver.CreateWalker();
            result = new StringBuilder();
            AppendFirstChar(result, await walker.GetNextAsync(default));
            AppendFirstChar(result, await walker.GetNextAsync(default));

            result.ToString().Should().Be("BD");
        }

        private static StringBuilder AppendFirstChar(StringBuilder sb, IDependencyChainNode node) {
            switch (node) {
                case IDependencyChainSingleNode<string> single:
                    return sb.Append(single.Value[0]);
                case IDependencyChainLoopNode<string> loop:
                    return sb.Append($"({new string(loop.Values.Select(v => v[0]).ToArray())})");
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
