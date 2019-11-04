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

using System.Linq;
using FluentAssertions;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.UnitTests.Core.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class LocationLoopResolverTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        // ReSharper disable StringLiteralTypo
        [PermutationDataRow("A1B1", "B0C1", "C0A0")]
        [PermutationDataRow("A2B8", "B2A0", "B6C0", "C3B4")]
        // ReSharper restore StringLiteralTypo
        [DataTestMethod]
        public void FindStartingItem(params string[] input) {
            var edges = input.Select(s => (s[0], (int)s[1] - 0x30, s[2], (int)s[3] - 0x30));
            LocationLoopResolver<char>.FindStartingItems(edges).Should().Equal('A');
        }
        
        // ReSharper disable StringLiteralTypo
        [PermutationDataRow("A0B1", "B0A1")]
        [PermutationDataRow("A0B1", "B0C1", "C0A1")]
        // ReSharper restore StringLiteralTypo
        [DataTestMethod]
        public void NoStartingItem(params string[] input) {
            var edges = input.Select(s => (s[0], (int)s[1] - 0x30, s[2], (int)s[3] - 0x30));
            LocationLoopResolver<char>.FindStartingItems(edges).Should().BeEmpty();
        }

        // ReSharper disable StringLiteralTypo
        [PermutationDataRow("A2B4", "B2A0", "C3B4")]
        [PermutationDataRow("A2B4", "B2A0", "C2D4", "D2C0")]
        // ReSharper restore StringLiteralTypo
        [DataTestMethod]
        public void TwoStartingItems(params string[] input) {
            var edges = input.Select(s => (s[0], (int)s[1] - 0x30, s[2], (int)s[3] - 0x30));
            LocationLoopResolver<char>.FindStartingItems(edges).Should().BeEquivalentTo('A', 'C');
        }
    }
}
