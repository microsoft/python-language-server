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
using System.Diagnostics;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Analysis.Dependencies {
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    internal sealed class WalkingVertex<TKey, TValue> {
        public static Comparison<WalkingVertex<TKey, TValue>> FirstPassIncomingComparison { get; } = (v1, v2) => v1.FirstPass._incomingCount.CompareTo(v2.FirstPass._incomingCount);

        private readonly List<WalkingVertex<TKey, TValue>> _outgoing;
        private bool _isSealed;
        private int _incomingCount;
        private int _walkedIncomingCount;
        public DependencyVertex<TKey, TValue> DependencyVertex { get; }
        public IReadOnlyList<WalkingVertex<TKey, TValue>> Outgoing => _outgoing;

        public int Index { get; set; }
        public int LoopNumber { get; set; }
        public bool HasIncoming => _incomingCount != 0;
        public bool HasOnlyWalkedIncoming => _walkedIncomingCount == 0;
        public bool HasMissingDependencies { get; private set; }

        public WalkingVertex<TKey, TValue> FirstPass { get; }
        public WalkingVertex<TKey, TValue> SecondPass { get; private set; }

        public bool IsInLoop => LoopNumber >= 0;

        public string DebuggerDisplay => DependencyVertex.DebuggerDisplay;

        public WalkingVertex(DependencyVertex<TKey, TValue> vertex, WalkingVertex<TKey, TValue> firstPass = null) {
            DependencyVertex = vertex;
            FirstPass = firstPass;
            Index = -1;
            LoopNumber = firstPass?.LoopNumber ?? -1;
            _outgoing = new List<WalkingVertex<TKey, TValue>>();
        }

        public void MarkHasMissingDependencies() {
            CheckNotSealed();
            HasMissingDependencies = true;
        }

        public void AddOutgoing(WalkingVertex<TKey, TValue> outgoingVertex) {
            CheckNotSealed();

            _outgoing.Add(outgoingVertex);
            outgoingVertex._incomingCount++;
            outgoingVertex._walkedIncomingCount++;
        }

        public void AddOutgoing(HashSet<WalkingVertex<TKey, TValue>> loop) {
            CheckNotSealed();

            _outgoing.AddRange(loop);
            foreach (var outgoingVertex in loop) {
                outgoingVertex._incomingCount++;
                outgoingVertex._walkedIncomingCount++;
            }
        }

        public void RemoveOutgoingAt(int index) {
            CheckNotSealed();

            var outgoingVertex = _outgoing[index];
            _outgoing.RemoveAt(index);
            outgoingVertex._incomingCount--;
            outgoingVertex._walkedIncomingCount--;
        }

        public WalkingVertex<TKey, TValue> CreateSecondPassVertex() {
            CheckNotSealed();

            SecondPass = new WalkingVertex<TKey, TValue>(DependencyVertex, this);
            return SecondPass;
        }

        public void Seal() => _isSealed = true;

        public void DecrementIncoming(bool isWalkedIncoming) {
            CheckSealed();
            _incomingCount--;
            if (isWalkedIncoming) {
                _walkedIncomingCount--;
            }
        }

        private void CheckSealed() => Check.InvalidOperation(_isSealed);
        private void CheckNotSealed() => Check.InvalidOperation(!_isSealed);
    }
}
