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

using System.Diagnostics;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public partial class PathResolverSnapshot {
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
            // will be stored as list of pairs: (2, c), (0, i), (1, y)
            // 
            // To provide immutability, it must be changed only by calling List<T>.Add
            private readonly ImmutableArray<(int nodeIndexInParent, Node node)> _pathFromRoot;

            // indicates which entry in the _pathFromRoot correspond to this edge.
            private readonly int _indexInPath;

            public Node ParentNode => IsFirst ? default : _pathFromRoot[_indexInPath - 1].node;
            public int NodeIndexInParent => _pathFromRoot[_indexInPath].nodeIndexInParent;
            public Node Node => _pathFromRoot[_indexInPath].node;
            public Edge Previous => IsFirst ? default : new Edge(_pathFromRoot, _indexInPath - 1);
            public Edge Next => IsLast ? default : new Edge(_pathFromRoot, _indexInPath + 1);
            public int PathLength => _pathFromRoot.Count;
            public bool IsFirst => _indexInPath == 0;
            public bool IsNonRooted => _pathFromRoot.Count > 0 && _pathFromRoot[0].node.Name == "*";
            public bool IsEmpty => _pathFromRoot.Count == 0;
            private bool IsLast => _indexInPath == _pathFromRoot.Count - 1;

            public Edge(int startIndex, Node start) {
                _pathFromRoot = ImmutableArray<(int, Node)>.Empty.Add((startIndex, start));
                _indexInPath = 0;
            }

            public Edge FirstEdge => new Edge(_pathFromRoot, indexInVertices: 0);
            public Edge GetPrevious(int count) => count > _indexInPath ? default : new Edge(_pathFromRoot, _indexInPath - count);
            public Edge GetNext(int count) => _indexInPath + count > _pathFromRoot.Count - 1 ? default : new Edge(_pathFromRoot, _indexInPath + count);

            private Edge(ImmutableArray<(int index, Node node)> vertices, int indexInVertices) {
                // Node has only down pointers for the immutable tree so that when tree needs to be updated
                // it can update only spine of the tree and reuse all other tree nodes. basically making
                // the tree incrementally updatable.
                // but not having Parent pointer makes using the tree data structure hard. so 
                // Edge tracks the spine (provide Back/Parent pointer) but only created on demand for
                // a specific node requested.
                // 
                // concept is similar to green-red tree.
                // see https://blog.yaakov.online/red-green-trees/
                //     https://blogs.msdn.microsoft.com/ericlippert/2012/06/08/persistence-facades-and-roslyns-red-green-trees/
                //     https://github.com/KirillOsenkov/Bliki/wiki/Roslyn-Immutable-Trees

                _pathFromRoot = vertices;
                _indexInPath = indexInVertices;
            }

            /// <summary>
            /// Appends new arc to the end vertex of current arc
            /// </summary>
            /// <param name="childVertexIndexToAppened"></param>
            /// <returns>New last arc</returns>
            public Edge Append(int childVertexIndexToAppened) {
                var nextVertex = Node.Children[childVertexIndexToAppened];
                var trimLength = _pathFromRoot.Count - _indexInPath - 1;
                var vertices = _pathFromRoot.ReplaceAt(_indexInPath + 1, trimLength, (childVertexIndexToAppened, nextVertex));
                return new Edge(vertices, _indexInPath + 1);
            }

            public override bool Equals(object obj) => obj is Edge other && Equals(other);
            public bool Equals(Edge other) => Equals(_pathFromRoot, other._pathFromRoot) && _indexInPath == other._indexInPath;

            public override int GetHashCode() {
                unchecked {
                    return (_pathFromRoot.GetHashCode() * 397) ^ _indexInPath;
                }
            }

            public static bool operator ==(Edge left, Edge right) => left.Equals(right);
            public static bool operator !=(Edge left, Edge right) => !left.Equals(right);

            private string DebuggerDisplay {
                get {
                    var start = _indexInPath > 0 ? _pathFromRoot[_indexInPath - 1].node.Name : "null";
                    var endIndex = _pathFromRoot[_indexInPath].nodeIndexInParent.ToString();
                    var end = _pathFromRoot[_indexInPath].node.Name;
                    return $"[{start}]-{endIndex}-[{end}]";
                }
            }
        }
    }
}
