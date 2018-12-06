﻿// Python Tools for Visual Studio
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

namespace Microsoft.Python.Core.Logging {
    public interface ILogger {
        /// <summary>
        /// Sets minimal log level for output to appear in the host application.
        /// </summary>
        TraceEventType LogLevel { get; set; }

        void Log(TraceEventType eventType, IFormattable message);
        void Log(TraceEventType eventType, string message);
        void Log(TraceEventType eventType, params object[] p);
    }
}
