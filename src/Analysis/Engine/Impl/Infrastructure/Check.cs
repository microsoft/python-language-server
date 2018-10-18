// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.FormattableString;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    public static class Check {
        [DebuggerStepThrough]
        public static void FieldType<T>(string fieldName, object fieldValue) {
            if (!(fieldValue is T)) {
                throw new InvalidOperationException($"Field {fieldName} must be of type {fieldValue}");
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentOfType<T>(string argumentName, object argument, [CallerMemberName] string callerName = null) {
            ArgumentNotNull(argumentName, argument);

            if (!(argument is T)) {
                throw new ArgumentException($"Argument {argumentName} of method {callerName} must be of type {typeof(T)}");
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentNotNull(string argumentName, object argument) {
            if (argument is null) {
                throw new ArgumentNullException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentNotNullOrEmpty(string argumentName, string argument) {
            ArgumentNotNull(argumentName, argument);

            if (string.IsNullOrEmpty(argument)) {
                throw new ArgumentException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void ArgumentOutOfRange(string argumentName, Func<bool> predicate) {
            if (predicate()) {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

        [DebuggerStepThrough]
        public static void InvalidOperation(Func<bool> predicate, string message = null) {
            if (!predicate()) {
                throw new InvalidOperationException(message ?? string.Empty);
            }
        }

        [DebuggerStepThrough]
        public static void Argument(string argumentName, Func<bool> predicate) {
            if (!predicate()) {
                throw new ArgumentException(Invariant($"{argumentName} is not valid"));
            }
        }
    }
}