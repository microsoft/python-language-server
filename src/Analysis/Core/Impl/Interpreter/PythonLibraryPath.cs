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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using IOPath = System.IO.Path;

namespace Microsoft.Python.Analysis.Core.Interpreter {
    public sealed class PythonLibraryPath {
        private readonly string _modulePrefix;

        private static readonly Regex ParseRegex = new Regex(
            @"(?<path>[^|]+)\|(?<stdlib>stdlib)?\|(?<prefix>[^|]+)?"
        );

        public PythonLibraryPath(string path, bool isStandardLibrary, string modulePrefix) {
            Path = path;
            IsStandardLibrary = isStandardLibrary;
            _modulePrefix = modulePrefix;
        }

        public string Path { get; }

        public bool IsStandardLibrary { get; }

        public string ModulePrefix => _modulePrefix ?? string.Empty;

        public override string ToString() 
            => "{0}|{1}|{2}".FormatInvariant(Path, IsStandardLibrary ? "stdlib" : "", _modulePrefix ?? "");

        public static PythonLibraryPath Parse(string s) {
            if (string.IsNullOrEmpty(s)) {
                throw new ArgumentNullException("source");
            }
            
            var m = ParseRegex.Match(s);
            if (!m.Success || !m.Groups["path"].Success) {
                throw new FormatException();
            }
            
            return new PythonLibraryPath(
                m.Groups["path"].Value,
                m.Groups["stdlib"].Success,
                m.Groups["prefix"].Success ? m.Groups["prefix"].Value : null
            );
        }

        /// <summary>
        /// Gets the default set of search paths based on the path to the root
        /// of the standard library.
        /// </summary>
        /// <param name="library">Root of the standard library.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        /// <remarks>New in 2.2, moved in 3.3</remarks>
        public static List<PythonLibraryPath> GetDefaultSearchPaths(string library) {
            var result = new List<PythonLibraryPath>();
            if (!Directory.Exists(library)) {
                return result;
            }

            result.Add(new PythonLibraryPath(library, true, null));

            var sitePackages = IOPath.Combine(library, "site-packages");
            if (!Directory.Exists(sitePackages)) {
                return result;
            }

            result.Add(new PythonLibraryPath(sitePackages, false, null));
            result.AddRange(ModulePath.ExpandPathFiles(sitePackages)
                .Select(p => new PythonLibraryPath(p, false, null))
            );

            return result;
        }

        /// <summary>
        /// Gets the set of search paths for the specified factory.
        /// </summary>
        public static async Task<IList<PythonLibraryPath>> GetSearchPathsAsync(InterpreterConfiguration config) {
            for (int retries = 5; retries > 0; --retries) {
                try {
                    return await GetSearchPathsFromInterpreterAsync(config.InterpreterPath);
                } catch (InvalidOperationException) {
                    // Failed to get paths
                    break;
                } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                    // Failed to get paths due to IO exception - sleep and then loop
                    Thread.Sleep(50);
                }
            }

            var ospy = PathUtils.FindFile(config.LibraryPath, "os.py");
            if (!string.IsNullOrEmpty(ospy)) {
                return GetDefaultSearchPaths(IOPath.GetDirectoryName(ospy));
            }

            return Array.Empty<PythonLibraryPath>();
        }

        /// <summary>
        /// Gets the set of search paths by running the interpreter.
        /// </summary>
        /// <param name="interpreter">Path to the interpreter.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        /// <remarks>Added in 2.2, moved in 3.3</remarks>
        public static async Task<List<PythonLibraryPath>> GetSearchPathsFromInterpreterAsync(string interpreter) {
            // sys.path will include the working directory, so we make an empty
            // path that we can filter out later
            var tempWorkingDir = IOPath.Combine(IOPath.GetTempPath(), IOPath.GetRandomFileName());
            Directory.CreateDirectory(tempWorkingDir);
            if (!InstallPath.TryGetFile("get_search_paths.py", out var srcGetSearchPaths)) {
                return new List<PythonLibraryPath>();
            }
            var getSearchPaths = IOPath.Combine(tempWorkingDir, PathUtils.GetFileName(srcGetSearchPaths));
            File.Copy(srcGetSearchPaths, getSearchPaths);

            var lines = new List<string>();
            var errorLines = new List<string> { "Cannot obtain list of paths" };

            try {
                using (var proc = new ProcessHelper(interpreter, new[] { "-S", "-E", getSearchPaths }, tempWorkingDir)) {
                    proc.OnOutputLine = lines.Add;
                    proc.OnErrorLine = errorLines.Add;

                    proc.Start();
                    using (var cts = new CancellationTokenSource(30000)) {
                        int exitCode;
                        try {
                            exitCode = await proc.WaitAsync(cts.Token);
                        } catch (OperationCanceledException) {
                            proc.Kill();
                            exitCode = -1;
                        }
                        if (exitCode != 0) {
                            throw new InvalidOperationException(string.Join(Environment.NewLine, errorLines));
                        }
                    }
                }
            } finally {
                PathUtils.DeleteDirectory(tempWorkingDir);
            }

            return lines.Select(s => {
                if (s.StartsWithOrdinal(tempWorkingDir, ignoreCase: true)) {
                    return null;
                }
                try {
                    return Parse(s);
                } catch (ArgumentException) {
                    Debug.Fail("Invalid search path: " + (s ?? "<null>"));
                    return null;
                } catch (FormatException) {
                    Debug.Fail("Invalid format for search path: " + s);
                    return null;
                }
            }).Where(p => p != null).ToList();
        }
    }
}
