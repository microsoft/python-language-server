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

namespace Microsoft.Python.UnitTests.Core.MSTest {
    public class VectorAttribute : Attribute {
        public VectorAttribute(object data) {
            Data = new[] {data};
        }

        public VectorAttribute(object data, params object[] moreData) {
            if (moreData == null) {
                moreData = new object[1];
            }

            Data = new object[moreData.Length + 1];
            Data[0] = data;
            Array.Copy(moreData, 0, Data, 1, moreData.Length);
        }

        public object[] Data { get; }

        public string DisplayName { get; set; }
    }
}
