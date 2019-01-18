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

// #define WAIT_FOR_DEBUGGER

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Threading;
using Microsoft.Python.LanguageServer.Services;
using Newtonsoft.Json;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Microsoft.Python.LanguageServer.Server {
    internal static class Program {
        public static void Main(string[] args) {
            CheckDebugMode();
            using (CoreShell.Create()) {
                var services = CoreShell.Current.ServiceManager;

                var messageFormatter = new JsonMessageFormatter();
                // StreamJsonRpc v1.4 serializer defaults
                messageFormatter.JsonSerializer.NullValueHandling = NullValueHandling.Ignore;
                messageFormatter.JsonSerializer.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
                messageFormatter.JsonSerializer.Converters.Add(new UriConverter());

                using (var cin = Console.OpenStandardInput())
                using (var cout = Console.OpenStandardOutput())
                using (var server = new Implementation.LanguageServer())
                using (var rpc = new LanguageServerJsonRpc(cout, cin, messageFormatter, server)) {
                    rpc.TraceSource.Switch.Level = SourceLevels.Error;
                    rpc.SynchronizationContext = new SingleThreadSynchronizationContext();

                    var osp = new OSPlatform();
                    services
                        .AddService(rpc)
                        .AddService(new Logger(rpc))
                        .AddService(new UIService(rpc))
                        .AddService(new ProgressService(rpc))
                        .AddService(new TelemetryService(rpc))
                        .AddService(new IdleTimeService())
                        .AddService(osp)
                        .AddService(new FileSystem(osp));

                    services.AddService(messageFormatter.JsonSerializer);

                    var token = server.Start(services, rpc);
                    rpc.StartListening();

                    // Wait for the "exit" request, it will terminate the process.
                    token.WaitHandle.WaitOne();
                }
            }
        }

        private static void CheckDebugMode() {
#if WAIT_FOR_DEBUGGER
            var start = DateTime.Now;
            while (!System.Diagnostics.Debugger.IsAttached) {
                System.Threading.Thread.Sleep(1000);
                if ((DateTime.Now - start).TotalMilliseconds > 15000) {
                    break;
                }
            }
#endif
        }

        private class LanguageServerJsonRpc : JsonRpc {
            public LanguageServerJsonRpc(Stream sendingStream, Stream receivingStream, IJsonRpcMessageFormatter formatter, object target)
                : base(new HeaderDelimitedMessageHandler(sendingStream, receivingStream, formatter), target) { }

            protected override JsonRpcError.ErrorDetail CreateErrorDetails(JsonRpcRequest request, Exception exception) {
                var localRpcEx = exception as LocalRpcException;

                return new JsonRpcError.ErrorDetail {
                    Code = (JsonRpcErrorCode?)localRpcEx?.ErrorCode ?? JsonRpcErrorCode.InvocationError,
                    Message = exception.Message,
                    Data = exception.StackTrace,
                };
            }
        }
    }

    sealed class UriConverter : JsonConverter {
        public override bool CanConvert(Type objectType) => objectType == typeof(Uri);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.String) {
                var str = (string)reader.Value;
                return new Uri(str.Replace("%3A", ":"));
            }

            if (reader.TokenType == JsonToken.Null) {
                return null;
            }

            throw new InvalidOperationException($"UriConverter: unsupported token type {reader.TokenType}");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (null == value) {
                writer.WriteNull();
                return;
            }

            if (value is Uri) {
                var uri = (Uri)value;
                var scheme = uri.Scheme;
                var str = uri.ToString();
                str = uri.Scheme + "://" + str.Substring(scheme.Length + 3).Replace(":", "%3A").Replace('\\', '/');
                writer.WriteValue(str);
                return;
            }

            throw new InvalidOperationException($"UriConverter: unsupported value type {value.GetType()}");
        }
    }
}
