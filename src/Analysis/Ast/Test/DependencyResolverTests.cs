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

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class DependencyResolverTests {
    //    public TestContext TestContext { get; set; }

    //    [TestInitialize]
    //    public void TestInitialize()
    //        => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

    //    [TestCleanup]
    //    public void Cleanup() => TestEnvironmentImpl.TestCleanup();

    //    // ReSharper disable StringLiteralTypo
    //    [DataRow("A:BC|B:C|C", "CBA", "A")]
    //    [DataRow("C|A:BC|B:C", "CBA", "A")]
    //    [DataRow("C|B:AC|A:BC", "CBABA", "A")]
    //    [DataRow("A:CE|B:A|C:B|D:B|E", "[CE]ABCABD", "D")]
    //    [DataRow("A:D|B:DA|C:BA|D:AE|E", "[AE]DADBC", "C")]
    //    [DataRow("A:C|C:B|B:A|D:AF|F:CE|E:BD", "ABCABCDEFDEF", "F")]
    //    [DataRow("A:BC|B:AC|C:BA|D:BC", "ACBACBD", "D")]
    //    [DataRow("A|B|C|D:AB|E:BC", "[ABC][DE]", "D|E")]
    //    [DataRow("A:CE|B:A|C:B|D:BC|E|F:C", "[CE]ABCAB[DF]", "D|F")]
    //    [DataRow("A:D|B:E|C:F|D:E|E:F|F:D", "DFEDFE[ABC]", "A|B|C")]
    //    // ReSharper restore StringLiteralTypo
    //    [DataTestMethod]
    //    public void ChangeValue(string input, string output, string root) {
    //        var resolver = new DependencyResolver<string, string>();
    //        var splitInput = input.Split("|");
    //        var splitRoots = root.Split("|");

    //        foreach (var value in splitInput) {
    //            var kv = value.Split(":");
    //            var dependencies = kv.Length == 1 ? ImmutableArray<string>.Empty : ImmutableArray<string>.Create(kv[1].Select(c => c.ToString()).ToList());
    //            resolver.ChangeValue(kv[0], value, splitRoots.Contains(kv[0]), dependencies);
    //        }

    //        var walker = resolver.CreateWalker();
    //        var result = new StringBuilder();
    //        var tasks = new List<Task<IDependencyChainNode<string>>>();
    //        while (walker.Remaining > 0) {
    //            var nodeTask = walker.GetNextAsync(default);
    //            if (!nodeTask.IsCompleted) {
    //                if (tasks.Count > 1) {
    //                    result.Append('[');
    //                }

    //                foreach (var task in tasks) {
    //                    result.Append(task.Result.Value[0]);
    //                    task.Result.MarkWalked();
    //                    task.Result.MoveNext();
    //                }

    //                if (tasks.Count > 1) {
    //                    result.Append(']');
    //                }

    //                tasks.Clear();
    //            }
    //            tasks.Add(nodeTask);
    //        }

    //        result.ToString().Should().Be(output);
    //    }
        
    //    [TestMethod]
    //    public async Task ChangeValue_ChangeToIdentical() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:B", true, "B");
    //        resolver.ChangeValue("B", "B:C", false, "C");
    //        resolver.ChangeValue("C", "C", false);
    //        var walker = resolver.CreateWalker();

    //        var result = new StringBuilder();
    //        while (walker.Remaining > 0) {
    //            var node = await walker.GetNextAsync(default);
    //            result.Append(node.Value[0]);
    //            node.HasOnlyWalkedDependencies.Should().BeTrue();
    //            node.MarkWalked();
    //            node.MoveNext();
    //        }

    //        result.ToString().Should().Be("CBA");

    //        resolver.ChangeValue("B", "B:C", false, "C");
    //        walker = resolver.CreateWalker();

    //        result = new StringBuilder();
    //        while (walker.Remaining > 0) {
    //            var node = await walker.GetNextAsync(default);
    //            result.Append(node.Value[0]);
    //            node.HasOnlyWalkedDependencies.Should().BeTrue();
    //            node.MarkWalked();
    //            node.MoveNext();
    //        }

    //        result.ToString().Should().Be("BA");
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_TwoChanges() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:B", true, "B");
    //        resolver.ChangeValue("B", "B", true);
    //        resolver.ChangeValue("C", "C:D", true, "D");
    //        resolver.ChangeValue("D", "D", false);
    //        var walker = resolver.CreateWalker();

    //        var result = new StringBuilder();
    //        while (walker.Remaining > 0) {
    //            var node = await walker.GetNextAsync(default);
    //            result.Append(node.Value[0]);
    //            node.HasOnlyWalkedDependencies.Should().BeTrue();
    //            node.MarkWalked();
    //            node.MoveNext();
    //        }

    //        result.ToString().Should().Be("BDAC");

    //        resolver.ChangeValue("D", "D", false);
    //        resolver.ChangeValue("B", "B:C", true, "C");

    //        walker = resolver.CreateWalker();
    //        result = new StringBuilder();
    //        while (walker.Remaining > 0) {
    //            var node = await walker.GetNextAsync(default);
    //            result.Append(node.Value[0]);
    //            node.HasOnlyWalkedDependencies.Should().BeTrue();
    //            node.MarkWalked();
    //            node.MoveNext();
    //        }

    //        result.ToString().Should().Be("DCBA");
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_MissingKeys() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:B", true, "B");
    //        resolver.ChangeValue("B", "B", false);
    //        resolver.ChangeValue("C", "C:D", true, "D");
    //        var walker = resolver.CreateWalker();

    //        var result = new StringBuilder();
    //        var node = await walker.GetNextAsync(default);
    //        result.Append(node.Value[0]);
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        result.Append(node.Value[0]);
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.MissingKeys.Should().Equal("D");
    //        result.ToString().Should().Be("BC");

    //        resolver.ChangeValue("D", "D", false);
    //        walker = resolver.CreateWalker();
    //        result = new StringBuilder();
    //        result.Append((await walker.GetNextAsync(default)).Value[0]);
    //        result.Append((await walker.GetNextAsync(default)).Value[0]);
            
    //        walker.MissingKeys.Should().BeEmpty();
    //        result.ToString().Should().Be("AD");
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_Add() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:BD", true, "B", "D");
    //        resolver.ChangeValue("C", "C", false);

    //        var walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().Equal("B", "D");
    //        var node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:BD");
    //        node.HasMissingDependencies.Should().BeTrue();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);

    //        // Add B
    //        resolver.ChangeValue("B", "B", false);
    //        walker = resolver.CreateWalker();       
    //        walker.MissingKeys.Should().Equal("D");
            
    //        node = await walker.GetNextAsync(default); 
    //        node.Value.Should().Be("B");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default); 
    //        node.Value.Should().Be("A:BD");
    //        node.HasMissingDependencies.Should().BeTrue();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0); 
            
    //        // Add D
    //        resolver.ChangeValue("D", "D:C", false, "C");
    //        walker = resolver.CreateWalker();       
    //        walker.MissingKeys.Should().BeEmpty();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default); 
    //        node.Value.Should().Be("D:C");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default); 
    //        node.Value.Should().Be("A:BD");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_Add_ParallelWalkers() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:BD", true, "B", "D");
    //        resolver.ChangeValue("B", "B:C", false, "C");
    //        resolver.ChangeValue("C", "C", false);

    //        var walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().Equal("D");

    //        var node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.IsValidVersion.Should().BeTrue();

    //        // Add D
    //        resolver.ChangeValue("D", "D:C", false, "C");
    //        var newWalker = resolver.CreateWalker();
    //        newWalker.MissingKeys.Should().BeEmpty();

    //        // MarkWalked node from old walker
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsValidVersion.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("B:C");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsValidVersion.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:BD");
    //        node.HasMissingDependencies.Should().BeTrue();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsValidVersion.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);

    //        // Walk new walker
    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("C");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsValidVersion.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("B:C");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsValidVersion.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("D:C");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsValidVersion.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("A:BD");
    //        node.HasMissingDependencies.Should().BeFalse();
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsValidVersion.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_PartiallyWalkLoop() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:B", true, "B");
    //        resolver.ChangeValue("B", "B:CE", false, "C", "E");
    //        resolver.ChangeValue("C", "C:DE", false, "D", "E");
    //        resolver.ChangeValue("D", "D:BE", false, "B", "E");
    //        resolver.ChangeValue("E", "E", false);

    //        var walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().BeEmpty();

    //        var node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("E");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("B:CE");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("D:BE");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C:DE");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        // Create new walker
    //        var newWalker = resolver.CreateWalker();

    //        // Mark vertex walked as it would've in parallel
    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("B:CE");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsWalkedWithDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        // Now iterate with new walker
    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("B:CE");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.IsWalkedWithDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("D:BE");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.IsWalkedWithDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("C:DE");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.IsWalkedWithDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("B:CE");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsWalkedWithDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("D:BE");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsWalkedWithDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("C:DE");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsWalkedWithDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await newWalker.GetNextAsync(default);
    //        node.Value.Should().Be("A:B");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.IsWalkedWithDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        newWalker.Remaining.Should().Be(0);
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_Remove() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:BC", true, "B", "C");
    //        resolver.ChangeValue("B", "B:C", false, "C");
    //        resolver.ChangeValue("C", "C", false);

    //        var walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().BeEmpty();
    //        var node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("B:C");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:BC");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        resolver.Remove("B");
    //        walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().Equal("B");

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:BC");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_ChangeChangeRemove() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:B", true, "B");
    //        resolver.ChangeValue("B", "B:C", true, "C");
    //        resolver.ChangeValue("C", "C:AD", true, "A", "D");

    //        var walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().Equal("D");
    //        walker.AffectedValues.Should().Equal("A:B", "B:C", "C:AD");
    //        walker.Remaining.Should().Be(6);

    //        //resolver.ChangeValue("D", "D:B", true, "B");
    //        resolver.ChangeValue("A", "A", true);
    //        resolver.ChangeValue("B", "B", true);
    //        resolver.Remove("B");

    //        walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().Equal("D");
    //        walker.Remaining.Should().Be(2);

    //        var node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C:AD");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_RemoveFromLoop() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:B", true, "B");
    //        resolver.ChangeValue("B", "B:C", false, "C");
    //        resolver.ChangeValue("C", "C:A", false, "A");

    //        var walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().BeEmpty();

    //        var node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:B");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C:A");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("B:C");
    //        node.HasOnlyWalkedDependencies.Should().BeFalse();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:B");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C:A");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("B:C");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);

    //        resolver.Remove("B");
    //        walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().Equal("B");

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:B");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_RemoveKeys() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:BC", true, "B", "C");
    //        resolver.ChangeValue("B", "B:C", false, "C");
    //        resolver.ChangeValue("C", "C:D", false, "D");
    //        resolver.ChangeValue("D", "D", false);

    //        var walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().BeEmpty();
    //        var node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("D");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C:D");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("B:C");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:BC");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        resolver.RemoveKeys("B", "D");
    //        walker = resolver.CreateWalker();
    //        walker.MissingKeys.Should().Equal("B", "D");

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("C:D");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        node = await walker.GetNextAsync(default);
    //        node.Value.Should().Be("A:BC");
    //        node.HasOnlyWalkedDependencies.Should().BeTrue();
    //        node.MarkWalked();
    //        node.MoveNext();

    //        walker.Remaining.Should().Be(0);
    //    }

    //    [TestMethod]
    //    public async Task ChangeValue_Skip() {
    //        var resolver = new DependencyResolver<string, string>();
    //        resolver.ChangeValue("A", "A:B", true, "B");
    //        resolver.ChangeValue("B", "B", false);
    //        resolver.ChangeValue("D", "D", false);
    //        resolver.ChangeValue("C", "C:D", true, "D");

    //        var walker = resolver.CreateWalker();
    //        var result = new StringBuilder();
    //        var node = await walker.GetNextAsync(default);
    //        result.Append(node.Value[0]);
    //        node.MoveNext();
            
    //        node = await walker.GetNextAsync(default);
    //        result.Append(node.Value[0]);
    //        node.MoveNext();
            
    //        result.ToString().Should().Be("BD");

    //        resolver.ChangeValue("D", "D", false);
    //        walker = resolver.CreateWalker();
    //        result = new StringBuilder();
    //        result.Append((await walker.GetNextAsync(default)).Value[0]);
    //        result.Append((await walker.GetNextAsync(default)).Value[0]);

    //        result.ToString().Should().Be("BD");
    //    }
    }
}
