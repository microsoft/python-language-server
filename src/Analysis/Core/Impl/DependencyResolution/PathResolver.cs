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
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public sealed class PathResolver {
        public PathResolver(in PythonLanguageVersion pythonLanguageVersion, in string root, in ImmutableArray<string> interpreterSearchPaths, in ImmutableArray<string> userSearchPaths) {
            // need to audit whether CurrentSnapshot ever get updated concurrently
            // thread safety is probably fine but serialization is another issue where if 2 callers do TryAddModulePath or other calls concurrently, 
            // they will use same CurrentSnapshot to update which ends up with 2 different snapshots with same version.
            CurrentSnapshot = new PathResolverSnapshot(pythonLanguageVersion, root, interpreterSearchPaths, userSearchPaths);
        }

        public void SetBuiltins(in IEnumerable<string> builtinModuleNames) => CurrentSnapshot = CurrentSnapshot.SetBuiltins(builtinModuleNames);
        public void RemoveModulePath(in string path) => CurrentSnapshot = CurrentSnapshot.RemoveModulePath(path);
        public bool TryAddModulePath(in string path, long fileSize, in bool allowNonRooted, out string fullModuleName) {
            CurrentSnapshot = CurrentSnapshot.AddModulePath(path, fileSize, allowNonRooted, out fullModuleName);
            return fullModuleName != null;
        }

        public PathResolverSnapshot CurrentSnapshot { get; private set; }
    }
}
