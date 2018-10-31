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

using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        /// <summary>
        /// Represents arc between two nodes in the tree
        /// </summary>
        private readonly struct Arc {
            // List of vertexes.
            // To provide immutability, it must be changed only by calling List<T>.Add
            // Better to be replaced with some immutable structure
            private readonly List<(int index, Node node)> _vertexes;
            private readonly int _index;
            private readonly int _lastIndex;

            public Node Start => IsFirst ? null : _vertexes[_index - 1].node;
            public int EndIndex => _vertexes[_index].index;
            public Node End => _vertexes[_index].node;
            public Arc Previous => IsFirst ? default : new Arc(_vertexes, _index - 1, _lastIndex);
            public Arc Next => IsLast ? default : new Arc(_vertexes, _index + 1, _lastIndex);
            public int PathLength => _index;
            private bool IsFirst => _index == 0;
            private bool IsLast => _index == _lastIndex;

            public Arc(int startIndex, Node start) {
                _vertexes = new List<(int, Node)> {(startIndex, start)};
                _index = 0;
                _lastIndex = 0;
            }

            public Arc GetFirstArc() => new Arc(_vertexes, 0, _lastIndex);
            public Arc GetPrevious(int count) => count > _index ? default : new Arc(_vertexes, _index - count, _lastIndex);
            public Arc GetNext(int count) => _index + count > _lastIndex ? default : new Arc(_vertexes, _index + count, _lastIndex);

            private Arc(List<(int index, Node node)> vertexes, int index, int lastIndex) {
                _vertexes = vertexes;
                _index = index;
                _lastIndex = lastIndex;
            }

            /// <summary>
            /// Appends new arc to the end vertex of current arc
            /// </summary>
            /// <param name="nextVertexIndex"></param>
            /// <returns>New last arc</returns>
            public Arc Append(int nextVertexIndex) {
                var nextVertex = End.GetChildAt(nextVertexIndex);

                if (IsLast) {
                    // Last arc, append vertex to the list and create a new one
                    _vertexes.Add((nextVertexIndex, nextVertex));
                    return new Arc(_vertexes, _index + 1, _index + 1);
                }

                // branch from existing arc
                var branchVertexes = new List<(int, Node)>(_vertexes);
                var excess = branchVertexes.Count - _index - 1;
                branchVertexes.RemoveRange(branchVertexes.Count - excess, excess);
                branchVertexes.Add((nextVertexIndex, nextVertex));
                return new Arc(branchVertexes, _index + 1, _index + 1);
            }

            public override bool Equals(object obj) => obj is Arc other && Equals(other);
            public bool Equals(Arc other) => Equals(_vertexes, other._vertexes) && _index == other._index;

            public override int GetHashCode() {
                unchecked {
                    return ((_vertexes != null ? _vertexes.GetHashCode() : 0) * 397) ^ _index;
                }
            }

            public static bool operator ==(Arc left, Arc right) => left.Equals(right);
            public static bool operator !=(Arc left, Arc right) => !left.Equals(right);
        }
    }
}