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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using FluentAssertions;
using Microsoft.Python.Core.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Python.Core.Tests {
    [TestClass]
    public class ImmutableArrayTests {
        [TestMethod, Priority(0)]
        public void ImmutableArray_Empty() {
            var arr = ImmutableArray<int>.Empty;
            arr.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public void ImmutableArray_Add() {
            var arr = ImmutableArray<int>.Empty.Add(0);
            arr.Should().Equal(0);

            var arr2 = arr.Add(1);
            arr.Should().Equal(0);
            arr2.Should().Equal(0, 1);

            var arr3 = arr.Add(2);
            arr.Should().Equal(0);
            arr2.Should().Equal(0, 1);
            arr3.Should().Equal(0, 2);
        }

        [TestMethod, Priority(0)]
        public void ImmutableArray_AddRange() {
            var arr = ImmutableArray<int>.Empty.AddRange(new [] { 0, 1 });
            arr.Should().Equal(0, 1);

            var arr2 = ImmutableArray<int>.Empty.AddRange(new[] { 2, 3 });
            arr.Should().Equal(0, 1);
            arr2.Should().Equal(2, 3);

            var arr3 = arr.AddRange(arr2);
            arr.Should().Equal(0, 1);
            arr2.Should().Equal(2, 3);
            arr3.Should().Equal(0, 1, 2, 3);

            var arr4 = arr.Add(4);
            arr.Should().Equal(0, 1);
            arr2.Should().Equal(2, 3);
            arr3.Should().Equal(0, 1, 2, 3);
            arr4.Should().Equal(0, 1, 4);
        }

        [TestMethod, Priority(0)]
        public void ImmutableArray_Remove() {
            var arr = ImmutableArray<int>.Empty.AddRange(new[] { 1, 0 });
            var arr2 = arr.RemoveAt(0);
            var arr3 = arr.Remove(0);

            arr.Should().Equal(1, 0);
            arr2.Should().Equal(0);
            arr3.Should().Equal(1);
        }

        [TestMethod, Priority(0)]
        public void ImmutableArray_ReplaceAt() {
            var array = new[] {0, 1, 2, 3, 4, 5};
            var arr = ImmutableArray<int>.Create(array);
            var arr2 = arr.ReplaceAt(1, 3, 6);
            var arr3 = arr.ReplaceAt(0, 1, 6);
            var arr4 = arr.ReplaceAt(0, 2, 6);
            var arr5 = arr.ReplaceAt(4, 2, 6);
            var arr6 = arr.ReplaceAt(1, 4, 6);

            arr.Should().Equal(array);
            arr2.Should().Equal(0, 6, 4, 5);
            arr3.Should().Equal(6, 1, 2, 3, 4, 5);
            arr4.Should().Equal(6, 2, 3, 4, 5);
            arr5.Should().Equal(0, 1, 2, 3, 6);
            arr6.Should().Equal(0, 6, 5);
        }
    }
}
