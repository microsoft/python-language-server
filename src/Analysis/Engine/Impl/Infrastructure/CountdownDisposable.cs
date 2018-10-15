// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    public sealed class CountdownDisposable {
        private readonly Action _createAction;
        private readonly Action _disposeAction;
        private int _count;
        public int Count => _count;

        public CountdownDisposable(Action disposeAction = null) : this (() => { }, disposeAction) { }
        public CountdownDisposable(Action createAction, Action disposeAction) {
            _createAction = createAction ?? (() => {});
            _disposeAction = disposeAction ?? (() => {});
        }

        public IDisposable Increment() {
            if (Interlocked.Increment(ref _count) == 1) {
                _createAction();
            }

            return Disposable.Create(Decrement);
        }

        public void Decrement() {
            if (Interlocked.Decrement(ref _count) == 0) {
                _disposeAction();
            }
        }
    }
}
