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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Core.Threading {
    public static class SpecializedTasks {
        public static readonly Task<bool> True = Task.FromResult(true);
        public static readonly Task<bool> False = Task.FromResult(false);

        public static Task<T> Default<T>() {
            return Empty<T>.Default;
        }

        public static Task<T> DefaultOrResult<T>(T value) {
            if (EqualityComparer<T>.Default.Equals(value, default)) {
                return Default<T>();
            }

            return Task.FromResult(value);
        }

        public static Task<IReadOnlyList<T>> EmptyReadOnlyList<T>() {
            return Empty<T>.EmptyReadOnlyList;
        }

        public static Task<ImmutableArray<T>> EmptyImmutableArray<T>() {
            return Empty<T>.EmptyImmutableArray;
        }

        public static Task<IEnumerable<T>> EmptyEnumerable<T>() {
            return Empty<T>.EmptyEnumerable;
        }

        public static Task<T> FromResult<T>(T t) where T : class {
            return FromResultCache<T>.FromResult(t);
        }

        private static class Empty<T> {
            public static readonly Task<T> Default = Task.FromResult<T>(default);
            public static readonly Task<IEnumerable<T>> EmptyEnumerable = Task.FromResult(Enumerable.Empty<T>());
            public static readonly Task<ImmutableArray<T>> EmptyImmutableArray = Task.FromResult(ImmutableArray<T>.Empty);
            public static readonly Task<IReadOnlyList<T>> EmptyReadOnlyList = Task.FromResult<IReadOnlyList<T>>(ImmutableArray<T>.Empty);
        }

        private static class FromResultCache<T> where T : class {
            private static readonly ConditionalWeakTable<T, Task<T>> s_fromResultCache = new ConditionalWeakTable<T, Task<T>>();
            private static readonly ConditionalWeakTable<T, Task<T>>.CreateValueCallback s_taskCreationCallback = Task.FromResult<T>;

            public static Task<T> FromResult(T t) {
                return s_fromResultCache.GetValue(t, s_taskCreationCallback);
            }
        }
    }
}
