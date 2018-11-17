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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        /// <summary>
        /// This type is a compromise between an array (fast access, slow and expensive copying for every immutable change) and binary tree based immutable types
        /// Access is almost as fast as in array, adding is as fast as in List (almost identical implementation),
        /// setting new value and removal of anything but last element always requires full array copying. 
        /// Can't be made public because TrimEnd changes the length, but preserves original array, so referenced objects are persisted, which can be a problem for bigger objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private struct ImmutableArray<T> : IEnumerable<T> {
            private readonly T[] _items;
            private readonly int _size; // Size of the part of array that is used. Equal or less than _items.Length
            private readonly int _count; // Length of the ImmutableArray. Equal or less than _size.

            private ImmutableArray(T[] items, int size, int count) {
                _items = items;
                _size = size;
                _count = count;
            }

            public static ImmutableArray<T> Empty { get; } = new ImmutableArray<T>(Array.Empty<T>(), 0, 0);

            public T this[int index] => _items[index];
            public int Count => _count;

            [Pure]
            public ImmutableArray<T> Add(T item) {
                var newCount = _count + 1;
                var newItems = _items;

                if (_size != _count || newCount > _items.Length) {
                    var capacity = GetCapacity(newCount);
                    newItems = new T[capacity];
                    Array.Copy(_items, 0, newItems, 0, _count);
                }

                newItems[_count] = item;
                return new ImmutableArray<T>(newItems, newCount, newCount);
            }

            [Pure]
            public ImmutableArray<T> AddRange(T[] items) {
                if (items.Length == 0) {
                    return this;
                }

                var newCount = _count + items.Length;
                var newItems = _items;

                if (_size != _count || newCount > _items.Length) {
                    var capacity = GetCapacity(newCount);
                    newItems = new T[capacity];
                    Array.Copy(_items, 0, newItems, 0, _count);
                }

                Array.Copy(items, 0, newItems, _count, items.Length);
                return new ImmutableArray<T>(newItems, newCount, newCount);
            }

            [Pure]
            public ImmutableArray<T> RemoveAt(int index) {
                var newCount = _count - 1;
                if (index == newCount) {
                    return new ImmutableArray<T>(_items, _size, newCount);
                }

                var capacity = GetCapacity(newCount);
                var newArray = new T[capacity];

                if (index > 0) {
                    Array.Copy(_items, newArray, index);
                }

                Array.Copy(_items, index + 1, newArray, index, newCount - index);
                return new ImmutableArray<T>(newArray, newCount, newCount);
            }

            [Pure]
            public ImmutableArray<T> TrimEnd(int trimLength) 
                => trimLength >= _count ? Empty : new ImmutableArray<T>(_items, _size, _count - trimLength);

            [Pure]
            public ImmutableArray<T> ReplaceAt(int index, T value) {
                var capacity = GetCapacity(_count);
                var newArray = new T[capacity];
                Array.Copy(_items, newArray, _count);
                newArray[index] = value;
                return new ImmutableArray<T>(newArray, _count, _count);
            }

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

            private bool Equals(ImmutableArray<T> other) {
                return Equals(_items, other._items) && _size == other._size && _count == other._count;
            }

            public override bool Equals(object obj) => obj is ImmutableArray<T> other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    var hashCode = (_items != null ? _items.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ _size;
                    hashCode = (hashCode * 397) ^ _count;
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

                public void Dispose() {}

                public bool MoveNext() {
                    var localList = _owner;

                    if (_index < localList._count) {
                        Current = localList._items[_index];
                        _index++;
                        return true;
                    }

                    _index = _owner._size + 1;
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
}
