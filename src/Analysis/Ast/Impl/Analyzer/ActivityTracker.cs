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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Python.Analysis.Analyzer {
    internal static class ActivityTracker {
        private static readonly Dictionary<string, AnalysisState> _modules = new Dictionary<string, AnalysisState>();
        private static readonly object _lock = new object();
        private static bool _tracking;
        private static Stopwatch _sw;

        private struct AnalysisState {
            public int Count;
            public bool IsComplete;
        }

        public static void OnEnqueueModule(string path) {
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            lock (_lock) {
                if (!_modules.TryGetValue(path, out var st)) {
                    _modules[path] = default;
                } else {
                    st.IsComplete = false;
                }
            }
        }

        public static void OnModuleAnalysisComplete(string path) {
            lock (_lock) {
                if (_modules.TryGetValue(path, out var st)) {
                    st.Count++;
                    st.IsComplete = true;
                }
            }
        }

        public static bool IsAnalysisComplete {
            get {
                lock (_lock) {
                    return _modules.All(m => m.Value.IsComplete);
                }
            }
        }


        public static void StartTracking() {
            lock (_lock) {
                if (!_tracking) {
                    _tracking = true;
                    _modules.Clear();
                    _sw = Stopwatch.StartNew();
                }
            }
        }

        public static void EndTracking() {
            lock (_lock) {
                if (_tracking) {
                    _sw?.Stop();
                    _tracking = false;
                }
            }
        }

        public static int ModuleCount {
            get {
                lock (_lock) {
                    return _modules.Count;
                }
            }
        }
        public static double MillisecondsElapsed => _sw?.Elapsed.TotalMilliseconds ?? 0;
    }
}
