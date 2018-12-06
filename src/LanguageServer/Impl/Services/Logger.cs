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
// permissions and limitations under the License

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Services {
    internal sealed class Logger: ILogger {
        private readonly JsonRpc _rpc;

        public Logger(JsonRpc rpc) {
            _rpc = rpc;
        }

        public TraceEventType LogLevel { get; set; } = TraceEventType.Error;
        public void Log(TraceEventType eventType, string message)
            => LogMessageAsync(message, eventType).DoNotWait();
        public void Log(TraceEventType eventType, IFormattable message)
            => Log(eventType, message.ToString());

        public void Log(TraceEventType eventType, params object[] parameters) {
            var sb = new StringBuilder();
            for(var  i = 0; i < parameters.Length; i++) {
                sb.Append('{');
                sb.Append(i.ToString());
                sb.Append("} ");
                Log(eventType, sb.ToString().FormatUI(parameters));
            }
        }

        public Task LogMessageAsync(string message, TraceEventType eventType) {
            if (eventType > LogLevel && eventType != TraceEventType.Information) {
                return Task.CompletedTask;
            }
            var parameters = new LogMessageParams {
                type = eventType.ToMessageType(),
                message = message
            };
            return _rpc.NotifyWithParameterObjectAsync("window/logMessage", parameters);
        }

        [Serializable]
        private class LogMessageParams {
            public MessageType type;
            public string message;
        }
    }
}
