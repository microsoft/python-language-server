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
        private readonly Dictionary<TKey, int> _keyToVertexIndex = new Dictionary<TKey, int>();
        private readonly List<DependencyVertex<TKey, TValue>> _verticesByIndex = new List<DependencyVertex<TKey, TValue>>();

        private bool _snapshotIsInvalid;
        private DependencyGraphSnapshot<TKey, TValue> _snapshot;
        public int Version { get; private set; }

        public DependencyGraphSnapshot<TKey, TValue> Snapshot {
            get {
                if (_snapshotIsInvalid) {
                    CreateNewSnapshot();
                }

                return _snapshot;
            }
        }
        
        public DependencyGraph() {
            _snapshot = new DependencyGraphSnapshot<TKey, TValue>(0);
            _snapshotIsInvalid = false;
        }

        public DependencyVertex<TKey, TValue> TryAdd(TKey key, TValue value, ImmutableArray<TKey> incomingKeys) {
            var version = ++Version;
            _snapshotIsInvalid = true;

            if (_keyToVertexIndex.TryGetValue(key, out _)) {
                return null;
            }

            var index = _verticesByIndex.Count;
            var changedVertex = new DependencyVertex<TKey, TValue>(key, value, incomingKeys, version, index);
            _verticesByIndex.Add(changedVertex);
            _keyToVertexIndex[key] = index;
            return changedVertex;
        }

        public DependencyVertex<TKey, TValue> AddOrUpdate(TKey key, TValue value, ImmutableArray<TKey> incomingKeys) {
            var version = ++Version;
            _snapshotIsInvalid = true;

            DependencyVertex<TKey, TValue> changedVertex;
            if (_keyToVertexIndex.TryGetValue(key, out var index)) {
                var currentVertex = _verticesByIndex[index];
                changedVertex = new DependencyVertex<TKey, TValue>(currentVertex, value, incomingKeys, version);
                _verticesByIndex[changedVertex.Index] = changedVertex;
            } else {
                index = _verticesByIndex.Count;
                changedVertex = new DependencyVertex<TKey, TValue>(key, value, incomingKeys, version, index);
                _verticesByIndex.Add(changedVertex);
                _keyToVertexIndex[key] = index;
            }
            
            return changedVertex;
        }

        public void RemoveKeys(ImmutableArray<TKey> keys) {
            var version = ++Version;
            _snapshotIsInvalid = true;

            foreach (var key in keys) {
                _keyToVertexIndex.Remove(key);
            }

            var newVertices = new DependencyVertex<TKey, TValue>[_keyToVertexIndex.Count];
            var newIndex = 0;
            foreach (var (key, index) in _keyToVertexIndex) {
                var currentVertex = _verticesByIndex[index];

                var changedVertex = new DependencyVertex<TKey, TValue>(key, currentVertex.Value, currentVertex.IncomingKeys, version, newIndex);
                newVertices[newIndex] = changedVertex;
                newIndex++;
            }

            _keyToVertexIndex.Clear();
            _verticesByIndex.Clear();

            _verticesByIndex.AddRange(newVertices);
            foreach (var vertex in newVertices) {
                _keyToVertexIndex.Add(vertex.Key, vertex.Index);
            }
            CreateNewSnapshot();
        }

        private void CreateNewSnapshot() {
            var version = Version;
            var missingKeysHashSet = new HashSet<TKey>();
            for (var i = 0; i < _verticesByIndex.Count; i++) {
                var vertex = _verticesByIndex[i];
                var newIncoming = new List<int>(vertex.IncomingKeys.Count);
                var oldIncoming = vertex.Incoming;

                foreach (var dependencyKey in vertex.IncomingKeys) {
                    if (_keyToVertexIndex.TryGetValue(dependencyKey, out var index)) {
                        newIncoming.Add(index);
                    } else {
                        missingKeysHashSet.Add(dependencyKey);
                        if (vertex.IsSealed) {
                            vertex = CreateNonSealedVertex(vertex, version, i);
                        }
                        vertex.SetHasMissingKeys();
                    }
                }

                if (newIncoming.Count == oldIncoming.Count && newIncoming.SequenceEqual(oldIncoming)) {
                    continue;
                }

                foreach (var index in oldIncoming.Except(newIncoming)) {
                    var incomingVertex = GetOrCreateNonSealedVertex(version, index);
                    incomingVertex.RemoveOutgoing(vertex.Index);
                }

                foreach (var index in newIncoming.Except(oldIncoming)) {
                    var incomingVertex = GetOrCreateNonSealedVertex(version, index);
                    incomingVertex.AddOutgoing(vertex.Index);
                }

                if (vertex.IsSealed) {
                    vertex = CreateNonSealedVertex(vertex, version, i);
                }

                vertex.SetIncoming(newIncoming.ToImmutableArray());
            }

            foreach (var vertex in _verticesByIndex) {
                vertex.Seal();
            }

            _snapshotIsInvalid = false;
            _snapshot = new DependencyGraphSnapshot<TKey, TValue>(version,
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
            return vertex;
        }

        private DependencyVertex<TKey, TValue> CreateNonSealedVertex(DependencyVertex<TKey, TValue> oldVertex, int version, int index) {
            var vertex = new DependencyVertex<TKey, TValue>(oldVertex, oldVertex.Value, oldVertex.IncomingKeys, version);
            _verticesByIndex[index] = vertex;
            return vertex;
        }
    }
}
