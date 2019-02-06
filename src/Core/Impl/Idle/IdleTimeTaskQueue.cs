// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Python.Core.Shell;

namespace Microsoft.Python.Core.Idle {
    /// <summary>
    /// A queue of asynchronous tasks that are processed in the order they were added.
    /// As opposed to the thread pool tasks only start on idle and queued task may be
    /// canceled if needed. There may be one or more tasks running in parallel depending
    /// on number of CPUs available.
    /// </summary>
    public sealed class IdleTimeTaskQueue {
        class ActionEntry {
            public Func<object, object> Action { get; }
            public Action<object> OnCompleted { get; }
            public Action OnCanceled { get; }
            public object Data { get; }
            public object Tag { get; }

            public ActionEntry(Func<object, object> action, Action<object> onCompleted, Action onCanceled, object data, object tag) {
                Action = action;
                OnCompleted = onCompleted;
                OnCanceled = onCanceled;
                Data = data;
                Tag = tag;
            }
        }

        private readonly IServiceContainer _services;
        private readonly IIdleTimeService _idleTime;
        private readonly List<ActionEntry> _actionQueue = new List<ActionEntry>();
        private readonly Task[] _runningTasks;
        private readonly object _lock = new object();
        private bool _connectedToIdle;

        public IdleTimeTaskQueue(IServiceContainer services) {
            _services = services;
            _idleTime = services.GetService<IIdleTimeService>();

            var taskCount = Math.Max(Environment.ProcessorCount / 2, 1);
            _runningTasks = new Task[taskCount];
        }

        /// <summary>
        /// Add task to the idle time queue. Tasks are executed asynchronously
        /// in the order they were added. On next idle time if thread is available
        /// it will take task from the head of the queue and execute it. 
        /// There may be one or more tasks running in parallel depending on
        /// the number of CPUs available.
        /// </summary>
        public void Enqueue(Func<object, object> taskAction, Action<object> onCompleted, Action onCanceled, object p, object tag) {
            lock (_lock) {
                _actionQueue.Add(new ActionEntry(taskAction, onCompleted, onCanceled, p, tag));
                ConnectToIdle();
            }
        }

        /// <summary>
        /// Add task to the idle time queue. Tasks are executed asynchronously in the order they were added.
        /// On next idle time if thread is available it will take task from the head of the queue and execute it. 
        /// There may be one or more tasks running in parallel depending on number of CPUs available.
        /// </summary>
        public void Enqueue(Func<object, object> taskAction, Action<object> callbackAction, object data, object tag)
            => Enqueue(taskAction, callbackAction, null, data, tag);

        /// <summary>
        /// Removes tasks associated with a give callback
        /// </summary>
        /// <param name="tag">Object uniquely identifying the task</param>
        public void Cancel(object tag) {
            ActionEntry e = null;

            lock (_lock) {
                if (_actionQueue.Count > 0) {
                    for (var i = _actionQueue.Count - 1; i >= 0; i--) {
                        if (_actionQueue[i].Tag == tag) {
                            e = _actionQueue[i];
                            _actionQueue.RemoveAt(i);
                        }
                    }

                    if (_actionQueue.Count == 0) {
                        DisconnectFromIdle();
                    }
                }
            }

            e?.OnCanceled?.Invoke();
        }

        public void IncreasePriority(object tag) {
            lock (_lock) {
                for (var i = 0; i < _actionQueue.Count; i++) {
                    var task = _actionQueue[i];

                    if (task.Tag == tag) {
                        _actionQueue.RemoveAt(i);
                        _actionQueue.Insert(0, task);
                        break;
                    }
                }
            }
        }

        private void ConnectToIdle() {
            if (!_connectedToIdle) {
                _connectedToIdle = true;
                _idleTime.Idle += OnIdle;
            }
        }

        private void DisconnectFromIdle() {
            if (_connectedToIdle) {
                _connectedToIdle = false;
                _idleTime.Idle -= OnIdle;
            }
        }

        private void OnIdle(object sender, EventArgs _) {
            lock (_lock) {
                for (var i = 0; i < _actionQueue.Count; i++) {
                    var index = GetAvailableWorkerTask();
                    if (index < 0) {
                        return; // all worker threads are busy
                    }

                    var e = _actionQueue[0];
                    _actionQueue.RemoveAt(0);

                    _runningTasks[index] = Task.Run(() => e.Action(e.Data))
                        .ContinueWith(t => {
                            if (t.IsCompleted) {
                                e.OnCompleted(t.Result);
                            } else if (t.IsCanceled) {
                                e.OnCanceled?.Invoke();
                            }
                    });

                    if (_actionQueue.Count == 0) {
                        DisconnectFromIdle();
                    }
                }
            }
        }

        private int GetAvailableWorkerTask() {
            for (var i = 0; i < _runningTasks.Length; i++) {
                var t = _runningTasks[i];
                if (t == null || t.IsCompleted || t.IsCanceled || t.IsFaulted) {
                    return i;
                }
            }
            return -1;
        }
    }
}
