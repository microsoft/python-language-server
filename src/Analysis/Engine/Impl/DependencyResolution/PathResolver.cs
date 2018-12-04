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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using Microsoft.Python.Parsing;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal sealed class PathResolver {
        private PathResolverSnapshot _currentSnapshot;

        public PathResolver(PythonLanguageVersion pythonLanguageVersion) {
            _currentSnapshot = new PathResolverSnapshot(pythonLanguageVersion);
        }

        public IEnumerable<string> SetRoot(in string root) {
            _currentSnapshot = _currentSnapshot.SetWorkDirectory(root, out var addedRoots);
            return addedRoots;
        }

        public IEnumerable<string> SetUserSearchPaths(in IEnumerable<string> searchPaths) {
            _currentSnapshot = _currentSnapshot.SetUserSearchPaths(searchPaths, out var addedRoots);
            return addedRoots;
        }

        public IEnumerable<string> SetInterpreterSearchPaths(in IEnumerable<string> searchPaths) {
            _currentSnapshot = _currentSnapshot.SetInterpreterPaths(searchPaths, out var addedRoots);
            return addedRoots;
        }

        public void SetBuiltins(in IEnumerable<string> builtinModuleNames) => _currentSnapshot = _currentSnapshot.SetBuiltins(builtinModuleNames);
        public void RemoveModulePath(in string path) => _currentSnapshot = _currentSnapshot.RemoveModulePath(path);
        public bool TryAddModulePath(in string path, out string fullModuleName) {
            _currentSnapshot = _currentSnapshot.AddModulePath(path, out fullModuleName);
            return fullModuleName != null;
        }

        public PathResolverSnapshot CurrentSnapshot => _currentSnapshot;
    }
}
