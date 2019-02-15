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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Dependencies {
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    internal sealed class DependencyVertex<TKey, TValue> {
        private readonly CancellationTokenSource _incomingKeysCts;
        private TaskCompletionSource<int> _incomingKeysTcs;

        public TKey Key { get; }
        public TValue Value { get; }
        public int Version { get; }
        public int Index { get; }
        public string DebuggerDisplay => $"{Key}:{Value}";

        public bool IsSealed { get; private set; }
        public bool HasMissingKeys { get; private set; }

        public ImmutableArray<TKey> IncomingKeys { get; private set; }
        public ImmutableArray<int> Incoming { get; private set; }
        public ImmutableArray<int> Outgoing { get; private set; }

        public DependencyVertex(DependencyVertex<TKey, TValue> oldVertex, TValue value, int version) {
            Key = oldVertex.Key;
            Value = value;
            Version = version;
            Index = oldVertex.Index;

            _incomingKeysCts = new CancellationTokenSource();
            oldVertex._incomingKeysCts.Cancel();

            IncomingKeys = oldVertex.IncomingKeys;
            Incoming = oldVertex.Incoming;
            Outgoing = oldVertex.Outgoing;
        }

        public DependencyVertex(TKey key, TValue value, int index, int version) {
            Key = key;
            Value = value;
            Version = version;
            Index = index;

            _incomingKeysCts = new CancellationTokenSource();

            IncomingKeys = ImmutableArray<TKey>.Empty;
            Incoming = ImmutableArray<int>.Empty;
            Outgoing = ImmutableArray<int>.Empty;
        }

        public Task EnsureDependenciesAsync(IDependencyFinder<TKey, TValue> dependencyFinder) {
            if (_incomingKeysTcs != null) {
                return _incomingKeysTcs.Task;
            }

            var tcs = new TaskCompletionSource<int>();
            if (Interlocked.CompareExchange(ref _incomingKeysTcs, tcs, null) == null) {
                return FindDependenciesAsync(dependencyFinder);
            }

            return _incomingKeysTcs.Task;
        }

        private async Task FindDependenciesAsync(IDependencyFinder<TKey, TValue> dependencyFinder) {
            try {
                IncomingKeys = await dependencyFinder.FindDependenciesAsync(Value, _incomingKeysCts.Token);
            } catch (OperationCanceledException e) {
                _incomingKeysTcs.TrySetCanceled(e.CancellationToken);
                throw;
            } catch (Exception ex) {
                _incomingKeysTcs.TrySetException(ex);
                throw;
            }
            _incomingKeysTcs.TrySetResult(0);
        }

        public void AddOutgoing(int index) {
            AssertIsNotSealed();
            Outgoing = Outgoing.Add(index);
        }

        public void RemoveOutgoing(int index) {
            AssertIsNotSealed();
            Outgoing = Outgoing.Remove(index);
        }

        public void SetIncoming(ImmutableArray<int> incoming) {
            AssertIsNotSealed();
            Incoming = incoming;
        }

        public void SetHasMissingKeys() {
            AssertIsNotSealed();
            HasMissingKeys = true;
        }

        public void Seal() => IsSealed = true;

        [Conditional("DEBUG")]
        private void AssertIsNotSealed() => Debug.Assert(!IsSealed);
    }
}
