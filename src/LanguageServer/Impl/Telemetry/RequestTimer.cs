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
using System.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.LanguageServer.Telemetry {
    internal class RequestTimer {
        private const int MaxEvents = 10;

        private readonly ITelemetryService _telemetryService;

        private readonly Dictionary<string, int> _events = new Dictionary<string, int>();
        private readonly object _lock = new object();

        public RequestTimer(ITelemetryService telemetryService) {
            _telemetryService = telemetryService;
        }

        public Timer Time(string method) {
            lock (_lock) {
                _events.TryGetValue(method, out var eventNum);

                if (eventNum >= MaxEvents) {
                    return Timer.Disabled;
                }

                _events[method] = eventNum + 1;

                return new Timer(method, _telemetryService);
            }
        }

        public class Timer : IDisposable {
            private readonly bool _disabled;
            private readonly string _method;
            private readonly ITelemetryService _telemetryService;
            private readonly Stopwatch _stopwatch;

            private Dictionary<string, string> _extraProperties;
            private Dictionary<string, double> _extraMeasures;

            public static Timer Disabled = new Timer();

            private Timer() {
                _disabled = true;
            }

            public Timer(string method, ITelemetryService telemetryService) {
                _method = method;
                _telemetryService = telemetryService;
                _stopwatch = Stopwatch.StartNew();
            }

            public void AddProperty(string name, string property) {
                if (!_disabled) {
                    _extraProperties = _extraProperties ?? new Dictionary<string, string>();
                    _extraProperties[name] = property;
                }
            }

            public void AddMeasure(string name, double measure) {
                if (!_disabled) {
                    _extraMeasures = _extraMeasures ?? new Dictionary<string, double>();
                    _extraMeasures[name] = measure;
                }
            }

            public void Dispose() {
                if (_disabled) {
                    return;
                }

                _stopwatch.Stop();

                var e = Telemetry.CreateEvent("rpc.request");
                e.Properties["method"] = _method;
                e.Measurements["elapsedMs"] = _stopwatch.Elapsed.TotalMilliseconds;

                if (_extraProperties != null) {
                    foreach (var (key, value) in _extraProperties) {
                        e.Properties[key] = value;
                    }
                }

                if (_extraMeasures != null) {
                    foreach (var (key, value) in _extraMeasures) {
                        e.Measurements[key] = value;
                    }
                }

                _telemetryService.SendTelemetryAsync(e).DoNotWait();
            }
        }
    }
}
