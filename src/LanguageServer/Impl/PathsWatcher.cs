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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.LanguageServer {
    internal sealed class PathsWatcher : IDisposable {
        private readonly DisposableBag _disposableBag = new DisposableBag(nameof(PathsWatcher));
        private readonly Action _onChanged;
        private readonly object _lock = new object();
        private readonly ILogger _log;

        private Timer _throttleTimer;
        private bool _changedSinceLastTick;

        public PathsWatcher(string[] paths, Action onChanged, ILogger log) {
            _log = log;
            paths = paths != null ? paths.Where(Path.IsPathRooted).ToArray() : Array.Empty<string>();
            if (paths.Length == 0) {
                return;
            }

            _onChanged = onChanged;

            var reduced = ReduceToCommonRoots(paths);

            foreach (var p in reduced) {
                try {
                    if (!Directory.Exists(p)) {
                        continue;
                    }
                } catch (IOException ex) {
                    _log.Log(TraceEventType.Warning, $"Unable to access directory {p}, exception {ex.Message}");
                    continue;
                }

                _log.Log(TraceEventType.Verbose, $"Watching {p}");

                try {
                    var fsw = new System.IO.FileSystemWatcher(p) {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                        InternalBufferSize = 1 << 16, // Max buffer size of 64 KB
                    };

                    fsw.Changed += OnChanged;
                    fsw.Created += OnChanged;
                    fsw.Deleted += OnChanged;
                    fsw.Renamed += OnChanged;

                    fsw.Filter = "*.p*"; // .py, .pyc, .pth - TODO: Use Filters in .NET Core 3.0.

                    _disposableBag
                        .Add(() => _throttleTimer?.Dispose())
                        .Add(() => fsw.Changed -= OnChanged)
                        .Add(() => fsw.Created -= OnChanged)
                        .Add(() => fsw.Deleted -= OnChanged)
                        .Add(() => fsw.Renamed -= OnChanged)
                        .Add(() => fsw.EnableRaisingEvents = false)
                        .Add(fsw);
                } catch (ArgumentException ex) {
                    _log.Log(TraceEventType.Warning, $"Unable to create file watcher for {p}, exception {ex.Message}");
                }
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            // Throttle calls so we don't get flooded with requests
            // if there is massive change to the file structure.
            lock (_lock) {
                _changedSinceLastTick = true;
                _throttleTimer = _throttleTimer ?? new Timer(TimerProc, null, 500, 500);
            }
        }

        private void TimerProc(object o) {
            lock (_lock) {
                if (!_changedSinceLastTick) {
                    ThreadPool.QueueUserWorkItem(_ => _onChanged());
                    _throttleTimer?.Dispose();
                    _throttleTimer = null;
                }
                _changedSinceLastTick = false;
            }
        }
        public void Dispose() => _disposableBag.TryDispose();

        private IEnumerable<string> ReduceToCommonRoots(string[] paths) {
            if (paths.Length == 0) {
                return paths;
            }

            var original = paths.OrderBy(s => s.Length).ToList();
            List<string> reduced = null;

            while (reduced == null || original.Count > reduced.Count) {
                var shortest = original[0];
                reduced = new List<string>();
                reduced.Add(shortest);
                for (var i = 1; i < original.Count; i++) {
                    // take all that do not start with the shortest
                    if (!original[i].StartsWith(shortest)) {
                        reduced.Add(original[i]);
                    }
                }
                original = reduced;
            }
            return reduced;
        }
    }
}
