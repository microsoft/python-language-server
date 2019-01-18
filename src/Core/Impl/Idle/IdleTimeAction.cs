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
using System.Collections.Concurrent;
using Microsoft.Python.Core.Shell;

namespace Microsoft.Python.Core.Idle {
    /// <summary>
    /// Action that should be executed on next idle after certain number of milliseconds
    /// </summary>
    public class IdleTimeAction {
        private static readonly ConcurrentDictionary<object, IdleTimeAction> _idleActions 
            = new ConcurrentDictionary<object, IdleTimeAction>();

        private readonly Action _action;
        private readonly int _delay;
        private readonly IIdleTimeService _idleTime;
        private readonly object _tag;
        private volatile bool _connectedToIdle;
        private DateTime _idleConnectTime;

        /// <summary>
        /// Create delayed idle time action
        /// </summary>
        /// <param name="action">Action to execute on idle</param>
        /// <param name="delay">Minimum number of milliseconds to wait before executing the action</param>
        /// <param name="tag">Object that uniquely identifies the action. Typically creator object.</param>
        /// <param name="idleTime">Idle time service</param>
        public static void Create(Action action, int delay, object tag, IIdleTimeService idleTime) {
            if (!_idleActions.TryGetValue(tag, out var existingAction)) {
                existingAction = new IdleTimeAction(action, delay, tag, idleTime);
                _idleActions[tag] = existingAction;
            }
        }

        /// <summary>
        /// Cancels idle time action. Has no effect if action has already been executed.
        /// </summary>
        /// <param name="tag">Tag identifying the action to cancel</param>
        public static void Cancel(object tag) {
            if (_idleActions.TryRemove(tag, out var idleTimeAction)) {
                idleTimeAction.DisconnectFromIdle();
            }
        }

        private IdleTimeAction(Action action, int delay, object tag, IIdleTimeService idleTime) {
            _action = action;
            _delay = delay;
            _tag = tag;
            _idleTime = idleTime;

            ConnectToIdle();
        }

        void OnIdle(object sender, EventArgs e) {
            if (_idleConnectTime.MillisecondsSinceUtc() > _delay) {
                DisconnectFromIdle();
                _action();
                _idleActions.TryRemove(_tag, out _);
            }
        }

        void ConnectToIdle() {
            if (!_connectedToIdle) {
                _idleTime.Idle += OnIdle;

                _idleConnectTime = DateTime.UtcNow;
                _connectedToIdle = true;
            }
        }

        void DisconnectFromIdle() {
            if (_connectedToIdle) {
                _idleTime.Idle -= OnIdle;
                _connectedToIdle = false;
            }
        }
    }
}
