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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Threading;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class DependencyResolver<TKey, TValue> : IDependencyResolver<TKey, TValue> {
        private readonly IDependencyFinder<TKey, TValue> _dependencyFinder;
        private readonly DependencyGraph<TKey, TValue> _vertices = new DependencyGraph<TKey, TValue>();
        private readonly Dictionary<TKey, DependencyVertex<TKey, TValue>> _changedVertices = new Dictionary<TKey, DependencyVertex<TKey, TValue>>();
        private readonly object _syncObj = new object();

        public IDependencyChainWalker<TKey, TValue> NotifyChanges(TKey key, TValue value, params TKey[] incomingKeys) 
            => NotifyChanges(key, value, ImmutableArray<TKey>.Create(incomingKeys));

        public IDependencyChainWalker<TKey, TValue> NotifyChanges(TKey key, TValue value, ImmutableArray<TKey> incomingKeys) {
            int version;
            ImmutableArray<DependencyVertex<TKey, TValue>> changedVertices;
            DependencyGraphSnapshot<TKey, TValue> snapshot;

            lock (_syncObj) {
                var dependencyVertex = _vertices.AddOrUpdate(key, value, incomingKeys);
                snapshot = _vertices.Snapshot;

                version = _vertices.Snapshot.Version;
                _changedVertices[key] = dependencyVertex;
                changedVertices = ImmutableArray<DependencyVertex<TKey, TValue>>.Create(_changedVertices.Values);
            }

            var walkingGraph = CreateWalkingGraph(snapshot.Vertices, changedVertices);
            var affectedValues = walkingGraph.Select(v => v.DependencyVertex.Value);

            var loopsCount = FindLoops(walkingGraph);
            var (startingVertices, totalNodesCount) = ResolveLoops(walkingGraph, loopsCount);
            foreach (var vertex in walkingGraph) {
                vertex.Seal();
                vertex.SecondPass?.Seal();
            }

            return new DependencyChainWalker(this, startingVertices, affectedValues, snapshot.MissingKeys, totalNodesCount, version);
        }

        private void CommitChanges(DependencyVertex<TKey, TValue> vertex) {
            lock (_syncObj) {
                if (_changedVertices.TryGetValue(vertex.Key, out var changedVertex) && changedVertex.Version <= vertex.Version) {
                    _changedVertices.Remove(vertex.Key);
                }
            }
        }

        private ImmutableArray<WalkingVertex<TKey, TValue>> CreateWalkingGraph(ImmutableArray<DependencyVertex<TKey, TValue>> snapshot, ImmutableArray<DependencyVertex<TKey, TValue>> changedVertices) {
            var analysisGraph = ImmutableArray<WalkingVertex<TKey, TValue>>.Empty;
            var nodesByVertexIndex = new Dictionary<int, WalkingVertex<TKey, TValue>>();

            foreach (var vertex in changedVertices) {
                var node = new WalkingVertex<TKey, TValue>(snapshot[vertex.Index]);
                analysisGraph = analysisGraph.Add(node);
                nodesByVertexIndex[vertex.Index] = node;
            }

            var queue = new Queue<WalkingVertex<TKey, TValue>>(analysisGraph);
            while (queue.Count > 0) {
                var node = queue.Dequeue();
                foreach (var outgoingIndex in node.DependencyVertex.Outgoing) {
                    if (!nodesByVertexIndex.TryGetValue(outgoingIndex, out var outgoingNode)) {
                        outgoingNode = new WalkingVertex<TKey, TValue>(snapshot[outgoingIndex]);
                        analysisGraph = analysisGraph.Add(outgoingNode);
                        nodesByVertexIndex[outgoingIndex] = outgoingNode;

                        queue.Enqueue(outgoingNode);
                    }

                    node.AddOutgoing(outgoingNode);
                }
            }

            return analysisGraph;
        }

        private int FindLoops(ImmutableArray<WalkingVertex<TKey, TValue>> graph) {
            var index = 0;
            var loopNumber = 0;
            var stackP = new Stack<WalkingVertex<TKey, TValue>>();
            var stackS = new Stack<WalkingVertex<TKey, TValue>>();

            foreach (var vertex in graph) {
                if (vertex.Index == -1) {
                    CheckForLoop(vertex, stackP, stackS, ref index, ref loopNumber);
                }
            }

            return loopNumber;
        }

        private void CheckForLoop(WalkingVertex<TKey, TValue> vertex, Stack<WalkingVertex<TKey, TValue>> stackP, Stack<WalkingVertex<TKey, TValue>> stackS, ref int counter, ref int loopNumber) {
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

        private static bool SetLoopNumber(WalkingVertex<TKey, TValue> vertex, Stack<WalkingVertex<TKey, TValue>> stackS, int loopIndex) {
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

        private static (ImmutableArray<WalkingVertex<TKey, TValue>>, int) ResolveLoops(ImmutableArray<WalkingVertex<TKey, TValue>> graph, int loopsCount) {
            // Create vertices for second pass
            var inLoopsCount = 0;
            var secondPassLoops = new List<WalkingVertex<TKey, TValue>>[loopsCount];
            foreach (var vertex in graph) {
                if (vertex.IsInLoop) {
                    var secondPassVertex = vertex.CreateSecondPassVertex();
                    var loopNumber = vertex.LoopNumber;
                    if (secondPassLoops[loopNumber] == null) {
                        secondPassLoops[loopNumber] = new List<WalkingVertex<TKey, TValue>> { secondPassVertex };
                    } else {
                        secondPassLoops[loopNumber].Add(secondPassVertex);
                    }

                    inLoopsCount++;
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
                }
            }

            // Make all vertices from second pass loop have incoming edges from vertices from first pass loop and set unique loop numbers
            foreach (var loop in secondPassLoops) {
                foreach (var secondPassVertex in loop) {
                    var firstPassVertex = secondPassVertex.FirstPass;
                    secondPassVertex.LoopNumber = loopsCount;
                    firstPassVertex.AddOutgoing(loop);

                    // Copy outgoing edges to the second pass vertex
                    foreach (var outgoingVertex in firstPassVertex.Outgoing) {
                        if (outgoingVertex.LoopNumber == firstPassVertex.LoopNumber) {
                            secondPassVertex.AddOutgoing(outgoingVertex.SecondPass);
                        }
                    }
                }

                loopsCount++;
            }

            // Iterate original graph to get starting vertices
            return (graph.Where(v => v.IncomingCount == 0), graph.Count + inLoopsCount);
        }

        private static void RemoveLoopEdges(WalkingVertex<TKey, TValue> vertex, ref int counter) {
            vertex.Index = counter++;
            for (var i = vertex.Outgoing.Count - 1; i >= 0; i--) {
                var outgoing = vertex.Outgoing[i];
                if (outgoing.Index == -1) {
                    RemoveLoopEdges(outgoing, ref counter);
                } else if (outgoing.Index < vertex.Index) {
                    vertex.RemoveOutgoingAt(i);
                }
            }
        }

        private sealed class DependencyChainWalker : IDependencyChainWalker<TKey, TValue> {
            private readonly DependencyResolver<TKey, TValue> _dependencyResolver;
            private readonly ImmutableArray<WalkingVertex<TKey, TValue>> _startingVertices;
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
                in DependencyResolver<TKey, TValue> dependencyResolver,
                in ImmutableArray<WalkingVertex<TKey, TValue>> startingVertices,
                in ImmutableArray<TValue> affectedValues,
                in ImmutableArray<TKey> missingKeys,
                in int totalNodesCount,
                in int version) {

                _syncObj = new object();
                _dependencyResolver = dependencyResolver;
                _startingVertices = startingVertices;
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
                            _ppc.Produce(new DependencyChainNode(this, vertex));
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
                    _dependencyResolver.CommitChanges(vertex.DependencyVertex);
                }

                if (isCompleted) {
                    _ppc.Produce(null);
                } else {
                    foreach (var toProduce in verticesToProduce) {
                        _ppc.Produce(new DependencyChainNode(this, toProduce));
                    }
                }
            }
        }

        private sealed class DependencyChainNode : IDependencyChainNode<TValue> {
            private readonly WalkingVertex<TKey, TValue> _vertex;
            private DependencyChainWalker _walker;
            public TValue Value => _vertex.DependencyVertex.Value;

            public DependencyChainNode(DependencyChainWalker walker, WalkingVertex<TKey, TValue> vertex) {
                _walker = walker;
                _vertex = vertex;
            }

            public void Commit() => Interlocked.Exchange(ref _walker, null)?.MarkCompleted(_vertex, true);
            public void Skip() => Interlocked.Exchange(ref _walker, null)?.MarkCompleted(_vertex, false);
        }
    }
}
