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
using System.Diagnostics;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        /// <summary>
        /// Represents the edge between two nodes in the tree
        /// </summary>
        [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
        private readonly struct Edge {
            // List of vertices.
            // For the faster search, each item is represented as node and index of that node in the parent
            // For example, the following path:
            //
            //       [*] 
            //  ┌───┬─╚═╗───┐
            // [a] [b] [c] [d]
            //    ╔═══╤═╝─┬───┐
            //   [i] [j] [k] [l]
            //  ┌─╚═╗
            // [x] [y] 
            //
            // will be stored as list of pairs: (2, a), (0, i), (1, y)
            // 
            // To provide immutability, it must be changed only by calling List<T>.Add
            private readonly ImmutableArray<(int index, Node node)> _vertices;
            private readonly int _index;

            public Node Start => IsFirst ? default : _vertices[_index - 1].node;
            public int EndIndex => _vertices[_index].index;
            public Node End => _vertices[_index].node;
            public Edge Previous => IsFirst ? default : new Edge(_vertices, _index - 1);
            public Edge Next => IsLast ? default : new Edge(_vertices, _index + 1);
            public int PathLength => _vertices.Count;
            public bool IsFirst => _index == 0;
            public bool IsNonRooted => _vertices.Count > 0 && _vertices[0].node.Name == "*";
            public bool IsEmpty => _vertices.Count == 0;
            private bool IsLast => _index == _vertices.Count - 1;

            public Edge(int startIndex, Node start) {
                _vertices = ImmutableArray<(int, Node)>.Empty.Add((startIndex, start));
                _index = 0;
            }

            public Edge FirstEdge => new Edge(_vertices, 0);
            public Edge GetPrevious(int count) => count > _index ? default : new Edge(_vertices, _index - count);
            public Edge GetNext(int count) => _index + count > _vertices.Count - 1 ? default : new Edge(_vertices, _index + count);

            private Edge(ImmutableArray<(int index, Node node)> vertices, int index) {
                _vertices = vertices;
                _index = index;
            }

            /// <summary>
            /// Appends new arc to the end vertex of current arc
            /// </summary>
            /// <param name="nextVertexIndex"></param>
            /// <returns>New last arc</returns>
            public Edge Append(int nextVertexIndex) {
                var nextVertex = End.Children[nextVertexIndex];
                var trimLength = _vertices.Count - _index - 1;
                var vertices = _vertices.TrimEnd(trimLength).Add((nextVertexIndex, nextVertex));
                return new Edge(vertices, _index + 1);
            }

            public override bool Equals(object obj) => obj is Edge other && Equals(other);
            public bool Equals(Edge other) => Equals(_vertices, other._vertices) && _index == other._index;

            public override int GetHashCode() {
                unchecked {
                    return (_vertices.GetHashCode() * 397) ^ _index;
                }
            }

            public static bool operator ==(Edge left, Edge right) => left.Equals(right);
            public static bool operator !=(Edge left, Edge right) => !left.Equals(right);

            private string DebuggerDisplay {
                get {
                    var start = _index > 0 ? _vertices[_index - 1].node.Name : "null";
                    var endIndex = _vertices[_index].index.ToString();
                    var end = _vertices[_index].node.Name;
                    return $"[{start}]-{endIndex}-[{end}]";
                }
            }
        }
    }
}
