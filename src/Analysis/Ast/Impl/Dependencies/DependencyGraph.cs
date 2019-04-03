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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Dependencies {
    internal struct DependencyGraphSnapshot<TKey, TValue> {
        public int Version;
        public ImmutableArray<DependencyVertex<TKey, TValue>> Vertices { get; }
        public ImmutableArray<TKey> MissingKeys { get; }

        public DependencyGraphSnapshot(int version, ImmutableArray<DependencyVertex<TKey, TValue>> vertices, ImmutableArray<TKey> missingKeys) {
            Version = version;
            Vertices = vertices;
            MissingKeys = missingKeys;
        }

        public DependencyGraphSnapshot(int version) {
            Version = version;
            Vertices = ImmutableArray<DependencyVertex<TKey, TValue>>.Empty;
            MissingKeys = ImmutableArray<TKey>.Empty;
        }
    }

    /// <summary>
    /// Graph that represents dependencies between modules
    /// NOT THREAD SAFE. All concurrent operations should happen under lock
    /// </summary>
    internal sealed class DependencyGraph<TKey, TValue> {
        private readonly Dictionary<TKey, DependencyVertex<TKey, TValue>> _verticesByKey = new Dictionary<TKey, DependencyVertex<TKey, TValue>>();
        private readonly List<DependencyVertex<TKey, TValue>> _verticesByIndex = new List<DependencyVertex<TKey, TValue>>();

        public DependencyGraphSnapshot<TKey, TValue> Snapshot { get; private set; }

        public DependencyGraph() {
            Snapshot = new DependencyGraphSnapshot<TKey, TValue>(0);
        }

        public DependencyVertex<TKey, TValue> AddOrUpdate(TKey key, TValue value, ImmutableArray<TKey> incomingKeys) {
            var version = Snapshot.Version + 1;
            
            DependencyVertex<TKey, TValue> changedVertex;
            if (_verticesByKey.TryGetValue(key, out var currentVertex)) {
                changedVertex = new DependencyVertex<TKey, TValue>(currentVertex, value, incomingKeys, version);
                _verticesByIndex[changedVertex.Index] = changedVertex;
            } else {
                changedVertex = new DependencyVertex<TKey, TValue>(key, value, incomingKeys, version, _verticesByIndex.Count);
                _verticesByIndex.Add(changedVertex);
            }
            
            _verticesByKey[key] = changedVertex;

            
            var vertices = _verticesByIndex
                .Where(v => !v.IsSealed || v.HasMissingKeys)
                .Select(v => GetOrCreateNonSealedVertex(version, v.Index))
                .ToArray();

            if (vertices.Length == 0) {
                Snapshot = new DependencyGraphSnapshot<TKey, TValue>(version);
                return changedVertex;
            }

            CreateNewSnapshot(vertices, version);

            return changedVertex;
        }

        public void RemoveKeys(ImmutableArray<TKey> keys) {
            var version = Snapshot.Version + 1;

            _verticesByIndex.Clear();
            foreach (var key in keys) {
                _verticesByKey.Remove(key);
            }

            foreach (var (key, currentVertex) in _verticesByKey) {
                var changedVertex = new DependencyVertex<TKey, TValue>(key, currentVertex.Value, currentVertex.IncomingKeys, version, _verticesByIndex.Count);
                _verticesByIndex.Add(changedVertex);
            }

            foreach (var vertex in _verticesByIndex) {
                _verticesByKey[vertex.Key] = vertex;
            }

            CreateNewSnapshot(_verticesByIndex, version);
        }

        private void CreateNewSnapshot(IEnumerable<DependencyVertex<TKey, TValue>> vertices, int version) {
            var missingKeysHashSet = new HashSet<TKey>();
            foreach (var vertex in vertices) {
                var newIncoming = ImmutableArray<int>.Empty;
                var oldIncoming = vertex.Incoming;

                foreach (var dependencyKey in vertex.IncomingKeys) {
                    if (_verticesByKey.TryGetValue(dependencyKey, out var dependency)) {
                        newIncoming = newIncoming.Add(dependency.Index);
                    } else {
                        missingKeysHashSet.Add(dependencyKey);
                        vertex.SetHasMissingKeys();
                    }
                }

                foreach (var index in oldIncoming.Except(newIncoming)) {
                    var incomingVertex = GetOrCreateNonSealedVertex(version, index);
                    incomingVertex.RemoveOutgoing(vertex.Index);
                }

                foreach (var index in newIncoming.Except(oldIncoming)) {
                    var incomingVertex = GetOrCreateNonSealedVertex(version, index);
                    incomingVertex.AddOutgoing(vertex.Index);
                }

                vertex.SetIncoming(newIncoming);
            }

            foreach (var vertex in _verticesByIndex) {
                vertex.Seal();
            }

            Snapshot = new DependencyGraphSnapshot<TKey, TValue>(version,
                ImmutableArray<DependencyVertex<TKey, TValue>>.Create(_verticesByIndex),
                ImmutableArray<TKey>.Create(missingKeysHashSet));
        }

        private DependencyVertex<TKey, TValue> GetOrCreateNonSealedVertex(int version, int index) {
            var vertex = _verticesByIndex[index];
            if (!vertex.IsSealed) {
                return vertex;
            }

            vertex = new DependencyVertex<TKey, TValue>(vertex, vertex.Value, vertex.IncomingKeys, version);
            _verticesByIndex[index] = vertex;
            _verticesByKey[vertex.Key] = vertex;
            return vertex;
        }
    }
}
