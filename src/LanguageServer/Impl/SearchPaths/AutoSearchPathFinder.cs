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

using System.IO;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.LanguageServer.SearchPaths {
    internal static class AutoSearchPathFinder {
        public static ImmutableArray<string> Find(IFileSystem fs, string root) {
            // Only happens in the case of a non-folder open, in which case we aren't going to end up with any valid user search paths.
            if (root == null) {
                return ImmutableArray<string>.Empty;
            }

            // For now, just check for "src".
            var srcDir = Path.Combine(root, "src");
            if (!fs.DirectoryExists(srcDir)) {
                return ImmutableArray<string>.Empty;
            }

            // If src is definitely an importable package, then don't add it as an import root, since otherwise it'd be unimportable.
            // There are still cases where "import src.foo.bar" are importable, but more difficult to check.
            var srcInit = Path.Combine(srcDir, "__init__.py");
            if (fs.FileExists(srcInit)) {
                return ImmutableArray<string>.Empty;
            }

            return ImmutableArray<string>.Create(srcDir);
        }
    }
}
