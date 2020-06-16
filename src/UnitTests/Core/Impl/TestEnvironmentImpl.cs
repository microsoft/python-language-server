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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TestUtilities {
    public class TestEnvironmentImpl {
        private static readonly FieldInfo _stackTraceStringField = typeof(Exception).GetField("_stackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _showDialogField = typeof(Debug).GetField("s_ShowDialog", BindingFlags.Static | BindingFlags.NonPublic);

        protected internal static TestEnvironmentImpl Instance { get; protected set; }

        protected TestEnvironmentImpl() {
            TryOverrideShowDialog();
        }

        private static void TryOverrideShowDialog() {
            if (_showDialogField != null) {
                _showDialogField.SetValue(null, new Action<string, string, string, string>(ThrowAssertException));
            }
        }

        private static void ThrowAssertException(string stackTrace, string message, string detailMessage, string errorSource) {
            var exception = new Exception(message);
            if (_stackTraceStringField != null) {
                _stackTraceStringField.SetValue(exception, stackTrace);
            }
            throw exception;
        }

        public static TimeSpan Elapsed() => Instance?._stopwatch.Value?.Elapsed ?? new TimeSpan();
        public static void TestInitialize(string testFullName, int secondsTimeout = 10) => Instance?.BeforeTestRun(testFullName, secondsTimeout);
        public static void TestCleanup() => Instance?.AfterTestRun();
        public static void AddBeforeAfterTest(Func<Task<IDisposable>> beforeAfterTest) => Instance?.AddBeforeAfterTestAction(() => beforeAfterTest().GetAwaiter().GetResult());
        public static void AddBeforeAfterTest(Func<IDisposable> beforeAfterTest) => Instance?.AddBeforeAfterTestAction(beforeAfterTest);

        private readonly AsyncLocal<List<Func<IDisposable>>> _beforeAfterTestActions = new AsyncLocal<List<Func<IDisposable>>>();
        private readonly AsyncLocal<Stack<IDisposable>> _beforeAfterTestActionDisposables = new AsyncLocal<Stack<IDisposable>>();
        private readonly AsyncLocal<TaskObserver> _taskObserver = new AsyncLocal<TaskObserver>();
        private readonly AsyncLocal<Stopwatch> _stopwatch = new AsyncLocal<Stopwatch>();
        private readonly AssemblyLoader _assemblyLoader = new AssemblyLoader();

        public TestEnvironmentImpl AddAssemblyResolvePaths(params string[] paths) {
            _assemblyLoader.AddPaths(paths.Where(n => !string.IsNullOrEmpty(n)).ToArray());
            return this;
        }

        public bool TryAddTaskToWait(Task task) {
            var taskObserver = _taskObserver.Value;
            return taskObserver != null && taskObserver.TryAdd(task);
        }
        
        protected virtual void BeforeTestRun(string testFullName, int secondsTimeout) {
            if (_taskObserver.Value != null) {
                throw new InvalidOperationException("AsyncLocal<TaskObserver> reentrancy");
            }

            if (_stopwatch.Value != null) {
                throw new InvalidOperationException("AsyncLocal<Stopwatch> reentrancy");
            }

            var beforeAfterTestActions = _beforeAfterTestActions.Value;
            _beforeAfterTestActions.Value = null;

            _taskObserver.Value = new TaskObserver(secondsTimeout);
            _stopwatch.Value = new Stopwatch();
            _stopwatch.Value.Start();
            TestData.SetTestRunScope(testFullName);

            if (beforeAfterTestActions != null) {
                var disposables = new Stack<IDisposable>();
                try {
                    foreach (var beforeAfterTestAction in beforeAfterTestActions) {
                        disposables.Push(beforeAfterTestAction());
                    }
                } catch (Exception) {
                    RunDisposablesSafe(disposables);
                    throw;
                }

                _beforeAfterTestActionDisposables.Value = disposables;
            }
        }

        protected virtual void AfterTestRun() {
            try {
                var disposables = _beforeAfterTestActionDisposables.Value;
                _beforeAfterTestActionDisposables.Value = null;
                if (disposables != null) {
                    var afterTestRunException = RunDisposablesSafe(disposables);
                    if (afterTestRunException != null) {
                        throw afterTestRunException;
                    }
                }

                _taskObserver.Value?.WaitForObservedTask();
            } finally {
                _stopwatch.Value?.Stop();
                TestData.ClearTestRunScope();

                _stopwatch.Value = null;
                _taskObserver.Value = null;
            }
        }

        private void AddBeforeAfterTestAction(Func<IDisposable> beforeAfterTest) {
            var actions = _beforeAfterTestActions.Value ?? (_beforeAfterTestActions.Value = new List<Func<IDisposable>>());
            actions.Add(beforeAfterTest);
        }

        private AggregateException RunDisposablesSafe(Stack<IDisposable> disposables) {
            var exceptions = new List<Exception>();
            while (disposables.Count > 0) {
                var disposable = disposables.Pop();
                try {
                    disposable.Dispose();
                } catch (Exception ex) {
                    exceptions.Add(ex);
                }
            }

            return exceptions.Count > 0 ? new AggregateException(exceptions) : null;
        }
    }
}
