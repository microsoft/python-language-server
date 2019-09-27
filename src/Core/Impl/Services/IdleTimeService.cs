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
using System.Threading;
using Microsoft.Python.Core.Idle;

namespace Microsoft.Python.Core.Services {
    public sealed class IdleTimeService : IIdleTimeService, IIdleTimeTracker, IDisposable {
        private static readonly TimeSpan s_initialDelay = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan s_interval = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan s_idleInterval = TimeSpan.FromMilliseconds(100);

        private Timer _timer;
        private DateTime _lastActivityTime;

        public IdleTimeService() {
            _timer = new Timer(OnTimer, state: this, s_initialDelay, s_interval);
            NotifyUserActivity();
        }

        public void NotifyUserActivity() => _lastActivityTime = DateTime.Now;

        public void Dispose() {
            _timer?.Dispose();
            _timer = null;

            Closing?.Invoke(this, EventArgs.Empty);
        }

        private void OnTimer(object state) {
            if (_timer != null && (DateTime.Now - _lastActivityTime) >= s_idleInterval) {
                Idle?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler<EventArgs> Idle;
        public event EventHandler<EventArgs> Closing;
    }
}
