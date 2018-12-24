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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents package with child modules. Typically
    /// used in scenarios such as 'import a.b.c'.
    /// </summary>
    internal sealed class PythonPackage: PythonModule, IPythonPackage {
        private readonly ConcurrentDictionary<string, IPythonModule> _childModules = new ConcurrentDictionary<string, IPythonModule>();

        public PythonPackage(string name, IServiceContainer services) 
            : base(name, ModuleType.Package, services) { }

        public void AddChildModule(string name, IPythonModule module) {
            if (!_childModules.ContainsKey(name)) {
                _childModules[name] = module;
            }
        }

        public override IEnumerable<string> GetMemberNames() => _childModules.Keys.ToArray();
        public override IMember GetMember(string name) => _childModules.TryGetValue(name, out var v) ? v : null;
        public IEnumerable<string> GetChildrenModuleNames() => GetMemberNames();
    }
}
