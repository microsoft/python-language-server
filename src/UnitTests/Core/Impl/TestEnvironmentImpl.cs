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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TestUtilities {
    public class TestEnvironmentImpl {
        protected internal static TestEnvironmentImpl Instance { get; protected set; }

        public static TimeSpan Elapsed() => Instance?._stopwatch.Value?.Elapsed ?? new TimeSpan();
        public static void TestInitialize(string testFullName, int secondsTimeout = 10) => Instance?.BeforeTestRun(testFullName, secondsTimeout);
        public static void TestCleanup() => Instance?.AfterTestRun();
        public static void AddBeforeAfterTest(Func<Task<IDisposable>> beforeAfterTest) => Instance?.AddBeforeAfterTestAction(() => beforeAfterTest().GetAwaiter().GetResult());
        public static void AddBeforeAfterTest(Func<IDisposable> beforeAfterTest) => Instance?.AddBeforeAfterTestAction(beforeAfterTest);

        private readonly AsyncLocal<List<Func<IDisposable>>> _beforeAfterTestActions = new AsyncLocal<List<Func<IDisposable>>>();
        private readonly AsyncLocal<TaskObserver> _taskObserver = new AsyncLocal<TaskObserver>();
        private readonly AsyncLocal<Stopwatch> _stopwatch = new AsyncLocal<Stopwatch>();
        private readonly AssemblyLoader _assemblyLoader = new AssemblyLoader();

        public TestEnvironmentImpl AddAssemblyResolvePaths(params string[] paths) {
            _assemblyLoader.AddPaths(paths.Where(n => !string.IsNullOrEmpty(n)).ToArray());
            return this;
        }

        public bool TryAddTaskToWait(Task task) {
            var taskObserver = _taskObserver.Value;
            if (taskObserver == null) {
                return false;
            }
            taskObserver.Add(task);
            return true;
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

            var disposables = new Stack<IDisposable>();
            try {
                foreach (var beforeAfterTestAction in beforeAfterTestActions) {
                    disposables.Push(beforeAfterTestAction());
                }
            } catch (Exception) {
                RunDisposables(disposables);
                throw;
            }
        }

        protected virtual void AfterTestRun() {
            try {
                _taskObserver.Value?.WaitForObservedTask();
                _stopwatch.Value?.Stop();
                TestData.ClearTestRunScope();
            } finally {
                _stopwatch.Value = null;
                _taskObserver.Value = null;
            }
        }

        private void AddBeforeAfterTestAction(Func<IDisposable> beforeAfterTest) {
            var actions = _beforeAfterTestActions.Value ?? (_beforeAfterTestActions.Value = new List<Func<IDisposable>>());
            actions.Add(beforeAfterTest);
        }

        private void RunDisposables(Stack<IDisposable> disposables) {
            while (disposables.Count > 0) {
                var disposable = disposables.Pop();
                try {
                    disposable.Dispose();
                } catch (Exception) {
                }
            }
        }
    }
}