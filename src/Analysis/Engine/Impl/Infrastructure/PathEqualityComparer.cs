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
        public static readonly PathEqualityComparer Instance = new PathEqualityComparer(
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux), Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar
        );

        private static readonly char[] InvalidPathChars = GetInvalidPathChars();

        private static char[] GetInvalidPathChars() {
            return Path.GetInvalidPathChars().Concat(new[] { '*', '?' }).ToArray();
        }

        // This is just a small cache to deal with the same keys being used
        // frequently in a loop.
        internal class CacheItem {
            public string CompareKey;
            public long Accessed;
        }
        internal readonly Dictionary<string, CacheItem> _compareKeyCache = new Dictionary<string, CacheItem>();
        private long _accessCount;
        private const int CACHE_UPPER_LIMIT = 32;

        private readonly bool _isCaseSensitivePath;
        private readonly char _directorySeparator;
        private readonly char _altDirectorySeparator;
        private readonly char[] _directorySeparators;

        internal PathEqualityComparer(bool isCaseSensitivePath, char directorySeparator, char altDirectorySeparator = '\0') {
            _isCaseSensitivePath = isCaseSensitivePath;
            _directorySeparator = directorySeparator;
            _altDirectorySeparator = altDirectorySeparator;

            if (altDirectorySeparator != '\0' && altDirectorySeparator != directorySeparator) {
                _directorySeparators = new[] { directorySeparator, altDirectorySeparator };
            } else {
                _directorySeparators = new[] { directorySeparator };
            }
        }

        internal string GetCompareKeyUncached(string path) {
            if (string.IsNullOrEmpty(path)) {
                return path;
            }

            var root = string.Empty;
            var rootParts = 0;
            var parts = new List<string>();
            int next_i = int.MaxValue;
            for (var i = 0; next_i > 0 && i < path.Length; i = next_i) {
                string segment;
                next_i = path.IndexOfAny(_directorySeparators, i) + 1;

                if (next_i <= 0) {
                    segment = path.Substring(i);
                } else if (i == 0 && next_i > 2 && next_i < path.Length - 1 && path[next_i - 2] == ':' && path[next_i] == '/') {
                    // smb://computer/share
                    next_i++;
                    segment = path.Substring(0, next_i);
                } else {
                    segment = path.Substring(i, next_i - i - 1);
                }

                if (segment.Length == 0) {
                    if (i == 0 && next_i == 1 && _directorySeparator == '\\') {
                        // Windows UNC
                        // There are two slashes, so our first four segments will
                        // be protected:
                        //
                        //   \\computer\share
                        //
                        // Segment 1: '' before first \
                        // Segment 2: '' between first and second \
                        // Segment 3: 'computer'
                        // Segment 4: 'share'
                        parts.Add(string.Empty);
                        parts.Add(string.Empty);    // the second one will be skipped
                        rootParts = 4;
                    }
                } else if (segment == ".") {
                    // Do nothing
                } else if (segment == "..") {
                    if (parts.Count > rootParts) {
                        parts.RemoveAt(parts.Count - 1);
                    } else {
                        parts.Add(segment);
                        rootParts += 1;
                    }
                } else {
                    if (parts.Count == 0 && segment.Last() == ':') {
                        rootParts = 1;
                    }
                    segment = segment.TrimEnd('.', ' ', '\t');
                    if (!_isCaseSensitivePath) {
                        segment = segment.ToUpperInvariant();
                    }
                    parts.Add(segment);
                }
            }
            return root + string.Join(_directorySeparator.ToString(), parts);
        }

        internal CacheItem GetOrCreateCacheItem(string key, out bool created) {
            CacheItem item;
            created = true;
            var access = Interlocked.Increment(ref _accessCount);
            lock (_compareKeyCache) {
                if (_compareKeyCache.TryGetValue(key, out item)) {
                    created = false;
                }

                if (created) {
                    if (_compareKeyCache.Count > CACHE_UPPER_LIMIT) {
                        // Purge half the old items in the cache
                        foreach (var k in _compareKeyCache.OrderBy(kv => kv.Value.Accessed).Take(CACHE_UPPER_LIMIT / 2).Select(kv => kv.Key).ToArray()) {
                            _compareKeyCache.Remove(k);
                        }
                    }

                    _compareKeyCache[key] = item = new CacheItem { Accessed = access };
                }
            }

            if (!created) {
                lock (item) {
                    item.Accessed = access;
                }
            }

            return item;
        }

        private string GetCompareKey(string path) {
            var item = GetOrCreateCacheItem(path, out bool created);

            string result;
            lock (item) {
                if (created) {
                    item.CompareKey = result = GetCompareKeyUncached(path);
                } else {
                    result = item.CompareKey;
                }
            }

            // The only time the result is null is if we race with initialization.
            // This can only happen cross-thread, so let's loop until it is ready.
            while (result == null) {
                Thread.Yield();
                lock (item) {
                    result = item.CompareKey;
                }
            }

            return result;
        }

        public static bool IsValidPath(string x) {
            if (string.IsNullOrEmpty(x)) {
                return false;
            }
            return x.IndexOfAny(InvalidPathChars) < 0;
        }

        public bool StartsWith(string x, string prefix, bool allowFullMatch = true) {
            prefix = GetCompareKey(prefix);
            x = GetCompareKey(x);

            if (StringComparer.Ordinal.Equals(prefix, x)) {
                return allowFullMatch;
            }

            return x.StartsWithOrdinal(prefix + Path.DirectorySeparatorChar);
        }

        public bool Equals(string x, string y)
            => StringComparer.Ordinal.Equals(GetCompareKey(x), GetCompareKey(y));

        public int GetHashCode(string obj)
            => StringComparer.Ordinal.GetHashCode(GetCompareKey(obj));
    }
}
