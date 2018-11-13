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
using System.Diagnostics;
using System.Reflection;
using Microsoft.PythonTools.Analysis.Infrastructure;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Implementation {
    internal class TelemetryRpcTraceListener : TraceListener {
        private readonly ITelemetryService2 _telemetryService;
        private readonly string _version;

        public TelemetryRpcTraceListener(ITelemetryService2 telemetryService) {
            _telemetryService = telemetryService;

            // This file is a part of the LanguageServer assembly, so this is the correct version.
            _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

#if DEBUG
            _version += "-debug";  // Add a suffix so we can more easily ignore non-release versions.
#endif
        }

        // See JsonRpc.CreateError.
        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data) {
            if (eventType != TraceEventType.Error || id != (int)JsonRpc.TraceEvents.LocalInvocationError) {
                return;
            }

            Debug.Assert(data.Length >= 2);
            if (data.Length < 2) {
                return;
            }

            var exception = data[0] as Exception;
            Debug.Assert(exception != null);
            if (exception == null) {
                return;
            }

            // See JsonRpc.StripExceptionToInnerException.
            if (exception is TargetInvocationException || (exception is AggregateException && exception.InnerException != null)) {
                exception = exception.InnerException;
            }

            // Exceptions we expect to throw and pass back over RPC, and therefore should not record.
            switch (exception) {
                case EditorOperationException _:
                case NotImplementedException _:
                case LanguageServerException _:
                    return;
            }

            var method = data[1] as string;
            Debug.Assert(method != null);
            if (method == null) {
                return;
            }

            var e = new TelemetryEvent {
                EventName = "python_language_server/rpc.exception", // TODO: Create EventName with a standardized format elsewhere.
            };
            e.Properties["method"] = method;
            e.Properties["name"] = exception.GetType().Name;
            e.Properties["stackTrace"] = exception.StackTrace;

            // TODO: Move this into a shared function, similarly to EventName.
            e.Properties["version"] = _version;

            _telemetryService.SendTelemetry(e).DoNotWait();
        }

        public override void Write(string message) { }

        public override void WriteLine(string message) { }

        // The only thing that this listener should do is look for RPC
        // incovation error events to then send. The base TraceListener
        // implements the its methods by building strings from given
        // arguments, then passing them to the abstract Write and
        // WriteLine (implemented as noops above). To prevent that extra
        // work, the below methods override the base class to do nothing.
        #region TraceListener noop overrides
        public override void Fail(string message) { }

        public override void Fail(string message, string detailMessage) { }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data) { }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id) { }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message) { }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args) { }

        public override void Write(object o) { }

        public override void Write(object o, string category) { }

        public override void Write(string message, string category) { }

        public override void WriteLine(object o) { }

        public override void WriteLine(object o, string category) { }

        public override void WriteLine(string message, string category) { }
        #endregion
    }
}
