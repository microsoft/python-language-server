﻿// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    static class EnumerableExtensions {
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
            => source == null || !source.Any();

        public static T[] MaybeEnumerate<T>(this T[] source) => source ?? Array.Empty<T>();

        public static IEnumerable<T> MaybeEnumerate<T>(this IEnumerable<T> source) => source ?? Enumerable.Empty<T>();

        private static T Identity<T>(T source) => source;

        public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> source) => source.SelectMany(Identity);

        public static IEnumerable<T> Ordered<T>(this IEnumerable<T> source)
            => source.OrderBy(Identity);

        private static bool NotNull<T>(T obj) where T : class => obj != null;

        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T> source) where T : class
            => source != null ? source.Where(NotNull) : Enumerable.Empty<T>();

        public static bool SetEquals<T>(this IEnumerable<T> source, IEnumerable<T> other,
            IEqualityComparer<T> comparer = null) where T : class {
            var set1 = new HashSet<T>(source, comparer);
            var set2 = new HashSet<T>(other, comparer);
            return set1.SetEquals(set2);
        }

        private static T GetKey<T, U>(KeyValuePair<T, U> keyValue) => keyValue.Key;

        public static IEnumerable<T> Keys<T, U>(this IEnumerable<KeyValuePair<T, U>> source) => source.Select(GetKey);

        public static IEnumerable<T> ExcludeDefault<T>(this IEnumerable<T> source) =>
            source.Where(i => !Equals(i, default(T)));

        public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate) {
            var i = 0;
            foreach (var item in source) {
                if (predicate(item)) {
                    return i;
                }

                i++;
            }

            return -1;
        }

        public static int IndexOf<T, TValue>(this IEnumerable<T> source, TValue value, Func<T, TValue, bool> predicate) {
            var i = 0;
            foreach (var item in source) {
                if (predicate(item, value)) {
                    return i;
                }

                i++;
            }

            return -1;
        }

        public static IEnumerable<int> IndexWhere<T>(this IEnumerable<T> source, Func<T, bool> predicate) {
            var i = 0;
            foreach (var item in source) {
                if (predicate(item)) {
                    yield return i;
                }

                i++;
            }
        }

        public static IEnumerable<T> TraverseBreadthFirst<T>(this T root, Func<T, IEnumerable<T>> selectChildren) {
            var items = new Queue<T>();
            items.Enqueue(root);
            while (items.Count > 0) {
                var item = items.Dequeue();
                yield return item;

                var children = selectChildren(item);
                if (children == null) {
                    continue;
                }

                foreach (var child in children) {
                    items.Enqueue(child);
                }
            }
        }
    }
}
