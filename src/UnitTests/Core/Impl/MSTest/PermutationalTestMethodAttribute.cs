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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Python.UnitTests.Core.MSTest {
    [AttributeUsage(AttributeTargets.Method)]
    public class PermutationalTestMethodAttribute : DataTestMethodAttribute, ITestDataSource {
        private readonly int _count;
        private readonly int[] _fixedPermutation;

        public string NameFormat { get; set; } = "{0} ({1})";

        public PermutationalTestMethodAttribute(int count, params int[] fixedPermutation) {
            _count = count;
            _fixedPermutation = fixedPermutation;
        }

        public IEnumerable<object[]> GetData(MethodInfo methodInfo) {
            if (_fixedPermutation != null && _fixedPermutation.Length > 0) {
                yield return new object[] { _fixedPermutation };
                yield break;
            }

            var permutationsIndexes = GetPermutationIndexes(_count);
            foreach (var permutationIndexes in permutationsIndexes) {
                yield return new object []{ permutationIndexes };
            }
        }

        public string GetDisplayName(MethodInfo methodInfo, object[] data) {
            var names = string.Join(", ", (int[])data[0]);
            return string.Format(CultureInfo.InvariantCulture, NameFormat, methodInfo.Name, names);
        }

        private int[][] GetPermutationIndexes(int count) {
            if (count == 0) {
                return Array.Empty<int[]>();
            }

            if (count == 1) {
                return new[] {new[] {0}};
            }

            var permutationMinusOne = GetPermutationIndexes(count - 1);
            var permutations = new int[permutationMinusOne.Length * count][];

            for (var i = 0; i < permutationMinusOne.Length; i++) {
                for (var j = 0; j < count; j++) {
                    permutations[i * count + j] = MakePermutationPlusOne(permutationMinusOne[i], count - j - 1);
                }
            }

            return permutations;
        }

        private int[] MakePermutationPlusOne(int[] permutation, int insertIndex) {
            var permutationPlusOne = new int[permutation.Length + 1];
            Array.Copy(permutation, 0, permutationPlusOne, 0, insertIndex);
            permutationPlusOne[insertIndex] = permutation.Length;
            Array.Copy(permutation, insertIndex, permutationPlusOne, insertIndex + 1, permutation.Length - insertIndex);

            return permutationPlusOne;
        }
    }
}