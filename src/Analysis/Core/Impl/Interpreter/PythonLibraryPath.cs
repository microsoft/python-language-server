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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private static readonly Regex ParseRegex = new Regex(
            @"(?<path>[^|]+)\|(?<type>[^|]+)\|(?<prefix>[^|]+)?"
        );

        public PythonLibraryPath(string path, PythonLibraryPathType type = PythonLibraryPathType.Unspecified, string modulePrefix = null) {
            Path = PathUtils.TrimEndSeparator(PathUtils.NormalizePath(path));
            Type = type;
            ModulePrefix = modulePrefix ?? string.Empty;
        }

        public PythonLibraryPath(string path, bool isStandardLibrary, string modulePrefix) :
            this(path, isStandardLibrary ? PythonLibraryPathType.StdLib : PythonLibraryPathType.Unspecified, modulePrefix) { }

        public string Path { get; }

        public PythonLibraryPathType Type { get; }

        public string ModulePrefix { get; } = string.Empty;

        public bool IsStandardLibrary => Type == PythonLibraryPathType.StdLib;

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

        public static PythonLibraryPath Parse(string s) {
            if (string.IsNullOrEmpty(s)) {
                throw new ArgumentNullException("source");
            }

            var m = ParseRegex.Match(s);
            if (!m.Success || !m.Groups["path"].Success || !m.Groups["type"].Success) {
                throw new FormatException();
            }

            PythonLibraryPathType type = PythonLibraryPathType.Unspecified;

            switch (m.Groups["type"].Value) {
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

            return new PythonLibraryPath(
                m.Groups["path"].Value,
                type,
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
                    if (s.PathStartsWith(tempWorkingDir)) {
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
            // PRECONDITIONS:
            // - root has already been normalized and had its end separator trimmed.
            // - All paths in fromInterpreter were normalised and end separator trimmed.

            // Clean up user configured paths.
            // 1) Noramlize paths.
            // 2) If a path isn't rooted, then root it relative to the workspace root. If there is no root, just continue.
            // 3) Trim off any ending separators for consistency.
            // 4) Remove any empty paths, FS root paths (bad idea), or paths equal to the root.
            fromUser = fromUser
                .Select(PathUtils.NormalizePath)
                .Select(p => root == null || IOPath.IsPathRooted(p) ? p : IOPath.GetFullPath(IOPath.Combine(root, p))) // TODO: Replace with GetFullPath(p, root) when .NET Standard 2.1 is out.
                .Select(PathUtils.TrimEndSeparator)
                .Where(p => !string.IsNullOrWhiteSpace(p) && p != "/" && !p.PathEquals(root));

            // Deduplicate, and keep in a set to quickly check interpreter paths against.
            var fromUserSet = new HashSet<string>(fromUser, PathEqualityComparer.Instance);

            // Remove any interpreter paths specified in the user config so they can be reclassified.
            fromInterpreter = fromInterpreter.Where(p => !fromUserSet.Contains(p.Path));

            var stdlibLookup = fromInterpreter.ToLookup(p => p.Type == PythonLibraryPathType.StdLib);

            // Pull out stdlib paths, and make them always be interpreter paths.
            var stdlib = stdlibLookup[true].ToList();
            var interpreterPaths = new List<PythonLibraryPath>(stdlib);
            fromInterpreter = stdlibLookup[false];

            var userPaths = new SortedSet<PythonLibraryPath>(PathDepthComparer.Instance);

            var allPaths = fromUserSet.Select(p => new PythonLibraryPath(p))
                .Concat(fromInterpreter.Where(p => !p.Path.PathEquals(root)));

            foreach (var p in allPaths) {
                // If path is within a stdlib path, then treat it as interpreter.
                if (stdlib.Any(s => fs.IsPathUnderRoot(s.Path, p.Path))) {
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

        public bool Equals(PythonLibraryPath other) => Path == other.Path && Type == other.Type && ModulePrefix == other.ModulePrefix;

        public static bool operator ==(PythonLibraryPath left, PythonLibraryPath right) => left.Equals(right);

        public static bool operator !=(PythonLibraryPath left, PythonLibraryPath right) => !left.Equals(right);

        private class PathDepthComparer : IComparer, IComparer<PythonLibraryPath> {
            public static readonly PathDepthComparer Instance = new PathDepthComparer();

            private PathDepthComparer() { }

            public int Compare(object x, object y) {
                return Compare((PythonLibraryPath)x, (PythonLibraryPath)y);
            }

            public int Compare(PythonLibraryPath x, PythonLibraryPath y) {
                var xSeps = x.Path.Count(c => c == IOPath.DirectorySeparatorChar);
                var ySeps = y.Path.Count(c => c == IOPath.DirectorySeparatorChar);

                var sepComp = xSeps.CompareTo(ySeps);
                if (sepComp != 0) {
                    // Deepest first.
                    return -sepComp;
                }

                return x.Path.PathCompare(y.Path);
            }
        }
    }
}
