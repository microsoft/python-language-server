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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Microsoft.Python.UnitTests.Core.FluentAssertions {
    public sealed class TaskAssertions : TaskAssertionsBase<Task, TaskAssertions> {
        public TaskAssertions(Task task) : base(task) { }
    }

    public sealed class TaskAssertions<TResult> : TaskAssertionsBase<Task<TResult>, TaskAssertions<TResult>> {
        public TaskAssertions(Task<TResult> task) : base(task) { }

        public Task<AndConstraint<TaskAssertions<TResult>>> HaveResultAsync(TResult result, int timeout = 10000, string because = "", params object[] reasonArgs)
            => BeInTimeAsync(HaveResultAsyncContinuation, result, timeout, because: because, reasonArgs: reasonArgs);

        private AndConstraint<TaskAssertions<TResult>> HaveResultAsyncContinuation(Task<Task> task, object state) {
            var data = (TimeoutContinuationState<TResult>)state;
            var and = AssertStatus(TaskStatus.RanToCompletion, true, data.Because, data.ReasonArgs,
                "Expected task to be completed in {0} milliseconds{reason}, but it is {1}.", data.Timeout, Subject.Status);

            Subject.Result.Should().Be(data.Argument);
            return and;
        }
    }

    public abstract class TaskAssertionsBase<TTask, TAssertions> : ReferenceTypeAssertions<TTask, TAssertions>
        where TTask : Task
        where TAssertions : TaskAssertionsBase<TTask, TAssertions> {

        protected TaskAssertionsBase(TTask task) {
            Subject = task;
        }

        protected override string Identifier { get; } = "System.Threading.Tasks.Task";

        public AndConstraint<TAssertions> BeCompleted(string because = "", params object[] reasonArgs) {
            Subject.Should().NotBeNull();

            Execute.Assertion.ForCondition(Subject.IsCompleted)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected task to be completed{{reason}}, but it is {Subject.Status}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> NotBeCompleted(string because = "", params object[] reasonArgs) {
            Subject.Should().NotBeNull();

            Execute.Assertion.ForCondition(!Subject.IsCompleted)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected task not to be completed{{reason}}, but {GetNotBeCompletedMessage()}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        private string GetNotBeCompletedMessage() {
            var exceptions = AsyncAssertions.GetExceptions(Subject);
            switch (Subject.Status) {
                case TaskStatus.RanToCompletion:
                    return "it has run to completion successfully";
                case TaskStatus.Canceled:
                    return $"it is canceled with exception of type {exceptions[0].GetType()}: {exceptions[0].Message}";
                case TaskStatus.Faulted:
                    return $@"it is faulted with the following exceptions: {string.Join(Environment.NewLine, exceptions.Select(e => $"    {e.GetType()}: {e.Message}"))}";
                default:
                    return string.Empty;
            }
        }

        public Task<AndConstraint<TAssertions>> BeCompletedAsync(int timeout = 30000, string because = "", params object[] reasonArgs)
            => BeInTimeAsync(BeCompletedAsyncContinuation, false, timeout, because: because, reasonArgs: reasonArgs);

        public Task<AndConstraint<TAssertions>> BeCanceledAsync(int timeout = 30000, string because = "", params object[] reasonArgs)
            => BeInTimeAsync(BeCanceledAsyncContinuation, false, timeout, because: because, reasonArgs: reasonArgs);

        public Task<AndConstraint<TAssertions>> NotBeCompletedAsync(int timeout = 5000, string because = "", params object[] reasonArgs)
            => BeInTimeAsync(NotBeCompletedAsyncContinuation, false, timeout, 5000, because, reasonArgs);

        protected Task<AndConstraint<TAssertions>> BeInTimeAsync<TArg>(Func<Task<Task>, object, AndConstraint<TAssertions>> continuation, TArg argument, int timeout = 10000, int debuggerTimeout = 100000, string because = "", params object[] reasonArgs) {
            Subject.Should().NotBeNull();
            if (Debugger.IsAttached) {
                timeout = Math.Max(debuggerTimeout, timeout);
            }

            var timeoutTask = Task.Delay(timeout);
            var state = new TimeoutContinuationState<TArg>(argument, timeout, because, reasonArgs);
            return Task.WhenAny(timeoutTask, Subject)
                .ContinueWith(continuation, state, default(CancellationToken), TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private AndConstraint<TAssertions> BeCompletedAsyncContinuation(Task<Task> task, object state) {
            var data = (TimeoutContinuationState<bool>)state;
            return AssertStatus(TaskStatus.RanToCompletion, true, data.Because, data.ReasonArgs,
                "Expected task to be completed in {0} milliseconds{reason}, but it is {1}.", data.Timeout, Subject.Status);
        }

        private AndConstraint<TAssertions> BeCanceledAsyncContinuation(Task<Task> task, object state) {
            var data = (TimeoutContinuationState<bool>)state;
            return AssertStatus(TaskStatus.Canceled, true, data.Because, data.ReasonArgs,
                "Expected task to be canceled in {0} milliseconds{reason}, but it is {1}.", data.Timeout, Subject.Status);
        }

        private AndConstraint<TAssertions> NotBeCompletedAsyncContinuation(Task<Task> task, object state) {
            var data = (TimeoutContinuationState<bool>)state;
            Execute.Assertion.ForCondition(!Subject.IsCompleted)
                .BecauseOf(data.Because, data.ReasonArgs)
                .FailWith($"Expected task not to be completed in {data.Timeout} milliseconds{{reason}}, but {GetNotBeCompletedMessage()}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> BeRanToCompletion(string because = "", params object[] reasonArgs)
            => AssertStatus(TaskStatus.RanToCompletion, true, because, reasonArgs, "Expected task to completed execution successfully{reason}, but it has status {0}.", Subject.Status);

        public AndConstraint<TAssertions> BeCanceled(string because = "", params object[] reasonArgs)
            => AssertStatus(TaskStatus.Canceled, true, because, reasonArgs, "Expected task to be canceled{reason}, but it has status {0}.", Subject.Status);

        public AndConstraint<TAssertions> NotBeCanceled(string because = "", params object[] reasonArgs)
            => AssertStatus(TaskStatus.Canceled, false, because, reasonArgs, "Expected task not to be canceled{reason}, but it has status {0}.", Subject.Status);

        public AndConstraint<TAssertions> BeFaulted(string because = "", params object[] reasonArgs)
            => AssertStatus(TaskStatus.Faulted, true, because, reasonArgs, "Expected task to be faulted{reason}, but it has status {0}.", Subject.Status);

        public AndConstraint<TAssertions> BeFaulted<TException>(string because = "", params object[] reasonArgs) where TException : Exception
            => AssertStatus(TaskStatus.Faulted, false, because, reasonArgs, "Expected task to be faulted with exception of type {0}{reason}, but it has status {1}.", typeof(TException), Subject.Status);

        public AndConstraint<TAssertions> NotBeFaulted(string because = "", params object[] reasonArgs)
            => AssertStatus(TaskStatus.Faulted, false, because, reasonArgs, "Expected task not to be faulted{reason}, but it has status {0}.", Subject.Status);

        protected AndConstraint<TAssertions> AssertStatus(TaskStatus status, bool hasStatus, string because, object[] reasonArgs, string message, params object[] messageArgs) {
            Subject.Should().NotBeNull();

            Execute.Assertion.ForCondition(status == Subject.Status == hasStatus)
                .BecauseOf(because, reasonArgs)
                .FailWith(message, messageArgs);

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        protected class TimeoutContinuationState<TArg> {
            public TimeoutContinuationState(TArg argument, int timeout, string because, object[] reasonArgs) {
                Argument = argument;
                Because = because;
                ReasonArgs = reasonArgs;
                Timeout = timeout;
            }
            public TArg Argument { get; }
            public int Timeout { get; }
            public string Because { get; }
            public object[] ReasonArgs { get; }
        }
    }
}
