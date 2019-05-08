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
using System.Threading;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Dependencies {
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    internal sealed class DependencyVertex<TKey, TValue> {
        public TKey Key { get; }
        public TValue Value { get; }
        public bool IsRoot { get; }
        public int Version { get; }
        public int Index { get; }
        public string DebuggerDisplay => $"{Key}:{Value}";

        public bool IsSealed => _state >= (int)State.Sealed;
        public bool IsWalked => _state == (int)State.Walked;

        public ImmutableArray<int> Incoming { get; }

        private int _state;
        private HashSet<int> _outgoing;
        private static HashSet<int> _empty = new HashSet<int>();

        public DependencyVertex(DependencyVertex<TKey, TValue> oldVertex, int version) {
            Key = oldVertex.Key;
            Value = oldVertex.Value;
            IsRoot = oldVertex.IsRoot;
            Index = oldVertex.Index;
            Incoming = oldVertex.Incoming;

            Version = version;

            _outgoing = oldVertex.Outgoing;
            _state = oldVertex.IsWalked ? (int)State.ChangedOutgoing : (int)State.New;
        }

        public DependencyVertex(TKey key, TValue value, bool isRoot, ImmutableArray<int> incoming, int version, int index) {
            Key = key;
            Value = value;
            IsRoot = isRoot;
            Version = version;
            Index = index;
            Incoming = incoming;

            _state = (int)State.New;
        }

        public bool ContainsOutgoing(int index) => _outgoing != null && _outgoing.Contains(index);
        public HashSet<int> Outgoing => _outgoing ?? _empty;

        public void Seal(HashSet<int> outgoing) {
            Debug.Assert(_state <= (int)State.ChangedOutgoing);
            _state = _state == (int)State.ChangedOutgoing ? (int)State.Walked : (int)State.Sealed;
            _outgoing = outgoing;
        }

        public void MarkWalked() {
            Debug.Assert(_state >= (int)State.Sealed);
            Interlocked.Exchange(ref _state, (int)State.Walked);
        }

        private enum State {
            New = 0,
            ChangedOutgoing = 1,
            Sealed = 2,
            Walked = 3
        }
    }
}
