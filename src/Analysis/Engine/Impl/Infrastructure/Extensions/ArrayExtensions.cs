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

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    internal static class ArrayExtensions {
        public static T[] ImmutableAdd<T>(this T[] array, in T item) {
            if (array.Length == 0) {
                return new[] {item};
            }

            var copy = new T[array.Length + 1];
            Array.Copy(array, copy, array.Length);
            copy[array.Length] = item;
            return copy;
        }

        public static T[] ImmutableReplaceAt<T>(this T[] array, in T item, in int index) {
            var copy = new T[array.Length];
            Array.Copy(array, copy, array.Length);
            copy[index] = item;
            return copy;
        }

        public static T[] ImmutableRemoveAt<T>(this T[] array, in int index) {
            var copy = new T[array.Length - 1];
            if (index > 0) {
                Array.Copy(array, copy, index);
            }

            if (index < copy.Length) {
                Array.Copy(array, index + 1, copy, index, copy.Length - index);
            }

            return copy;
        }
    }
}