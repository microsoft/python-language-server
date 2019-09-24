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
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Python.UnitTests.Core.MSTest {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PermutationDataRowAttribute : Attribute, ITestDataSource {
        private readonly object[] _source;

        public PermutationDataRowAttribute() {
            _source = new object[0];
        }

        public PermutationDataRowAttribute(object data) {
            _source = new object[1] {data};
        }

        public PermutationDataRowAttribute(object data, params object[] moreData) {
            if (moreData == null) {
                moreData = new object[1];
            }

            _source = new object[moreData.Length + 1];
            _source[0] = data;
            Array.Copy(moreData, 0, _source, 1, moreData.Length);
        }

        public IEnumerable<object[]> GetData(MethodInfo methodInfo) {
            var permutationsIndexes = GetPermutationIndexes(_source.Length);
            var data = new object[permutationsIndexes.Length][];
            for (var dataIndex = 0; dataIndex < permutationsIndexes.Length; dataIndex++) {
                var permutationIndexes = permutationsIndexes[dataIndex];
                var dataRow = new object[_source.Length];
                for (var i = 0; i < dataRow.Length; i++) {
                    dataRow[i] = _source[permutationIndexes[i]];
                }

                data[dataIndex] = dataRow;
            }

            return data;
        }
        
        public string GetDisplayName(MethodInfo methodInfo, object[] data) 
            => data == null ? null : string.Format(CultureInfo.InvariantCulture, "{0} ({1})", methodInfo.Name, string.Join(", ", data));

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
