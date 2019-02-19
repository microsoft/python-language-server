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
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions.Execution;
using FluentAssertions.Specialized;

namespace Microsoft.Python.UnitTests.Core.FluentAssertions {
    internal sealed class AsyncAssertions {
        private readonly Func<Task> _asyncAction;

        public AsyncAssertions(Func<Task> asyncAction) {
            _asyncAction = asyncAction;
        }

        public async Task ShouldNotThrowAsync(string because, object[] becauseArgs) {
            var exceptions = await InvokeAction();

            Execute.Assertion
                .ForCondition(exceptions.Count == 0)
                .BecauseOf(because, becauseArgs)
                .FailWith("Did not expect any exception{reason}, but found:{0}.", exceptions.Select(e => $"\n\t{e.GetType()} with message {e.Message}"));
        }

        public async Task<ExceptionAssertions<TException>> ShouldThrowAsync<TException>(string because, object[] becauseArgs)
            where TException : Exception {
            var exceptions = await InvokeAction();

            Execute.Assertion
                .ForCondition(exceptions.Any())
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected {0}{reason}, but no exception was thrown.", typeof(TException));

            var typedExceptions = exceptions.OfType<TException>().ToList();
            Execute.Assertion
                .ForCondition(typedExceptions.Any())
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected {0}{reason}, but found {1}.", typeof(TException), exceptions);

            return new ExceptionAssertions<TException>(typedExceptions);
        }

        private Task<List<Exception>> InvokeAction() => _asyncAction().ContinueWith(GetExceptions);

        public static List<Exception> GetExceptions(Task task) {
            switch (task.Status) {
                case TaskStatus.Canceled:
                    return GetCanceledException(task);
                case TaskStatus.Faulted:
                    return new List<Exception>(task.Exception.Flatten().InnerExceptions);
                default:
                    return new List<Exception>();
            }
        }

        private static List<Exception> GetCanceledException(Task task) {
            var exceptions = new List<Exception>();
            try {
                task.GetAwaiter().GetResult();
            } catch (Exception ex) {
                exceptions.Add(ex);
            }
            return exceptions;
        }
    }
}
