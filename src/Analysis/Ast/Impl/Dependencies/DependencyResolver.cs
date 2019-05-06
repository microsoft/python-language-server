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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Threading;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class DependencyResolver<TKey, TValue> : IDependencyResolver<TKey, TValue> {
        private readonly Dictionary<TKey, int> _keys = new Dictionary<TKey, int>();
        private readonly List<DependencyVertex<TKey, TValue>> _vertices = new List<DependencyVertex<TKey, TValue>>();
        private readonly object _syncObj = new object();

        private int _version;

        public int Version => _version;
        
        public int ChangeValue(TKey key, TValue value, bool isRoot, params TKey[] incomingKeys)
            => ChangeValue(key, value, isRoot, ImmutableArray<TKey>.Create(incomingKeys));

        public int ChangeValue(TKey key, TValue value, bool isRoot, ImmutableArray<TKey> incomingKeys) {
            lock (_syncObj) {
                if (!_keys.TryGetValue(key, out var index)) {
                    index = _keys.Count;
                    _keys[key] = index;
                    _vertices.Add(default);
                }

                Update(key, value, isRoot, incomingKeys, index);
                return _version;
            }
        }

        public int TryAddValue(TKey key, TValue value, bool isRoot, ImmutableArray<TKey> incomingKeys) {
            lock (_syncObj) {
                if (!_keys.TryGetValue(key, out var index)) {
                    index = _keys.Count;
                    _keys[key] = index;
                    _vertices.Add(default);
                } else if (_vertices[index] != default) {
                    return _version;
                }

                Update(key, value, isRoot, incomingKeys, index);
                return _version;
            }
        }

        public int Remove(TKey key) {
            lock (_syncObj) {
                if (!_keys.TryGetValue(key, out var index)) {
                    return _version;
                }

                Interlocked.Increment(ref _version);

                _vertices[index] = default;
                return _version;
            }
        }

        public int RemoveKeys(params TKey[] keys) => RemoveKeys(ImmutableArray<TKey>.Create(keys));

        public int RemoveKeys(ImmutableArray<TKey> keys) {
            lock (_syncObj) {
                foreach (var key in keys) {
                    if (_keys.TryGetValue(key, out var index)) {
                        _vertices[index] = default;
                    }
                }

                var oldKeysReversed = _keys.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
                var oldVertices = new DependencyVertex<TKey, TValue>[_vertices.Count];
                _vertices.CopyTo(oldVertices);

                _keys.Clear();
                _vertices.Clear();

                foreach (var oldVertex in oldVertices) {
                    if (oldVertex == default) {
                        continue;
                    }

                    var incomingKeys = oldVertex.Incoming.Select(i => oldKeysReversed[i]);
                    var key = oldVertex.Key;
                    var value = oldVertex.Value;
                    var isRoot = oldVertex.IsRoot;

                    if (!_keys.TryGetValue(key, out var index)) {
                        index = _keys.Count;
                        _keys[key] = index;
                        _vertices.Add(default);
                    }
                    
                    Update(key, value, isRoot, incomingKeys, index);
                }

                return _version;
            }
        }

        private void Update(TKey key, TValue value, bool isRoot, ImmutableArray<TKey> incomingKeys, int index) {
            var version = Interlocked.Increment(ref _version);

            var incoming = EnsureKeys(index, incomingKeys, version);

            _vertices[index] = new DependencyVertex<TKey, TValue>(key, value, isRoot, incoming, version, index);
            _keys[key] = index;
        }

        private ImmutableArray<int> EnsureKeys(int index, ImmutableArray<TKey> keys, int version) {
            var incoming = ImmutableArray<int>.Empty;

            foreach (var key in keys) {
                if (!_keys.TryGetValue(key, out var keyIndex)) {
                    keyIndex = _keys.Count;
                    _keys[key] = keyIndex;
                    _vertices.Add(default);
                } else {
                    var vertex = _vertices[keyIndex];
                    if (vertex != default && vertex.IsSealed && !vertex.ContainsOutgoing(index)) {
                        _vertices[keyIndex] = new DependencyVertex<TKey, TValue>(vertex, version);
                    }
                }

                incoming = incoming.Add(keyIndex);
            }

            return incoming;
        }

        public IDependencyChainWalker<TKey, TValue> CreateWalker() {
            lock (_syncObj) {
                TryCreateWalker(_version, int.MaxValue, out var walker);
                return walker;
            }
        }

        public bool TryCreateWalker(int version, int walkerDepthLimit, out IDependencyChainWalker<TKey, TValue> walker) {
            ImmutableArray<DependencyVertex<TKey, TValue>> vertices;

            lock (_syncObj) {
                if (version != _version) {
                    walker = default;
                    return false;
                }

                vertices = ImmutableArray<DependencyVertex<TKey, TValue>>.Create(_vertices);
            }

            if (!TryBuildReverseGraph(vertices, version)) {
                walker = default;
                return false;
            }

            if (!TryCreateWalkingGraph(vertices, version, out var walkingGraph)) {
                walker = default;
                return false;
            }

            if (!FindLoops(walkingGraph, version, out var loopsCount)) {
                walker = default;
                return false;
            }

            if (!TryResolveLoops(walkingGraph, loopsCount, version, out var startingVertices, out var totalNodesCount)) {
                walker = default;
                return false;
            }

            foreach (var vertex in walkingGraph) {
                vertex.Seal();
                vertex.SecondPass?.Seal();
            }

            var depths = CalculateDepths(vertices);
            lock (_syncObj) {
                if (version != _version) {
                    walker = default;
                    return false;
                }

                var affectedValues = walkingGraph.Select(v => v.DependencyVertex.Value);
                var missingKeys = ImmutableArray<TKey>.Empty;
                foreach (var (key, index) in _keys) {
                    if (_vertices[index] == default/* && depths[index] <= walkerDepthLimit*/) {
                        missingKeys = missingKeys.Add(key);
                    }
                }

                walker = new DependencyChainWalker(startingVertices, affectedValues, depths, missingKeys, totalNodesCount, version);
                return true;
            }
        }

        private bool TryBuildReverseGraph(ImmutableArray<DependencyVertex<TKey, TValue>> vertices, int version) {
            var reverseGraphIsBuilt = true;
            foreach (var vertex in vertices) {
                if (vertex != null && !vertex.IsSealed) {
                    reverseGraphIsBuilt = false;
                    break;
                }
            }

            if (reverseGraphIsBuilt) {
                return version == _version;
            }

            var outgoingVertices = new HashSet<int>[vertices.Count];
            foreach (var vertex in vertices) {
                if (vertex == null) {
                    continue;
                }

                if (version != _version) {
                    return false;
                }

                foreach (var incomingIndex in vertex.Incoming) {
                    if (vertices[incomingIndex] != default) {
                        var outgoing = outgoingVertices[incomingIndex];
                        if (outgoing == default) {
                            outgoing = new HashSet<int>();
                            outgoingVertices[incomingIndex] = outgoing;
                        } 

                        outgoing.Add(vertex.Index);
                    }
                }
            }

            lock (_syncObj) {
                if (version != _version) {
                    return false;
                }

                foreach (var vertex in vertices) {
                    if (vertex != null && !vertex.IsSealed) {
                        vertex.Seal(outgoingVertices[vertex.Index]);
                    }
                }

                return true;
            }
        }

        private bool TryCreateWalkingGraph(in ImmutableArray<DependencyVertex<TKey, TValue>> vertices, int version, out ImmutableArray<WalkingVertex<TKey, TValue>> analysisGraph) {
            var nodesByVertexIndex = new Dictionary<int, WalkingVertex<TKey, TValue>>();

            foreach (var vertex in vertices) {
                if (vertex == null || vertex.IsWalked) {
                    continue;
                }

                var node = new WalkingVertex<TKey, TValue>(vertices[vertex.Index]);
                nodesByVertexIndex[vertex.Index] = node;
            }

            if (nodesByVertexIndex.Count == 0) {
                analysisGraph = default;
                return false;
            }

            var queue = new Queue<WalkingVertex<TKey, TValue>>(nodesByVertexIndex.Values);
            while (queue.Count > 0) {
                var node = queue.Dequeue();
                if (version != _version) {
                    analysisGraph = default;
                    return false;
                }

                foreach (var outgoingIndex in node.DependencyVertex.Outgoing) {
                    if (!nodesByVertexIndex.TryGetValue(outgoingIndex, out var outgoingNode)) {
                        var vertex = vertices[outgoingIndex];
                        outgoingNode = new WalkingVertex<TKey, TValue>(vertex);
                        nodesByVertexIndex[outgoingIndex] = outgoingNode;

                        queue.Enqueue(outgoingNode);
                    }

                    node.AddOutgoing(outgoingNode);
                }
            }

            analysisGraph = ImmutableArray<WalkingVertex<TKey, TValue>>.Create(nodesByVertexIndex.Values);
            return true;
        }

        private static ImmutableArray<int> CalculateDepths(in ImmutableArray<DependencyVertex<TKey, TValue>> vertices) {
            var depths = new int[vertices.Count];
            for (var i = 0; i < depths.Length; i++) {
                depths[i] = -1;
            }

            for (var i = 0; i < depths.Length; i++) {
                var vertex = vertices[i];
                if (vertex != null && vertex.IsRoot) {
                    depths[i] = 0;
                    SetDepths(depths, vertices, vertex.Incoming, 1);
                }
            }

            for (var i = 0; i < depths.Length; i++) {
                var vertex = vertices[i];
                if (vertex != null && depths[i] == -1) {
                    depths[i] = 1;
                    SetDepths(depths, vertices, vertex.Incoming, 2);
                }
            }

            return ImmutableArray<int>.Create(depths);
        }

        private static void SetDepths(int[] depths, ImmutableArray<DependencyVertex<TKey, TValue>> vertices, ImmutableArray<int> indices, int depth) {
            foreach (var index in indices) {
                if (depths[index] != -1 && depths[index] <= depth) {
                    continue;
                }

                depths[index] = depth;
                var vertex = vertices[index];
                if (vertex != null && vertex.Incoming.Count > 0) {
                    SetDepths(depths, vertices, vertex.Incoming, depth + 1);
                }
            }
        }

        private bool FindLoops(ImmutableArray<WalkingVertex<TKey, TValue>> graph, int version, out int loopsCount) {
            var index = 0;
            var loopNumber = 0;
            var stackP = new Stack<WalkingVertex<TKey, TValue>>();
            var stackS = new Stack<WalkingVertex<TKey, TValue>>();

            foreach (var vertex in graph) {
                if (vertex.Index == -1) {
                    CheckForLoop(vertex, stackP, stackS, ref index, ref loopNumber);
                }

                if (version != _version) {
                    loopsCount = default;
                    return false;
                }
            }

            loopsCount = loopNumber;
            return true;
        }

        private static void CheckForLoop(WalkingVertex<TKey, TValue> vertex, Stack<WalkingVertex<TKey, TValue>> stackP,
            Stack<WalkingVertex<TKey, TValue>> stackS, ref int counter, ref int loopNumber) {
            vertex.Index = counter++;
            stackP.Push(vertex);
            stackS.Push(vertex);

            foreach (var child in vertex.Outgoing) {
                if (child.Index == -1) {
                    CheckForLoop(child, stackP, stackS, ref counter, ref loopNumber);
                } else if (child.LoopNumber == -1) {
                    while (stackP.Peek().Index > child.Index) {
                        stackP.Pop();
                    }
                }
            }

            if (stackP.Count > 0 && vertex == stackP.Peek()) {
                if (SetLoopNumber(vertex, stackS, loopNumber)) {
                    loopNumber++;
                }

                stackP.Pop();
            }
        }

        private static bool SetLoopNumber(WalkingVertex<TKey, TValue> vertex, Stack<WalkingVertex<TKey, TValue>> stackS,
            int loopIndex) {
            var count = 0;
            WalkingVertex<TKey, TValue> loopVertex;
            do {
                loopVertex = stackS.Pop();
                loopVertex.LoopNumber = loopIndex;
                count++;
            } while (loopVertex != vertex);

            if (count != 1) {
                return true;
            }

            vertex.LoopNumber = -2;
            return false;
        }

        private bool TryResolveLoops(ImmutableArray<WalkingVertex<TKey, TValue>> graph, int loopsCount, int version, out ImmutableArray<WalkingVertex<TKey, TValue>> startingVertices, out int totalNodesCount) {
            // Create vertices for second pass
            var inLoopsCount = 0;
            var secondPassLoops = new List<WalkingVertex<TKey, TValue>>[loopsCount];
            foreach (var vertex in graph) {
                if (vertex.IsInLoop) {
                    var secondPassVertex = vertex.CreateSecondPassVertex();
                    var loopNumber = vertex.LoopNumber;
                    if (secondPassLoops[loopNumber] == null) {
                        secondPassLoops[loopNumber] = new List<WalkingVertex<TKey, TValue>> {secondPassVertex};
                    } else {
                        secondPassLoops[loopNumber].Add(secondPassVertex);
                    }

                    inLoopsCount++;
                }

                if (version != _version) {
                    startingVertices = default;
                    totalNodesCount = default;
                    return false;
                }

                vertex.Index = -1; // Reset index, will use later
            }

            // Break the loops so that its items can be iterated
            foreach (var loop in secondPassLoops) {
                // Sort loop items by amount of incoming connections
                loop.Sort(WalkingVertex<TKey, TValue>.FirstPassIncomingComparison);

                var counter = 0;
                foreach (var secondPassVertex in loop) {
                    var vertex = secondPassVertex.FirstPass;
                    if (vertex.Index == -1) {
                        RemoveLoopEdges(vertex, ref counter);
                    }

                    if (version != _version) {
                        startingVertices = default;
                        totalNodesCount = default;
                        return false;
                    }
                }
            }

            // Make all vertices from second pass loop have incoming edges from vertices from first pass loop and set unique loop numbers
            var outgoingVertices = new HashSet<WalkingVertex<TKey, TValue>>();
            foreach (var loop in secondPassLoops) {
                outgoingVertices.Clear();
                foreach (var secondPassVertex in loop) {
                    var firstPassVertex = secondPassVertex.FirstPass;
                    firstPassVertex.AddOutgoing(loop);

                    foreach (var outgoingVertex in firstPassVertex.Outgoing) {
                        if (outgoingVertex.LoopNumber != firstPassVertex.LoopNumber) {
                            // Collect outgoing vertices to reference them from loop
                            outgoingVertices.Add(outgoingVertex);
                        } else if (outgoingVertex.SecondPass != null) {
                            // Copy outgoing edges to the second pass vertex
                            secondPassVertex.AddOutgoing(outgoingVertex.SecondPass);
                        }
                    }
                }

                foreach (var secondPassVertex in loop) {
                    secondPassVertex.AddOutgoing(outgoingVertices);
                }

                if (version != _version) {
                    startingVertices = default;
                    totalNodesCount = default;
                    return false;
                }

                loopsCount++;
            }

            // Iterate original graph to get starting vertices
            startingVertices = graph.Where(v => v.IncomingCount == 0);
            totalNodesCount = graph.Count + inLoopsCount;

            return true;
        }

        private static void RemoveLoopEdges(WalkingVertex<TKey, TValue> vertex, ref int counter) {
            vertex.Index = counter++;
            for (var i = vertex.Outgoing.Count - 1; i >= 0; i--) {
                var outgoing = vertex.Outgoing[i];
                if (outgoing.LoopNumber != vertex.LoopNumber) {
                    continue;
                }

                if (outgoing.Index == -1) {
                    RemoveLoopEdges(outgoing, ref counter);
                } else if (outgoing.Index < vertex.Index) {
                    vertex.RemoveOutgoingAt(i);
                }
            }
        }

        private sealed class DependencyChainWalker : IDependencyChainWalker<TKey, TValue> {
            private readonly ImmutableArray<WalkingVertex<TKey, TValue>> _startingVertices;
            private readonly ImmutableArray<int> _depths;
            private readonly object _syncObj;
            private int _remaining;
            private PriorityProducerConsumer<IDependencyChainNode<TValue>> _ppc;

            public ImmutableArray<TKey> MissingKeys { get; }
            public ImmutableArray<TValue> AffectedValues { get; }
            public int Version { get; }

            public int Remaining {
                get {
                    lock (_syncObj) {
                        return _remaining;
                    }
                }
            }

            public DependencyChainWalker(
                in ImmutableArray<WalkingVertex<TKey, TValue>> startingVertices,
                in ImmutableArray<TValue> affectedValues,
                in ImmutableArray<int> depths,
                in ImmutableArray<TKey> missingKeys,
                in int totalNodesCount,
                in int version) {
                _syncObj = new object();
                _startingVertices = startingVertices;
                _depths = depths;
                AffectedValues = affectedValues;
                Version = version;
                MissingKeys = missingKeys;

                _remaining = totalNodesCount;
            }

            public Task<IDependencyChainNode<TValue>> GetNextAsync(CancellationToken cancellationToken) {
                PriorityProducerConsumer<IDependencyChainNode<TValue>> ppc;
                lock (_syncObj) {
                    if (_ppc == null) {
                        _ppc = new PriorityProducerConsumer<IDependencyChainNode<TValue>>();

                        foreach (var vertex in _startingVertices) {
                            _ppc.Produce(new DependencyChainNode(this, vertex, _depths[vertex.DependencyVertex.Index]));
                        }
                    }

                    ppc = _ppc;
                }

                return ppc.ConsumeAsync(cancellationToken);
            }

            public void MarkCompleted(WalkingVertex<TKey, TValue> vertex, bool commitChanges) {
                var verticesToProduce = new List<WalkingVertex<TKey, TValue>>();
                var isCompleted = false;
                lock (_syncObj) {
                    _remaining--;
                    foreach (var outgoing in vertex.Outgoing) {
                        if (outgoing.IncomingCount == 0) {
                            continue;
                        }

                        outgoing.DecrementIncoming();
                        if (outgoing.IncomingCount > 0) {
                            continue;
                        }

                        verticesToProduce.Add(outgoing);
                    }

                    if (_remaining == 0) {
                        isCompleted = true;
                    }
                }

                if (commitChanges && vertex.SecondPass == null) {
                    vertex.DependencyVertex.MarkWalked();
                }

                if (isCompleted) {
                    _ppc.Produce(null);
                } else {
                    foreach (var toProduce in verticesToProduce) {
                        _ppc.Produce(new DependencyChainNode(this, toProduce, _depths[toProduce.DependencyVertex.Index]));
                    }
                }
            }
        }

        private sealed class DependencyChainNode : IDependencyChainNode<TValue> {
            private readonly WalkingVertex<TKey, TValue> _vertex;
            private DependencyChainWalker _walker;
            public TValue Value => _vertex.DependencyVertex.Value;
            public int VertexDepth { get; }

            public DependencyChainNode(DependencyChainWalker walker, WalkingVertex<TKey, TValue> vertex, int depth) {
                _walker = walker;
                _vertex = vertex;
                VertexDepth = depth;
            }

            public void Commit() => Interlocked.Exchange(ref _walker, null)?.MarkCompleted(_vertex, true);
            public void Skip() => Interlocked.Exchange(ref _walker, null)?.MarkCompleted(_vertex, false);
        }
    }
}
