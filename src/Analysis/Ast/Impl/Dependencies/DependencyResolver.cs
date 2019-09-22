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
        
        public int ChangeValue(in TKey key, in TValue value, in bool isRoot, params TKey[] incomingKeys)
            => ChangeValue(key, value, isRoot, ImmutableArray<TKey>.Create(incomingKeys));

        public int ChangeValue(in TKey key, in TValue value, in bool isRoot, in ImmutableArray<TKey> incomingKeys) {
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

        public int TryAddValue(in TKey key, in TValue value, in bool isRoot, in ImmutableArray<TKey> incomingKeys) {
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

        public int Remove(in TKey key) {
            lock (_syncObj) {
                if (!_keys.TryGetValue(key, out var index)) {
                    return _version;
                }

                var version = Interlocked.Increment(ref _version);

                var vertex = _vertices[index];
                if (vertex == default) {
                    return version;
                }

                _vertices[index] = default;
                foreach (var incomingIndex in vertex.Incoming) {
                    var incoming = _vertices[incomingIndex];
                    if (incoming != default && incoming.IsSealed) {
                        _vertices[incomingIndex] = new DependencyVertex<TKey, TValue>(incoming, version, false);
                    }
                }

                if (!vertex.IsSealed) {
                    return version;
                }
                
                foreach (var outgoingIndex in vertex.Outgoing) {
                    var outgoing = _vertices[outgoingIndex];
                    if (outgoing != default && !outgoing.IsNew) {
                        _vertices[outgoingIndex] = new DependencyVertex<TKey, TValue>(outgoing, version, true);
                    }
                }

                return version;
            }
        }

        public int RemoveKeys(params TKey[] keys) => RemoveKeys(ImmutableArray<TKey>.Create(keys));

        public int RemoveKeys(in ImmutableArray<TKey> keys) {
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

        private void Update(in TKey key, in TValue value, in bool isRoot, in ImmutableArray<TKey> incomingKeys, in int index) {
            var version = Interlocked.Increment(ref _version);

            var incoming = EnsureKeys(index, incomingKeys, version);

            _vertices[index] = new DependencyVertex<TKey, TValue>(key, value, isRoot, incoming, version, index);
            _keys[key] = index;
        }

        private ImmutableArray<int> EnsureKeys(in int index, in ImmutableArray<TKey> keys, in int version) {
            var incoming = ImmutableArray<int>.Empty;

            foreach (var key in keys) {
                if (!_keys.TryGetValue(key, out var keyIndex)) {
                    keyIndex = _keys.Count;
                    _keys[key] = keyIndex;
                    _vertices.Add(default);
                } else {
                    var vertex = _vertices[keyIndex];
                    if (vertex != default && vertex.IsSealed && !vertex.ContainsOutgoing(index)) {
                        _vertices[keyIndex] = new DependencyVertex<TKey, TValue>(vertex, version, false);
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

        public bool TryCreateWalker(in int version, in int walkerDepthLimit, out IDependencyChainWalker<TKey, TValue> walker) {
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

            var depths = CalculateDepths(vertices);

            if (!TryCreateWalkingGraph(vertices, depths, version, out var walkingGraph)) {
                walker = default;
                return false;
            }

            if (!FindLoops(walkingGraph, version, out var loopsCount)) {
                walker = default;
                return false;
            }

            if (!TryResolveLoops(walkingGraph, loopsCount, version, out var loopNodes)) {
                walker = default;
                return false;
            }

            // Iterate original graph to get starting vertices
            if (!TryFindMissingDependencies(vertices, walkingGraph, version, out var missingKeys)) {
                walker = default;
                return false;
            }

            foreach (var vertex in walkingGraph) {
                vertex.Seal();
            }

            var affectedValues = walkingGraph.Select(v => v.DependencyVertex.Value);
            var startingVertices = walkingGraph.AddRange(loopNodes).Where(v => !v.HasIncoming);
            walker = new DependencyChainWalker(this, startingVertices, affectedValues, depths, missingKeys, walkingGraph.Count, version);
            return version == _version;
        }

        private bool TryBuildReverseGraph(in ImmutableArray<DependencyVertex<TKey, TValue>> vertices, in int version) {
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
                if (vertex == default) {
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
                    if (vertex != default && !vertex.IsSealed) {
                        vertex.Seal(outgoingVertices[vertex.Index]);
                    }
                }

                return true;
            }
        }

        private bool TryCreateWalkingGraph(in ImmutableArray<DependencyVertex<TKey, TValue>> vertices, in ImmutableArray<int> depths, in int version, out ImmutableArray<WalkingVertex<TKey, TValue>> analysisGraph) {
            if (version != _version) {
                analysisGraph = default;
                return false;
            }

            var nodesByVertexIndex = new Dictionary<int, WalkingVertex<TKey, TValue>>();

            for (var index = 0; index < vertices.Count; index++) {
                var vertex = vertices[index];
                if (vertex == null || vertex.IsWalked || depths[index] == -1) {
                    continue;
                }

                var node = new WalkingVertex<TKey, TValue>(vertices[index]);
                nodesByVertexIndex[index] = node;
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
                        if (vertex == null || depths[vertex.Index] == -1) {
                            continue;
                        }

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
                depths[i] = -1; // Unreachable vertices will be marked as -1
            }

            for (var i = 0; i < depths.Length; i++) {
                var vertex = vertices[i];
                if (vertex != null && vertex.IsRoot) {
                    depths[i] = 0;
                    SetDepths(depths, vertices, vertex.Incoming, 1);
                }
            }
            
            return ImmutableArray<int>.Create(depths);
        }

        private static void SetDepths(in int[] depths, in ImmutableArray<DependencyVertex<TKey, TValue>> vertices, in ImmutableArray<int> indices, int depth) {
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

        private bool TryResolveLoops(in ImmutableArray<WalkingVertex<TKey, TValue>> graph, int loopsCount, int version, out ImmutableArray<WalkingVertex<TKey, TValue>> loopVertices) {
            loopVertices = ImmutableArray<WalkingVertex<TKey, TValue>>.Empty;
            if (loopsCount == 0) {
                return true;
            }

            // Create independent walking vertices for vertex loops
            for (var i = 0; i < loopsCount; i++) {
                loopVertices = loopVertices.Add(new WalkingVertex<TKey, TValue>(default));
            }

            foreach (var vertex in graph) {
                if (vertex.IsInLoop) {
                    var loopVertex = loopVertices[vertex.LoopNumber];

                    // Break internal loop connections
                    for (var i = vertex.Outgoing.Count - 1; i >= 0; i--) {
                        if (vertex.Outgoing[i].LoopNumber == vertex.LoopNumber) {
                            vertex.RemoveOutgoingAt(i);
                        }
                    }

                    loopVertex.AddOutgoing(vertex);
                }

                if (version != _version) {
                    return false;
                }
            }

            foreach (var vertex in graph) {
                for (var i = vertex.Outgoing.Count - 1; i >= 0; i--) {
                    var outgoing = vertex.Outgoing[i];
                    if (outgoing.IsInLoop && outgoing.LoopNumber != vertex.LoopNumber) {
                        var loopVertex = loopVertices[outgoing.LoopNumber];
                        vertex.RemoveOutgoingAt(i);
                        loopVertex.AddOutgoing(outgoing);
                    }
                }

                if (version != _version) {
                    return false;
                }
            }

            return true;
        }

        private bool TryFindMissingDependencies(in ImmutableArray<DependencyVertex<TKey, TValue>> vertices, in ImmutableArray<WalkingVertex<TKey, TValue>> walkingGraph, int version, out ImmutableArray<TKey> missingKeys) {
            var haveMissingDependencies = new bool[vertices.Count];
            var queue = new Queue<DependencyVertex<TKey, TValue>>();
            var missingIndicesHashSet = new HashSet<int>();

            // First, go through all the vertices and find those that have missing incoming edges
            foreach (var vertex in vertices) {
                if (vertex == default) {
                    continue;
                }

                foreach (var incoming in vertex.Incoming) {
                    if (vertices[incoming] == default) {
                        haveMissingDependencies[vertex.Index] = true;
                        queue.Enqueue(vertex);
                        missingIndicesHashSet.Add(incoming);
                    }
                }
            }

            missingKeys = ImmutableArray<TKey>.Empty;

            // From them, go through the edges and mark all reachable vertices
            while (queue.Count > 0) {
                if (version != _version) {
                    return false;
                }

                var vertex = queue.Dequeue();
                foreach (var outgoing in vertex.Outgoing) {
                    if (vertices[outgoing] == default) {
                        continue;
                    }

                    if (haveMissingDependencies[outgoing]) {
                        continue; // This vertex has been visited
                    }

                    haveMissingDependencies[outgoing] = true;
                    queue.Enqueue(vertices[outgoing]);
                }
            }

            foreach (var walkingVertex in walkingGraph) {
                if (walkingVertex.DependencyVertex != default && haveMissingDependencies[walkingVertex.DependencyVertex.Index]) {
                    walkingVertex.MarkHasMissingDependencies();
                }
            }

            lock (_syncObj) {
                foreach (var (key, index) in _keys) {
                    if (missingIndicesHashSet.Contains(index)/* && depths[index] <= walkerDepthLimit*/) {
                        missingKeys = missingKeys.Add(key);
                    }
                }
            }
            
            return version == _version;
        }

        private sealed class DependencyChainWalker : IDependencyChainWalker<TKey, TValue> {
            private readonly DependencyResolver<TKey, TValue> _dependencyResolver;
            private readonly ImmutableArray<WalkingVertex<TKey, TValue>> _startingVertices;
            private readonly ImmutableArray<int> _depths;
            private readonly object _syncObj;
            private int _remaining;
            private PriorityProducerConsumer<IDependencyChainNode> _ppc;

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

            public DependencyChainWalker(in DependencyResolver<TKey, TValue> dependencyResolver,
                in ImmutableArray<WalkingVertex<TKey, TValue>> startingVertices,
                in ImmutableArray<TValue> affectedValues,
                in ImmutableArray<int> depths,
                in ImmutableArray<TKey> missingKeys,
                in int totalNodesCount,
                in int version) {

                _syncObj = new object();
                _dependencyResolver = dependencyResolver;
                _startingVertices = startingVertices;
                _depths = depths;
                AffectedValues = affectedValues;
                Version = version;
                MissingKeys = missingKeys;

                _remaining = totalNodesCount;
            }

            public Task<IDependencyChainNode> GetNextAsync(CancellationToken cancellationToken) {
                PriorityProducerConsumer<IDependencyChainNode> ppc;
                lock (_syncObj) {
                    if (_ppc == null) {
                        _ppc = new PriorityProducerConsumer<IDependencyChainNode>();

                        foreach (var vertex in _startingVertices) {
                            _ppc.Produce(CreateNode(vertex));
                        }
                    }

                    ppc = _ppc;
                }

                return ppc.ConsumeAsync(cancellationToken);
            }

            public void MoveNext(WalkingVertex<TKey, TValue> vertex) {
                var verticesToProduce = new List<WalkingVertex<TKey, TValue>>();
                var isCompleted = false;
                lock (_syncObj) {
                    _remaining--;
                    foreach (var outgoing in vertex.Outgoing) {
                        if (!outgoing.HasIncoming) {
                            continue;
                        }

                        outgoing.DecrementIncoming(vertex.HasOnlyWalkedIncoming);
                        if (outgoing.HasIncoming) {
                            continue;
                        }

                        verticesToProduce.Add(outgoing);
                    }

                    if (_remaining == 0) {
                        isCompleted = true;
                    }
                }

                if (isCompleted) {
                    _ppc.Produce(null);
                } else {
                    foreach (var toProduce in verticesToProduce) {
                        _ppc.Produce(CreateNode(toProduce));
                    }
                }
            }

            public bool IsValidVersion {
                get {
                    lock (_dependencyResolver._syncObj) {
                        return _dependencyResolver._version == Version;
                    }
                }
            }

            private IDependencyChainNode CreateNode(WalkingVertex<TKey, TValue> vertex) {
                if (vertex.DependencyVertex != default) { 
                    return new SingleNode(this, vertex, _depths[vertex.DependencyVertex.Index]);
                }

                var vertices = vertex.Outgoing;
                var values = vertices.Select(v => v.DependencyVertex.Value).ToImmutableArray();
                var depth = vertices.Min(v => _depths[v.DependencyVertex.Index]);
                var hasMissingDependencies = vertices.Any(v => v.HasMissingDependencies);
                return new LoopNode(this, vertices, values, depth, hasMissingDependencies);
            }
        }

        private sealed class SingleNode : IDependencyChainSingleNode<TValue> {
            private readonly WalkingVertex<TKey, TValue> _vertex;
            private DependencyChainWalker _walker;
            public TValue Value => _vertex.DependencyVertex.Value;
            public int VertexDepth { get; }
            public bool HasMissingDependencies => _vertex.HasMissingDependencies;
            public bool HasOnlyWalkedDependencies => _vertex.HasOnlyWalkedIncoming && _vertex.SecondPass == default;
            public bool IsWalkedWithDependencies => _vertex.HasOnlyWalkedIncoming && _vertex.DependencyVertex.IsWalked;
            public bool IsValidVersion => _walker.IsValidVersion;

            public SingleNode(DependencyChainWalker walker, WalkingVertex<TKey, TValue> vertex, int depth) => (_walker, _vertex, VertexDepth) = (walker, vertex, depth);

            public void MarkWalked() => _vertex.DependencyVertex.MarkWalked();

            public void MoveNext() => Interlocked.Exchange(ref _walker, null)?.MoveNext(_vertex);
        }

        private sealed class LoopNode : IDependencyChainLoopNode<TValue> {
            private readonly IReadOnlyList<WalkingVertex<TKey, TValue>> _vertices;
            private DependencyChainWalker _walker;

            public int VertexDepth { get; }
            public bool HasMissingDependencies { get; }
            public bool HasOnlyWalkedDependencies => _vertices.All(v => v.HasOnlyWalkedIncoming);
            public bool IsWalkedWithDependencies => _vertices.All(v => v.HasOnlyWalkedIncoming && v.DependencyVertex.IsWalked);
            public bool IsValidVersion => _walker.IsValidVersion;

            public ImmutableArray<TValue> Values { get; }

            public LoopNode(DependencyChainWalker walker, IReadOnlyList<WalkingVertex<TKey, TValue>> vertices, ImmutableArray<TValue> values, int depth, bool hasMissingDependencies) 
                => (_walker, _vertices, Values, VertexDepth, HasMissingDependencies) = (walker, vertices, values, depth, hasMissingDependencies);

            public void MarkWalked() {
                foreach (var vertex in _vertices) {
                    vertex.DependencyVertex.MarkWalked();
                }
            }

            public void MoveNext() {
                var walker = Interlocked.Exchange(ref _walker, null);
                if (walker != null) {
                    foreach (var vertex in _vertices) {
                        walker.MoveNext(vertex);
                    }
                }
            }
        }
    }
}
