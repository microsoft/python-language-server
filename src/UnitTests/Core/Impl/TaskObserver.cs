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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities {
    internal class TaskObserver {
        private readonly int _secondsTimeout;
        private readonly Action<Task> _afterTaskCompleted;
        private readonly TaskCompletionSource<Exception> _tcs;
        private readonly ConcurrentDictionary<Task, StackTrace> _stackTraces;
        private int _count;
        private bool _isTestCompleted;

        public TaskObserver(int secondsTimeout) {
            _secondsTimeout = secondsTimeout;
            _afterTaskCompleted = AfterTaskCompleted;
            _tcs = new TaskCompletionSource<Exception>();
            _stackTraces = new ConcurrentDictionary<Task, StackTrace>();
        }

        public bool TryAdd(Task task) {
            if (!_stackTraces.TryAdd(task, GetFilteredStackTrace())) {
                return false;
            }

            Interlocked.Increment(ref _count);
            // No reason to watch for task if it is completed already
            if (task.IsCompleted) {
                AfterTaskCompleted(task);
            } else {
                task.ContinueWith(_afterTaskCompleted, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            return true;
        }
        
        public void WaitForObservedTask() {
            TestCompleted();
            _tcs.Task.Wait(_secondsTimeout * 1000);

            try {
                Summarize();
            } finally {
                _stackTraces.Clear();
            }
        }

        [CustomAssertion]
        private void Summarize() { 
            var incompleteTasks = new Queue<(Task, StackTrace)>();
            var failedTasks = new Queue<Exception>();
            foreach (var kvp in _stackTraces) {
                var task = kvp.Key;
                var stackTrace = kvp.Value;

                if (!task.IsCompleted) {
                    incompleteTasks.Enqueue((task, stackTrace));
                } else if (task.IsFaulted && task.Exception != null) {
                    var aggregateException = task.Exception.Flatten();
                    var exception = aggregateException.InnerExceptions.Count == 1
                        ? aggregateException.InnerException
                        : aggregateException;

                    failedTasks.Enqueue(exception);
                }
            }

            if (incompleteTasks.Count == 0 && failedTasks.Count == 0) {
                return;
            }

            var message = new StringBuilder();
            var hasIncompleteTasks = incompleteTasks.Count > 0;
            var hasFailedTasks = failedTasks.Count > 0;
            if (hasIncompleteTasks) {
                if (incompleteTasks.Count > 1) {
                    message
                        .Append(incompleteTasks.Count)
                        .AppendLine(" tasks that have been started during test run are still not completed:")
                        .AppendLine();
                } else {
                    message
                        .AppendLine("One task that has been started during test run is still not completed:")
                        .AppendLine();
                }

                while (incompleteTasks.Count > 0) {
                    var (task, stackTrace) = incompleteTasks.Dequeue();
                    message
                        .Append("Id: ")
                        .Append(task.Id)
                        .Append(", status: ")
                        .Append(task.Status)
                        .AppendLine()
                        .AppendLine(new EnhancedStackTrace(stackTrace).ToString())
                        .AppendLine();
                }

                if (hasFailedTasks) {
                    message
                        .Append("Also, ");
                }
            }

            if (hasFailedTasks) {
                if (failedTasks.Count > 1) {
                    message
                        .Append(failedTasks.Count)
                        .AppendLine(" not awaited tasks have failed:")
                        .AppendLine();
                } else {
                    message
                        .Append(hasIncompleteTasks ? "one" : "One")
                        .AppendLine(" not awaited tasks has failed:")
                        .AppendLine();
                }
            }

            while (failedTasks.Count > 0) {
                var exception = failedTasks.Dequeue();
                message
                    .AppendDemystified(exception)
                    .AppendLine()
                    .AppendLine();
            }

            Assert.Fail(message.ToString());
            //Execute.Assertion.FailWith(message.ToString());
        }

        private void TestCompleted() {
            Volatile.Write(ref _isTestCompleted, true);
            if (_count == 0) {
                _tcs.TrySetResult(null);
            }
        }

        private void AfterTaskCompleted(Task task) {
            var count = Interlocked.Decrement(ref _count);
            if (!task.IsFaulted) {
                _stackTraces.TryRemove(task, out _);
            } else if (Debugger.IsAttached) {
                Debugger.Break();
            }

            if (count == 0 && Volatile.Read(ref _isTestCompleted)) {
                _tcs.TrySetResult(null);
            }
        }

        private static StackTrace GetFilteredStackTrace() {
            var skipCount = 2;
            var stackTrace = new StackTrace(skipCount, true);
            var frames = stackTrace.GetFrames();
            if (frames == null) {
                return stackTrace;
            }
            
            foreach (var frame in frames) {
                skipCount++;
                var frameMethod = frame.GetMethod();
                if (frameMethod.Name == "DoNotWait" && frameMethod.DeclaringType?.Name == "TaskExtensions") {
                    return new StackTrace(skipCount, true);
                }
            }

            return stackTrace;
        }
    }
}
