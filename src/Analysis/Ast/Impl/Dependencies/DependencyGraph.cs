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
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Dependencies {
    /// <summary>
    /// Graph that represents dependencies between modules
    /// NOT THREAD SAFE. All operations should happen under lock
    /// </summary>
    internal sealed class DependencyGraph<TKey, TValue> {
        private readonly Dictionary<TKey, DependencyVertex<TKey, TValue>> _verticesByKey = new Dictionary<TKey, DependencyVertex<TKey, TValue>>();
        private readonly List<DependencyVertex<TKey, TValue>> _verticesByIndex = new List<DependencyVertex<TKey, TValue>>();

        public int Version { get; private set; }

        public DependencyVertex<TKey, TValue> AddOrUpdate(TKey key, TValue value) {
            Version++;
            
            DependencyVertex<TKey, TValue> vertex;
            if (_verticesByKey.TryGetValue(key, out var currentVertex)) {
                vertex = new DependencyVertex<TKey, TValue>(currentVertex, value, Version);
                _verticesByIndex[vertex.Index] = vertex;
            } else {
                vertex = new DependencyVertex<TKey, TValue>(key, value, Version, _verticesByIndex.Count);
                _verticesByIndex.Add(vertex);
            }
            
            _verticesByKey[key] = vertex;
            return vertex;
        }

        public void ResolveDependencies(out ImmutableArray<DependencyVertex<TKey, TValue>> snapshot, out ImmutableArray<TKey> missingKeys) {
            var missingKeysHashSet = new HashSet<TKey>();
            var vertices = _verticesByIndex
                .Where(v => !v.IsSealed || v.HasMissingKeys)
                .Select(v => GetOrCreateNonSealedVertex(v.Index))
                .ToArray();

            if (vertices.Length == 0) {
                snapshot = ImmutableArray<DependencyVertex<TKey, TValue>>.Create(_verticesByIndex);
                missingKeys = ImmutableArray<TKey>.Empty;
                return;
            }

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
                    var incomingVertex = GetOrCreateNonSealedVertex(index);
                    incomingVertex.RemoveOutgoing(vertex.Index);
                }

                foreach (var index in newIncoming.Except(oldIncoming)) {
                    var incomingVertex = GetOrCreateNonSealedVertex(index);
                    incomingVertex.AddOutgoing(vertex.Index);
                }

                vertex.SetIncoming(newIncoming);
            }

            foreach (var vertex in _verticesByIndex) {
                vertex.Seal();
            }

            snapshot = ImmutableArray<DependencyVertex<TKey, TValue>>.Create(_verticesByIndex);
            missingKeys = ImmutableArray<TKey>.Create(missingKeysHashSet);

            DependencyVertex<TKey, TValue> GetOrCreateNonSealedVertex(int index) {
                var vertex = _verticesByIndex[index];
                if (!vertex.IsSealed) {
                    return vertex;
                }

                vertex = new DependencyVertex<TKey, TValue>(vertex, vertex.Value, Version);
                _verticesByIndex[index] = vertex;
                _verticesByKey[vertex.Key] = vertex;
                return vertex;
            }
        }
    }
}
