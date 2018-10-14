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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    class PathEqualityComparer : IEqualityComparer<string> {
        public static readonly PathEqualityComparer Instance = new PathEqualityComparer();
        private static readonly char[] InvalidPathChars = GetInvalidPathChars();
        private static StringComparison _pathComparisonType;

        private static char[] GetInvalidPathChars() {
            return Path.GetInvalidPathChars().Concat(new[] { '*', '?' }).ToArray();
        }

        private readonly StringComparer Ordinal = StringComparer.Ordinal;

        private PathEqualityComparer() {
            _pathComparisonType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        public static bool IsValidPath(string x)
            => string.IsNullOrEmpty(x) ? false : x.IndexOfAny(InvalidPathChars) < 0;

        public bool StartsWith(string x, string prefix) {
            prefix = prefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Normalize(x).StartsWith(prefix, _pathComparisonType);
        }

        public bool Equals(string x, string y) 
            => Normalize(x).Equals(Normalize(y), _pathComparisonType);

        public int GetHashCode(string obj) => obj.GetHashCode();

        internal static string Normalize(string x) {
            x = new Uri(x).LocalPath;
            x =  Path.IsPathRooted(x) ? x : Path.GetFullPath(x);
            return x.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
