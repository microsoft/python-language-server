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

        public void SetRoot(string root) => _currentSnapshot = _currentSnapshot.SetRoot(root);
        public void SetSearchPaths(IEnumerable<string> searchPaths) => _currentSnapshot = _currentSnapshot.SetSearchPaths(searchPaths);
        public void AddModulePath(string path) => _currentSnapshot = _currentSnapshot.AddModulePath(path);
        public PathResolverSnapshot CurrentSnapshot => _currentSnapshot;
    }
}
