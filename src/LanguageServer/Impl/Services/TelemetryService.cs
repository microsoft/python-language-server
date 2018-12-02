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

using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Python.Core.Shell;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Services {
#pragma warning disable CS0612 // Type or member is obsolete
    public sealed class TelemetryService : ITelemetryService {
#pragma warning restore CS0612 // Type or member is obsolete
        private readonly JsonRpc _rpc;
        private readonly string _plsVersion;

        public TelemetryService(JsonRpc rpc) {
            _rpc = rpc;

            _plsVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
#if DEBUG
            _plsVersion += "-debug";  // Add a suffix so we can more easily ignore non-release versions.
#endif
        }

        public Task SendTelemetryAsync(TelemetryEvent telemetryEvent) {
            telemetryEvent.Properties["plsVersion"] = _plsVersion;
            return _rpc.NotifyWithParameterObjectAsync("telemetry/event", telemetryEvent);
        }
    }
}
