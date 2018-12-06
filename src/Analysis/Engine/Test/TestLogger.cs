﻿// Copyright(c) Microsoft Corporation
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
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using TestUtilities;

namespace AnalysisTests {
    public sealed class TestLogger : ILogger {
        public TraceEventType LogLevel { get; set; } = TraceEventType.Verbose;
        public void Log(TraceEventType eventType, IFormattable message) => Log(eventType, message.ToString());
        public void Log(TraceEventType eventType, string message) {
            var m = $"[{TestEnvironmentImpl.Elapsed()}]: {message}";
            switch (eventType) {
                case TraceEventType.Error:
                case TraceEventType.Critical:
                    Trace.TraceError(m);
                    break;
                case TraceEventType.Warning:
                    Trace.TraceWarning(m);
                    break;
                case TraceEventType.Information:
                    Trace.TraceInformation(m);
                    break;
                case TraceEventType.Verbose:
                    Trace.TraceInformation($"LOG: {m}");
                    break;
            }
        }

        public void Log(TraceEventType eventType, params object[] parameters) {
            var sb = new StringBuilder();
            for (var i = 0; i < parameters.Length; i++) {
                sb.Append('{');
                sb.Append(i.ToString());
                sb.Append("} ");
                Log(eventType, sb.ToString().FormatUI(parameters));
            }
        }
    }
}
