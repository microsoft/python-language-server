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

        public PythonLibraryPath(string path, LibraryType libraryType, string modulePrefix) {
            Path = path;
            LibraryType = libraryType;
            _modulePrefix = modulePrefix;
        }

        public string Path { get; }
        public LibraryType LibraryType { get; }

        public string ModulePrefix => _modulePrefix ?? string.Empty;

        public override string ToString() 
            => "{0}|{1}|{2}".FormatInvariant(Path, LibraryType == LibraryType.Standard ? "stdlib" : "", _modulePrefix ?? string.Empty);

        public static PythonLibraryPath FromLibraryPath(string s, IFileSystem fs, string standardLibraryPath) {
            if (string.IsNullOrEmpty(s)) {
                throw new ArgumentNullException(nameof(s));
            }
            
            var m = ParseRegex.Match(s);
            if (!m.Success || !m.Groups["path"].Success) {
                throw new FormatException();
            }

            var libraryType = LibraryType.Other;
            var sitePackagesPath = GetSitePackagesPath(standardLibraryPath);
            var path = m.Groups["path"].Value;
            if (m.Groups["stdlib"].Success) {
                libraryType = LibraryType.Standard;
            } else if(fs.IsPathUnderRoot(sitePackagesPath, path)) {
                libraryType = LibraryType.SitePackages;
            }

            return new PythonLibraryPath(
                m.Groups["path"].Value,
                libraryType,
                m.Groups["prefix"].Success ? m.Groups["prefix"].Value : null
            );
        }

        /// <summary>
        /// Gets the default set of search paths based on the path to the root
        /// of the standard library.
        /// </summary>
        /// <param name="standardLibraryPath">Root of the standard library.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        public static List<PythonLibraryPath> GetDefaultSearchPaths(string standardLibraryPath) {
            var result = new List<PythonLibraryPath>();
            if (!Directory.Exists(standardLibraryPath)) {
                return result;
            }

            result.Add(new PythonLibraryPath(standardLibraryPath, LibraryType.Standard, null));

            var sitePackages = GetSitePackagesPath(standardLibraryPath);
            if (!Directory.Exists(sitePackages)) {
                return result;
            }

            result.Add(new PythonLibraryPath(sitePackages, LibraryType.SitePackages, null));
            result.AddRange(ModulePath.ExpandPathFiles(sitePackages)
                .Select(p => new PythonLibraryPath(p, LibraryType.SitePackages, null))
            );

            return result;
        }

        /// <summary>
        /// Gets the set of search paths for the specified factory.
        /// </summary>
        public static async Task<IList<PythonLibraryPath>> GetSearchPathsAsync(InterpreterConfiguration config, IFileSystem fs, IProcessServices ps, CancellationToken cancellationToken = default) {
            for (int retries = 5; retries > 0; --retries) {
                try {
                    return await GetSearchPathsFromInterpreterAsync(config, fs, ps, cancellationToken);
                } catch (InvalidOperationException) {
                    // Failed to get paths
                    break;
                } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                    // Failed to get paths due to IO exception - sleep and then loop
                    Thread.Sleep(50);
                }
            }

            var standardLibraryPath = GetStandardLibraryPath(config);
            if (!string.IsNullOrEmpty(standardLibraryPath)) {
                return GetDefaultSearchPaths(standardLibraryPath);
            }

            return Array.Empty<PythonLibraryPath>();
        }

        public static string GetStandardLibraryPath(InterpreterConfiguration config) {
            var ospy = PathUtils.FindFile(config.LibraryPath, "os.py");
            return !string.IsNullOrEmpty(ospy) ? IOPath.GetDirectoryName(ospy) : string.Empty;
        }

        public static string GetSitePackagesPath(InterpreterConfiguration config)
            => GetSitePackagesPath(GetStandardLibraryPath(config));

        public static string GetSitePackagesPath(string standardLibraryPath) 
            => !string.IsNullOrEmpty(standardLibraryPath) ? IOPath.Combine(standardLibraryPath, "site-packages") : string.Empty;

        /// <summary>
        /// Gets the set of search paths by running the interpreter.
        /// </summary>
        /// <param name="config">Interpreter configuration.</param>
        /// <param name="fs">File system services.</param>
        /// <param name="ps">Process services.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        public static async Task<List<PythonLibraryPath>> GetSearchPathsFromInterpreterAsync(InterpreterConfiguration config, IFileSystem fs, IProcessServices ps, CancellationToken cancellationToken = default) {
            // sys.path will include the working directory, so we make an empty
            // path that we can filter out later
            var tempWorkingDir = IOPath.Combine(IOPath.GetTempPath(), IOPath.GetRandomFileName());
            fs.CreateDirectory(tempWorkingDir);
            if (!InstallPath.TryGetFile("get_search_paths.py", out var srcGetSearchPaths)) {
                return new List<PythonLibraryPath>();
            }
            var getSearchPaths = IOPath.Combine(tempWorkingDir, PathUtils.GetFileName(srcGetSearchPaths));
            File.Copy(srcGetSearchPaths, getSearchPaths);

            var startInfo = new ProcessStartInfo(
                config.InterpreterPath,
                new[] { "-S", "-E", getSearchPaths }.AsQuotedArguments()
            ) {
                WorkingDirectory = tempWorkingDir,
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            try {
                var output = await ps.ExecuteAndCaptureOutputAsync(startInfo, cancellationToken);
                var standardLibraryPath = GetSitePackagesPath(config);
                return output.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Select(s => {
                    if (s.PathStartsWith(tempWorkingDir)) {
                        return null;
                    }
                    try {
                        return FromLibraryPath(s, fs, standardLibraryPath);
                    } catch (ArgumentException) {
                        Debug.Fail("Invalid search path: " + (s ?? "<null>"));
                        return null;
                    } catch (FormatException) {
                        Debug.Fail("Invalid format for search path: " + s);
                        return null;
                    }
                }).Where(p => p != null).ToList();
            } finally {
                fs.DeleteDirectory(tempWorkingDir, true);
            }
        }
    }
}
