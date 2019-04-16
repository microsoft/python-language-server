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

            if (_verticesByKey.TryGetValue(key, out _)) {
                return null;
            }

            var changedVertex = new DependencyVertex<TKey, TValue>(key, value, incomingKeys, version, _verticesByIndex.Count);
            _verticesByIndex.Add(changedVertex);
            _verticesByKey[key] = changedVertex;
            return changedVertex;
        }

        public DependencyVertex<TKey, TValue> AddOrUpdate(TKey key, TValue value, ImmutableArray<TKey> incomingKeys) {
            var version = ++Version;
            _snapshotIsInvalid = true;

            DependencyVertex<TKey, TValue> changedVertex;
            if (_verticesByKey.TryGetValue(key, out var currentVertex)) {
                changedVertex = new DependencyVertex<TKey, TValue>(currentVertex, value, incomingKeys, version);
                _verticesByIndex[changedVertex.Index] = changedVertex;
            } else {
                changedVertex = new DependencyVertex<TKey, TValue>(key, value, incomingKeys, version, _verticesByIndex.Count);
                _verticesByIndex.Add(changedVertex);
            }
            
            _verticesByKey[key] = changedVertex;
            return changedVertex;
        }

        public void RemoveKeys(ImmutableArray<TKey> keys) {
            var version = ++Version;
            _snapshotIsInvalid = true;

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

            CreateNewSnapshot();
        }

        private void CreateNewSnapshot() {
            var version = Version;
            var missingKeysHashSet = new HashSet<TKey>();
            for (var i = 0; i < _verticesByIndex.Count; i++) {
                var vertex = _verticesByIndex[i];
                var newIncoming = ImmutableArray<int>.Empty;
                var oldIncoming = vertex.Incoming;

                foreach (var dependencyKey in vertex.IncomingKeys) {
                    if (_verticesByKey.TryGetValue(dependencyKey, out var dependency)) {
                        newIncoming = newIncoming.Add(dependency.Index);
                    } else {
                        missingKeysHashSet.Add(dependencyKey);
                        if (vertex.IsSealed) {
                            vertex = CreateNonSealedVertex(vertex, version, i);
                        }
                        vertex.SetHasMissingKeys();
                    }
                }

                if (newIncoming.SequentiallyEquals(oldIncoming)) {
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

                vertex.SetIncoming(newIncoming);
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
            _verticesByKey[vertex.Key] = vertex;
            return vertex;
        }

        private DependencyVertex<TKey, TValue> CreateNonSealedVertex(DependencyVertex<TKey, TValue> oldVertex, int version, int index) {
            var vertex = new DependencyVertex<TKey, TValue>(oldVertex, oldVertex.Value, oldVertex.IncomingKeys, version);
            _verticesByIndex[index] = vertex;
            _verticesByKey[vertex.Key] = vertex;
            return vertex;
        }


    }
}
