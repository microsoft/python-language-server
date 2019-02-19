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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Microsoft.Python.Core.Collections {
    /// <summary>
    /// This type is a compromise between an array (fast access, slow and expensive copying for every immutable change) and binary tree based immutable types
    /// Access is almost as fast as in array, adding is as fast as in List (almost identical implementation),
    /// setting new value and removal of anything but last element always requires full array copying. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ImmutableArray<T> : IReadOnlyList<T>, IEquatable<ImmutableArray<T>> {
        private readonly T[] _items;

        private ImmutableArray(T[] items, int count) {
            _items = items;
            Count = count;
        }

        public static ImmutableArray<T> Empty { get; } = new ImmutableArray<T>(Array.Empty<T>(), 0);

        public static ImmutableArray<T> Create(T item) {
            var items = new T[1];
            items[0] = item;
            return new ImmutableArray<T>(items, 1);
        }

        public static ImmutableArray<T> Create(T[] array) {
            var items = new T[array.Length];
            Array.Copy(array, items, array.Length);
            return new ImmutableArray<T>(items, items.Length);
        }

        public static ImmutableArray<T> Create(List<T> list) {
            var items = new T[list.Count];
            list.CopyTo(items);
            return new ImmutableArray<T>(items, items.Length);
        }

        public static ImmutableArray<T> Create(HashSet<T> hashSet) {
            var items = new T[hashSet.Count];
            hashSet.CopyTo(items);
            return new ImmutableArray<T>(items, items.Length);
        }

        public static ImmutableArray<T> Create<TKey>(Dictionary<TKey, T>.ValueCollection collection) {
            var items = new T[collection.Count];
            collection.CopyTo(items, 0);
            return new ImmutableArray<T>(items, items.Length);
        }

        public T this[int index] => _items[index];
        public int Count { get; } // Length of the ImmutableArray.

        [Pure]
        public ImmutableArray<T> Add(T item) {
            var newCount = Count + 1;
            var newItems = _items;

            if (newCount > _items.Length) {
                var capacity = GetCapacity(newCount);
                newItems = new T[capacity];
                Array.Copy(_items, 0, newItems, 0, Count);
            }

            newItems[Count] = item;
            return new ImmutableArray<T>(newItems, newCount);
        }

        [Pure]
        public ImmutableArray<T> AddRange(T[] items) {
            if (items.Length == 0) {
                return this;
            }

            var newCount = Count + items.Length;
            var newItems = _items;

            if (newCount > _items.Length) {
                var capacity = GetCapacity(newCount);
                newItems = new T[capacity];
                Array.Copy(_items, 0, newItems, 0, Count);
            }

            Array.Copy(items, 0, newItems, Count, items.Length);
            return new ImmutableArray<T>(newItems, newCount);
        }

        [Pure]
        public ImmutableArray<T> Remove(T value) {
            var index = IndexOf(value);
            return index >= 0 ? RemoveAt(index) : this;
        }

        [Pure]
        public ImmutableArray<T> RemoveAt(int index) {
            var newCount = Count - 1;

            var capacity = GetCapacity(newCount);
            var newArray = new T[capacity];

            if (index > 0) {
                Array.Copy(_items, newArray, index);
            }

            if (index < newCount) {
                Array.Copy(_items, index + 1, newArray, index, newCount - index);
            }

            return new ImmutableArray<T>(newArray, newCount);
        }

        [Pure]
        public ImmutableArray<T> InsertAt(int index, T value) {
            if (index > Count) {
                throw new IndexOutOfRangeException();
            }

            if (index == Count) {
                return Add(value);
            }

            var newCount = Count + 1;
            var capacity = GetCapacity(newCount);
            var newArray = new T[capacity];

            if (index > 0) {
                Array.Copy(_items, newArray, index);
            }

            newArray[index] = value;
            Array.Copy(_items, index, newArray, index + 1, Count - index);

            return new ImmutableArray<T>(newArray, newCount);
        }

        [Pure]
        public ImmutableArray<T> ReplaceAt(int startIndex, int length, T value) {
            if (length == 0) {
                return InsertAt(startIndex, value);
            }

            if (length == 1) {
                return ReplaceAt(startIndex, value);
            }

            var newCount = Math.Max(Count - length + 1, startIndex + 1);
            var capacity = GetCapacity(newCount);
            var newArray = new T[capacity];
            
            if (startIndex > 0) {
                Array.Copy(_items, newArray, startIndex);
            }

            newArray[startIndex + 1] = value;

            if (startIndex + 2 < newCount) {
                Array.Copy(_items, startIndex + length + 1, newArray, startIndex + 1, newCount - startIndex - 2);
            }

            return new ImmutableArray<T>(newArray, newCount);
        }

        [Pure]
        public ImmutableArray<T> ReplaceAt(int index, T value) {
            var capacity = GetCapacity(Count);
            var newArray = new T[capacity];
            Array.Copy(_items, newArray, Count);
            newArray[index] = value;
            return new ImmutableArray<T>(newArray, Count);
        }

        [Pure]
        public ImmutableArray<T> Where(Func<T, bool> predicate) {
            var count = 0;
            for (var i = 0; i < Count; i++) {
                if (predicate(_items[i])) {
                    count++;
                }
            }

            var index = 0;
            var items = new T[count];
            for (var i = 0; i < Count; i++) {
                if (predicate(_items[i])) {
                    items[index] = _items[i];
                    index++;
                }
            }

            return new ImmutableArray<T>(items, items.Length);
        }

        [Pure]
        public ImmutableArray<TResult> Select<TResult>(Func<T, TResult> selector) {
            var items = new TResult[Count];
            for (var i = 0; i < Count; i++) {
                items[i] = selector(_items[i]);
            }
            return new ImmutableArray<TResult>(items, items.Length);
        }

        [Pure]
        public int IndexOf(T value) => Array.IndexOf(_items, value, 0, Count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCapacity(int length) {
            var capacity = _items.Length;

            if (capacity == 0) {
                capacity = 4;
            }

            while (length > capacity) {
                capacity = capacity * 2;
            }

            while (length < capacity / 2 && capacity > 4) {
                capacity = capacity / 2;
            }

            return capacity;
        }

        public bool Equals(ImmutableArray<T> other)
            => Equals(_items, other._items) && Count == other.Count;

        public override bool Equals(object obj) => obj is ImmutableArray<T> other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                var hashCode = (_items != null ? _items.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Count;
                return hashCode;
            }
        }

        public static bool operator ==(ImmutableArray<T> left, ImmutableArray<T> right) {
            return left.Equals(right);
        }

        public static bool operator !=(ImmutableArray<T> left, ImmutableArray<T> right) {
            return !left.Equals(right);
        }

        public Enumerator GetEnumerator()
        => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
            => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator()
            => new Enumerator(this);

        public struct Enumerator : IEnumerator<T> {
            private readonly ImmutableArray<T> _owner;
            private int _index;

            internal Enumerator(ImmutableArray<T> owner) {
                _owner = owner;
                _index = 0;
                Current = default;
            }

            public void Dispose() { }

            public bool MoveNext() {
                var localList = _owner;

                if (_index < localList.Count) {
                    Current = localList._items[_index];
                    _index++;
                    return true;
                }

                _index = _owner.Count + 1;
                Current = default;
                return false;
            }

            public T Current { get; private set; }

            object IEnumerator.Current => Current;

            void IEnumerator.Reset() {
                _index = 0;
                Current = default;
            }
        }
    }
}
