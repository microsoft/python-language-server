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
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using IOPath = System.IO.Path;

namespace Microsoft.Python.Analysis.Core.Interpreter {
    public enum PythonLibraryPathType {
        Unspecified,
        StdLib,
        Site,
        Pth,
    }

    public sealed class PythonLibraryPath : IEquatable<PythonLibraryPath> {
        public PythonLibraryPath(string path, PythonLibraryPathType type = PythonLibraryPathType.Unspecified, string modulePrefix = null) {
            Path = PathUtils.NormalizePathAndTrim(path);
            Type = type;
            ModulePrefix = modulePrefix ?? string.Empty;
        }

        public string Path { get; }
        public PythonLibraryPathType Type { get; }
        public string ModulePrefix { get; }

        public override string ToString() {
            var type = string.Empty;

            switch (Type) {
                case PythonLibraryPathType.StdLib:
                    type = "stdlib";
                    break;
                case PythonLibraryPathType.Site:
                    type = "site";
                    break;
                case PythonLibraryPathType.Pth:
                    type = "pth";
                    break;
            }

            return "{0}|{1}|{2}".FormatInvariant(Path, type, ModulePrefix);
        }

        private static PythonLibraryPath Parse(string s) {
            if (string.IsNullOrEmpty(s)) {
                throw new ArgumentNullException(nameof(s));
            }

            var parts = s.Split(new[] { '|' }, 3);
            if (parts.Length < 3) {
                throw new FormatException();
            }

            var path = parts[0];
            var ty = parts[1];
            var prefix = parts[2];

            var type = PythonLibraryPathType.Unspecified;
            switch (ty) {
                case "stdlib":
                    type = PythonLibraryPathType.StdLib;
                    break;
                case "site":
                    type = PythonLibraryPathType.Site;
                    break;
                case "pth":
                    type = PythonLibraryPathType.Pth;
                    break;
            }

            return new PythonLibraryPath(path, type, prefix);
        }

        /// <summary>
        /// Gets the default set of search paths based on the path to the root
        /// of the standard library.
        /// </summary>
        /// <param name="library">Root of the standard library.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        private static List<PythonLibraryPath> GetDefaultSearchPaths(string library) {
            var result = new List<PythonLibraryPath>();
            if (!Directory.Exists(library)) {
                return result;
            }

            result.Add(new PythonLibraryPath(library, PythonLibraryPathType.StdLib));

            var sitePackages = IOPath.Combine(library, "site-packages");
            if (!Directory.Exists(sitePackages)) {
                return result;
            }

            result.Add(new PythonLibraryPath(sitePackages));
            result.AddRange(ModulePath.ExpandPathFiles(sitePackages)
                .Select(p => new PythonLibraryPath(p))
            );

            return result;
        }

        /// <summary>
        /// Gets the set of search paths for the specified factory.
        /// </summary>
        public static async Task<IList<PythonLibraryPath>> GetSearchPathsAsync(InterpreterConfiguration config, IFileSystem fs, IProcessServices ps, CancellationToken cancellationToken = default) {
            for (int retries = 5; retries > 0; --retries) {
                try {
                    return await GetSearchPathsFromInterpreterAsync(config.InterpreterPath, fs, ps, cancellationToken);
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
        /// <param name="interpreter">Path to the interpreter.</param>
        /// <param name="fs">File system services.</param>
        /// <param name="ps">Process services.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        public static async Task<List<PythonLibraryPath>> GetSearchPathsFromInterpreterAsync(string interpreter, IFileSystem fs, IProcessServices ps, CancellationToken cancellationToken = default) {
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
                interpreter,
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
                return output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Select(s => {
                    try {
                        var p = Parse(s);

                        if (PathUtils.PathStartsWith(p.Path, tempWorkingDir)) {
                            return null;
                        }

                        return p;
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

        public static (IReadOnlyList<PythonLibraryPath> interpreterPaths, IReadOnlyList<PythonLibraryPath> userPaths) ClassifyPaths(
            string root,
            IFileSystem fs,
            IEnumerable<PythonLibraryPath> fromInterpreter,
            IEnumerable<string> fromUser
        ) {
#if DEBUG
            Debug.Assert(root == null || root.PathEquals(PathUtils.NormalizePathAndTrim(root)));
            Debug.Assert(!fromInterpreter.Any(p => !p.Path.PathEquals(PathUtils.NormalizePathAndTrim(p.Path))));
#endif

            // Clean up user configured paths.
            // 1) Normalize paths.
            // 2) If a path isn't rooted, then root it relative to the workspace root. If there is no root, just continue.
            // 3) Trim off any ending separators for consistency.
            // 4) Remove any empty paths, FS root paths (bad idea), or paths equal to the root.
            // 5) Deduplicate, preserving the order specified by the user.
            var fromUserList = fromUser
                .Select(PathUtils.NormalizePath)
                .Select(p => root == null || IOPath.IsPathRooted(p) ? p : IOPath.GetFullPath(IOPath.Combine(root, p))) // TODO: Replace with GetFullPath(p, root) when .NET Standard 2.1 is out.
                .Select(PathUtils.TrimEndSeparator)
                .Where(p => !string.IsNullOrWhiteSpace(p) && p != "/" && !p.PathEquals(root))
                .Distinct(PathEqualityComparer.Instance)
                .ToList();

            // Remove any interpreter paths specified in the user config so they can be reclassified.
            // The user list is usually small; List.Contains should not be too slow.
            fromInterpreter.Where(p => !fromUserList.Contains(p.Path, PathEqualityComparer.Instance))
                .Split(p => p.Type == PythonLibraryPathType.StdLib, out var stdlib, out var withoutStdlib);

            // Pull out stdlib paths, and make them always be interpreter paths.
            var interpreterPaths = new List<PythonLibraryPath>(stdlib);

            var userPaths = new List<PythonLibraryPath>();

            var allPaths = fromUserList.Select(p => new PythonLibraryPath(p))
                .Concat(withoutStdlib.Where(p => !p.Path.PathEquals(root)));

            foreach (var p in allPaths) {
                // If path is within a stdlib path, then treat it as interpreter.
                if (stdlib.Any(s => fs.IsPathUnderRoot(s.Path, p.Path))) {
                    interpreterPaths.Add(p);
                    continue;
                }

                // If Python says it's site, then treat is as interpreter.
                if (p.Type == PythonLibraryPathType.Site) {
                    interpreterPaths.Add(p);
                    continue;
                }

                // If path is outside the workspace, then treat it as interpreter.
                if (root == null || !fs.IsPathUnderRoot(root, p.Path)) {
                    interpreterPaths.Add(p);
                    continue;
                }

                userPaths.Add(p);
            }

            return (interpreterPaths, userPaths.ToList());
        }

        public override bool Equals(object obj) => obj is PythonLibraryPath other && Equals(other);

        public override int GetHashCode() {
            // TODO: Replace with HashCode.Combine when .NET Standard 2.1 is out.
            unchecked {
                var hashCode = Path.GetHashCode();
                hashCode = (hashCode * 397) ^ Type.GetHashCode();
                hashCode = (hashCode * 397) ^ ModulePrefix.GetHashCode();
                return hashCode;
            }
        }

        public bool Equals(PythonLibraryPath other) {
            if (other is null) {
                return false;
            }

            return Path.PathEquals(other.Path)
                && Type == other.Type
                && ModulePrefix == other.ModulePrefix;
        }

        public static bool operator ==(PythonLibraryPath left, PythonLibraryPath right) => left?.Equals(right) ?? right is null;
        public static bool operator !=(PythonLibraryPath left, PythonLibraryPath right) => !(left?.Equals(right) ?? right is null);
    }
}
