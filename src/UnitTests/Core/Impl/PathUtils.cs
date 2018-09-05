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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.Python.Tests.Utilities {
    public class PathUtils {
        private static readonly char[] DirectorySeparators = {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        };
        
        /// <summary>
        /// Returns the path to the parent directory segment of a path. If the
        /// last character of the path is a directory separator, the segment
        /// prior to that character is removed. Otherwise, the segment following
        /// the last directory separator is removed.
        /// </summary>
        /// <remarks>
        /// This should be used in place of:
        /// <c>Path.GetDirectoryName(CommonUtils.TrimEndSeparator(path)) + Path.DirectorySeparatorChar</c>
        /// </remarks>
        public static string GetParent(string path) {
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            }

            int last = path.Length - 1;
            if (DirectorySeparators.Contains(path[last])) {
                last -= 1;
            }

            if (last <= 0) {
                return string.Empty;
            }

            last = path.LastIndexOfAny(DirectorySeparators, last);

            if (last < 0) {
                return string.Empty;
            }

            return path.Remove(last + 1);
        }

        /// <summary>
        /// Returns a normalized directory path created by joining relativePath to root.
        /// The result is guaranteed to end with a backslash.
        /// </summary>
        /// <exception cref="ArgumentException">root is not an absolute path, or
        /// either path is invalid.</exception>
        /// <exception cref="InvalidOperationException">An absolute path cannot be
        /// created.</exception>
        public static string GetAbsoluteDirectoryPath(string root, string relativePath) {
            string absPath;

            if (string.IsNullOrEmpty(relativePath)) {
                return NormalizeDirectoryPath(root);
            }

            var relUri = MakeUri(relativePath, true, UriKind.RelativeOrAbsolute, "relativePath");
            Uri absUri;

            if (relUri.IsAbsoluteUri) {
                absUri = relUri;
            } else {
                var rootUri = MakeUri(root, true, UriKind.Absolute, "root");
                try {
                    absUri = new Uri(rootUri, relUri);
                } catch (UriFormatException ex) {
                    throw new InvalidOperationException("Cannot create absolute path", ex);
                }
            }

            absPath = absUri.IsFile ? absUri.LocalPath : absUri.AbsoluteUri;

            if (!string.IsNullOrEmpty(absPath) && !HasEndSeparator(absPath)) {
                absPath += absUri.IsFile ? Path.DirectorySeparatorChar : Path.AltDirectorySeparatorChar;
            }

            return absPath;
        }
        
        /// <summary>
        /// Returns a normalized file path created by joining relativePath to root.
        /// The result is not guaranteed to end with a backslash.
        /// </summary>
        /// <exception cref="ArgumentException">root is not an absolute path, or
        /// either path is invalid.</exception>
        public static string GetAbsoluteFilePath(string root, string relativePath) {
            var rootUri = MakeUri(root, true, UriKind.Absolute, "root");
            var relUri = MakeUri(relativePath, false, UriKind.RelativeOrAbsolute, "relativePath");

            Uri absUri;

            if (relUri.IsAbsoluteUri) {
                absUri = relUri;
            } else {
                try {
                    absUri = new Uri(rootUri, relUri);
                } catch (UriFormatException ex) {
                    throw new InvalidOperationException("Cannot create absolute path", ex);
                }
            }

            return absUri.IsFile ? absUri.LocalPath : absUri.AbsoluteUri;
        }

        /// <summary>
        /// Normalizes and returns the provided directory path, always
        /// ending with '/'.
        /// </summary>
        public static string NormalizeDirectoryPath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            var uri = MakeUri(path, true, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri) {
                if (uri.IsFile) {
                    return uri.LocalPath;
                } else {
                    return uri.AbsoluteUri.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            } else {
                return Uri.UnescapeDataString(uri.ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
        }
        
        internal static Uri MakeUri(string path, bool isDirectory, UriKind kind, string throwParameterName = "path") {
            try {
                if (isDirectory && !string.IsNullOrEmpty(path) && !HasEndSeparator(path)) {
                    path += Path.DirectorySeparatorChar;
                }

                return new Uri(path, kind);

            } catch (UriFormatException ex) {
                throw new ArgumentException("Path was invalid", throwParameterName, ex);
            } catch (ArgumentException ex) {
                throw new ArgumentException("Path was invalid", throwParameterName, ex);
            }
        }
        
        /// <summary>
        /// Returns true if the path has a directory separator character at the end.
        /// </summary>
        public static bool HasEndSeparator(string path) {
            return !string.IsNullOrEmpty(path) && DirectorySeparators.Contains(path[path.Length - 1]);
        }
    }
}
