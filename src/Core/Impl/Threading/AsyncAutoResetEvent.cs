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
using Microsoft.Python.Core.Threading;

namespace Microsoft.Python.Core {
    public sealed class AsyncAutoResetEvent {
        private readonly Queue<TaskCompletionSource<bool>> _waiters = new Queue<TaskCompletionSource<bool>>(); 
        private bool _isSignaled;

        public Task WaitAsync(in CancellationToken cancellationToken = default) {
            TaskCompletionSource<bool> tcs;
            
            lock (_waiters) { 
                if (_isSignaled) { 
                    _isSignaled = false; 
                    return Task.CompletedTask; 
                }
                
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); 
                _waiters.Enqueue(tcs); 
            }

            if (cancellationToken.CanBeCanceled) {
                tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(tcs.Task);
            }

            return tcs.Task; 
        }

        public void Set() {
            var  waiterToRelease = default(TaskCompletionSource<bool>);
            lock (_waiters) {
                while (_waiters.Count > 0) {
                    waiterToRelease = _waiters.Dequeue();
                    if (!waiterToRelease.Task.IsCompleted) {
                        break;
                    }
                }

                if (!_isSignaled && (waiterToRelease == default || waiterToRelease.Task.IsCompleted)) {
                    _isSignaled = true;
                }
            }

            if (waiterToRelease != null && !waiterToRelease.Task.IsCompleted) {
                waiterToRelease.TrySetResult(true);
            }
        }
    }
}
