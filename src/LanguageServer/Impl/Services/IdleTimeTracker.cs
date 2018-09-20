// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading;

namespace Microsoft.Python.LanguageServer.Services {
    sealed class IdleTimeTracker : IDisposable {
        private readonly int _delay;
        private readonly Action _action;
        private Timer _timer;
        private DateTime _lastActivityTime;

        public IdleTimeTracker(int msDelay, Action action) {
            _delay = msDelay;
            _action = action;
            _timer = new Timer(OnTimer, this, 50, 50);
            NotifyUserActivity();
        }

        public void NotifyUserActivity() => _lastActivityTime = DateTime.Now;

        public void Dispose() {
            _timer?.Dispose();
            _timer = null;
        }

        private void OnTimer(object state) {
            if ((DateTime.Now - _lastActivityTime).TotalMilliseconds >= _delay && _timer != null) {
                _action();
            }
        }
    }
}
