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
using System.Threading;
using Microsoft.Python.Core.Disposables;

namespace Microsoft.Python.Analysis.Utilities {
    internal sealed class ReentrancyGuard<T> {
        private readonly AsyncLocal<Stack<T>> _stack = new AsyncLocal<Stack<T>>();

        public IDisposable Push(T t, out bool reentered) {
            var localStack = _stack.Value;
            if (localStack != null) {
                if (localStack.Contains(t)) {
                    reentered = true;
                    return Disposable.Empty;
                }
            } else {
                _stack.Value = localStack = new Stack<T>();
            }

            reentered = false;
            localStack.Push(t);
            return Disposable.Create(Pop);
        }

        public void Pop() {
            _stack.Value.Pop();
            if (_stack.Value.Count == 0) {
                _stack.Value = null;
            }
        }
    }
}
